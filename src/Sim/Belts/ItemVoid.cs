using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Belts;

/// <summary>
/// A sink that always accepts and counts what it swallows. Stands in for "the rest of the
/// factory" in throughput tests and in the render demo, so a belt can run saturated
/// forever without a machine existing yet. Replace with a real machine input in Phase 1.
/// </summary>
public sealed class ItemVoid : ISimNode, IItemSink
{
    public long Consumed { get; private set; }

    /// <summary>When false the void refuses everything — a one-line way to test backpressure.</summary>
    public bool Open { get; set; } = true;

    public bool CanAccept(ItemId item) => Open && item.IsValid;

    public bool TryAccept(ItemId item)
    {
        if (!CanAccept(item)) return false;
        Consumed++;
        return true;
    }

    public void Tick() { }

    public override string ToString() => $"ItemVoid(consumed={Consumed}, open={Open})";
}
