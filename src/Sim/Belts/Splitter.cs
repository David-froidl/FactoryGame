using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Belts;

/// <summary>
/// One input, N outputs, round-robin. Upstream <i>pushes</i> into the splitter
/// (<see cref="IItemSink"/>), the splitter pushes onward on its own tick.
///
/// Holds exactly one item, like a Satisfactory splitter. That single slot is what makes
/// backpressure work: while it is occupied the splitter refuses new items and the belt
/// feeding it stalls, rather than the splitter buffering an unbounded queue.
///
/// Fairness rule: the cursor advances only on a <i>successful</i> hand-off. So with every
/// output free, items alternate strictly 0,1,2,0,1,2; with output 1 blocked they go
/// 0,2,0,2 and no throughput is lost. That matches player expectation in both games.
/// </summary>
public sealed class Splitter : ISimNode, IItemSink
{
    private readonly List<IItemSink> _outputs = new(3);
    private ItemId _held;
    private int _cursor;

    /// <summary>Outputs in the order they were added; the round-robin cursor walks this list.</summary>
    public IReadOnlyList<IItemSink> Outputs => _outputs;

    /// <summary>The item currently occupying the splitter's single slot, if any.</summary>
    public ItemId Held => _held;

    public long TotalPassed { get; private set; }

    public void AddOutput(IItemSink output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _outputs.Add(output);
    }

    public bool CanAccept(ItemId item) => item.IsValid && !_held.IsValid;

    public bool TryAccept(ItemId item)
    {
        if (!CanAccept(item)) return false;
        _held = item;
        return true;
    }

    public void Tick()
    {
        if (!_held.IsValid || _outputs.Count == 0) return;

        int n = _outputs.Count;
        for (int k = 0; k < n; k++)
        {
            int index = _cursor + k;
            if (index >= n) index -= n;

            if (_outputs[index].TryAccept(_held))
            {
                _cursor = index + 1 == n ? 0 : index + 1;
                _held = ItemId.None;
                TotalPassed++;
                return;
            }
        }
        // Every output is blocked: keep the item and stall the upstream belt.
    }

    public override string ToString() => $"Splitter({_outputs.Count} out, held={_held})";
}
