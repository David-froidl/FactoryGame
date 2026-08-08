using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Belts;

/// <summary>
/// A straight run of belt between two endpoints (machine, splitter, merger, corner).
/// Items on it are <b>data, not objects</b>: two parallel arrays holding the item id and
/// the gap to the item ahead. There is no per-item allocation and nothing to garbage collect.
///
/// Positions are relative, measured backwards from the output end:
///   <c>position(i) = gap[0] + gap[1] + ... + gap[i]</c>
/// so item 0 is the front-most item and <c>gap[0]</c> is its distance to the belt's output.
/// Because positions are cumulative, moving the head implicitly moves everything behind it
/// for free — that is the whole point of the representation. A free-flowing belt of 200
/// items costs one subtraction per tick.
///
/// Work is only needed when the head cannot move a full step: then items behind close up
/// to <see cref="ItemSpacing"/> one at a time, and the loop exits the moment the movement
/// deficit reaches zero. <see cref="_compressedHead"/> lets an already-jammed prefix be
/// skipped, so a fully stalled belt is also O(1) per tick rather than O(items).
/// </summary>
public sealed class BeltSegment : ISimNode, IItemSink, IItemSource
{
    // Two parallel arrays instead of one array of structs: the hot advance loop touches
    // only gaps, so it streams 4 bytes per item instead of 8. Both are ring buffers —
    // items leave at the front and arrive at the back, and a ring makes both O(1).
    private readonly int[] _gaps;
    private readonly ItemId[] _items;
    private readonly int _mask;

    private int _head;
    private int _count;

    /// <summary>Sum of all gaps, i.e. the position of the last (rear-most) item.</summary>
    private int _gapSum;

    /// <summary>
    /// Lower bound on the number of leading items already packed solid: gaps 1.._compressedHead-1
    /// are known to equal <see cref="ItemSpacing"/>. Safe to understate, never overstate.
    /// </summary>
    private int _compressedHead;

    /// <param name="length">Belt length in fixed-point units (see <see cref="SimConstants.UnitsPerTile"/>).</param>
    /// <param name="speed">Units travelled per tick. Use <see cref="SimConstants.ItemsPerMinuteToSpeed"/>.</param>
    /// <param name="itemSpacing">Minimum centre-to-centre distance between items.</param>
    public BeltSegment(int length, int speed, int itemSpacing = SimConstants.ItemSpacing)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be positive.");
        if (itemSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(itemSpacing), itemSpacing, "Spacing must be positive.");
        if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        if (speed > itemSpacing)
            throw new ArgumentOutOfRangeException(nameof(speed), speed,
                "Speed cannot exceed item spacing: at most one item may leave a belt per tick.");

        Length = length;
        Speed = speed;
        ItemSpacing = itemSpacing;
        Capacity = length / itemSpacing + 1;

        int ringSize = NextPowerOfTwo(Capacity);
        _gaps = new int[ringSize];
        _items = new ItemId[ringSize];
        _mask = ringSize - 1;
    }

    /// <summary>Belt length in fixed-point units.</summary>
    public int Length { get; }

    /// <summary>Units travelled per tick.</summary>
    public int Speed { get; }

    /// <summary>Minimum centre-to-centre distance between two items.</summary>
    public int ItemSpacing { get; }

    /// <summary>Maximum number of items this belt can hold at full density.</summary>
    public int Capacity { get; }

    /// <summary>Items currently on the belt.</summary>
    public int Count => _count;

    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Where the head item goes when it reaches the output end. Null means a dead end —
    /// items pile up and back-pressure upstream, which is exactly what we want for an
    /// unconnected belt. Leave null when something <i>pulls</i> from this belt instead
    /// (a merger); see <c>BeltNetwork</c>.
    /// </summary>
    public IItemSink? Output { get; set; }

    /// <summary>Lifetime counters, useful for throughput assertions and for the renderer.</summary>
    public long TotalInserted { get; private set; }

    public long TotalPopped { get; private set; }

    /// <summary>Distance from the rear-most item to the input end. Full belt length when empty.</summary>
    public int TailSpace => Length - _gapSum;

    /// <summary>Gap between item <paramref name="index"/> and the item ahead of it (or the output end for 0).</summary>
    public int GapAt(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        return _gaps[(_head + index) & _mask];
    }

    public ItemId ItemAt(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        return _items[(_head + index) & _mask];
    }

    // ---- IItemSink: items enter at the rear ----

    public bool CanAccept(ItemId item)
        => item.IsValid && _count < Capacity && (_count == 0 || TailSpace >= ItemSpacing);

    public bool TryAccept(ItemId item)
    {
        if (!CanAccept(item)) return false;

        // The new item enters at the very rear, so its gap to the previous rear item is
        // whatever slack was left. Post-condition: _gapSum == Length.
        int gap = TailSpace;
        int slot = (_head + _count) & _mask;
        _items[slot] = item;
        _gaps[slot] = gap;
        _gapSum += gap;
        _count++;
        TotalInserted++;
        return true;
    }

    /// <summary>Throwing form of <see cref="TryAccept"/>, for call sites that already checked.</summary>
    public void Insert(ItemId item)
    {
        if (!TryAccept(item))
            throw new InvalidOperationException(
                $"Belt cannot accept {item}: count={_count}/{Capacity}, tailSpace={TailSpace}, spacing={ItemSpacing}.");
    }

    // ---- IItemSource: items leave from the front, once they reach the output end ----

    /// <summary>True when the head item has reached the output end and is ready to hand over.</summary>
    public bool TryPeek(out ItemId item)
    {
        if (_count > 0 && _gaps[_head] == 0)
        {
            item = _items[_head];
            return true;
        }

        item = ItemId.None;
        return false;
    }

    public bool TryTake(out ItemId item) => TryPop(out item);

    /// <summary>Removes the head item if it has reached the output end.</summary>
    public bool TryPop(out ItemId item)
    {
        if (_count == 0 || _gaps[_head] != 0)
        {
            item = ItemId.None;
            return false;
        }

        item = RemoveHead();
        return true;
    }

    // ---- Tick ----

    /// <summary>
    /// Advance every item one tick, then hand the head item to <see cref="Output"/> if it
    /// has arrived. Advance-then-hand-off (rather than the reverse) keeps throughput exact:
    /// the head reaches the end and leaves in the same tick, so followers are never held up
    /// by an item that is about to disappear.
    /// </summary>
    public void Tick()
    {
        Advance();

        if (Output is not null && _count > 0 && _gaps[_head] == 0)
        {
            ItemId head = _items[_head];
            if (Output.TryAccept(head)) RemoveHead();
        }
    }

    private void Advance()
    {
        if (_count == 0) return;

        int headGap = _gaps[_head];
        int moved = headGap < Speed ? headGap : Speed;
        if (moved != 0)
        {
            _gaps[_head] = headGap - moved;
            _gapSum -= moved;
        }

        int deficit = Speed - moved;
        if (deficit == 0) return; // Free-flowing: everything behind inherits the move. O(1).

        // The head ran into the output end. Items behind close up until they hit minimum
        // spacing; each one that fully absorbs the deficit ends the propagation.
        for (int i = _compressedHead < 1 ? 1 : _compressedHead; i < _count; i++)
        {
            int slot = (_head + i) & _mask;
            int slack = _gaps[slot] - ItemSpacing;

            if (slack >= deficit)
            {
                _gaps[slot] -= deficit;
                _gapSum -= deficit;
                _compressedHead = slack == deficit ? i + 1 : i;
                return;
            }

            if (slack > 0)
            {
                _gaps[slot] = ItemSpacing;
                _gapSum -= slack;
                deficit -= slack;
            }

            _compressedHead = i + 1;
        }

        _compressedHead = _count;
    }

    // ---- Rendering support ----

    /// <summary>
    /// Writes absolute positions (distance from the output end, ascending) and item ids
    /// into caller-owned buffers. Returns the number written. Allocation-free by design:
    /// the renderer keeps its scratch arrays for the lifetime of the belt.
    /// </summary>
    public int CopyTo(Span<int> positions, Span<ItemId> items)
    {
        int n = Math.Min(_count, Math.Min(positions.Length, items.Length));
        int position = 0;
        for (int i = 0; i < n; i++)
        {
            int slot = (_head + i) & _mask;
            position += _gaps[slot];
            positions[i] = position;
            items[i] = _items[slot];
        }

        return n;
    }

    public void Clear()
    {
        Array.Clear(_gaps);
        Array.Clear(_items);
        _head = 0;
        _count = 0;
        _gapSum = 0;
        _compressedHead = 0;
    }

    public override string ToString()
        => $"Belt(len={Length}, speed={Speed}, {_count}/{Capacity} items)";

    private ItemId RemoveHead()
    {
        ItemId item = _items[_head];
        _gapSum -= _gaps[_head];
        _items[_head] = ItemId.None;
        _gaps[_head] = 0;
        _head = (_head + 1) & _mask;
        _count--;
        // Every index shifted down by one.
        if (_compressedHead > 0) _compressedHead--;
        TotalPopped++;
        return item;
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value) result <<= 1;
        return result;
    }
}
