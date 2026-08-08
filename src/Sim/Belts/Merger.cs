using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Belts;

/// <summary>
/// N inputs, one output, strict priority by input order (index 0 wins).
///
/// A merger <i>pulls</i>, unlike everything else in the sim which pushes. It has to:
/// priority is only meaningful if one node decides the order, and a push model would make
/// the winner depend on tick order instead. Belts feeding a merger therefore leave
/// <see cref="BeltSegment.Output"/> null — <c>BeltNetwork.Connect</c> enforces that.
///
/// Strict priority means a saturated high-priority input starves the others. That is the
/// intended, testable behaviour; a round-robin mode is a small addition when it is needed.
/// </summary>
public sealed class Merger : ISimNode, IItemSource
{
    private readonly List<IItemSource> _inputs = new(3);
    private ItemId _held;

    /// <summary>Inputs in descending priority: index 0 is served first.</summary>
    public IReadOnlyList<IItemSource> Inputs => _inputs;

    /// <summary>Where merged items go. Null means the merger fills its slot and stalls.</summary>
    public IItemSink? Output { get; set; }

    public ItemId Held => _held;

    public long TotalPassed { get; private set; }

    /// <summary>Adds an input at the lowest priority so far.</summary>
    public void AddInput(IItemSource input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _inputs.Add(input);
    }

    public void Tick()
    {
        // Push first, then pull, so the slot turns over once per tick at full rate.
        if (_held.IsValid && Output is not null && Output.TryAccept(_held))
        {
            _held = ItemId.None;
            TotalPassed++;
        }

        if (_held.IsValid) return; // Output blocked: hold the item, stall every input.

        for (int i = 0; i < _inputs.Count; i++)
        {
            if (_inputs[i].TryTake(out ItemId item))
            {
                _held = item;
                return;
            }
        }
    }

    // A merger can also be pulled from (e.g. chained into another merger).
    public bool TryPeek(out ItemId item)
    {
        item = _held;
        return _held.IsValid;
    }

    public bool TryTake(out ItemId item)
    {
        item = _held;
        if (!_held.IsValid) return false;
        _held = ItemId.None;
        TotalPassed++;
        return true;
    }

    public override string ToString() => $"Merger({_inputs.Count} in, held={_held})";
}
