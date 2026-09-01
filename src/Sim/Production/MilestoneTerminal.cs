using Factory.Sim.Core;
using Factory.Sim.Items;

namespace Factory.Sim.Production;

/// <summary>
/// Accepts deliveries of one item type and unlocks <see cref="MilestoneDefinition.UnlockId"/>
/// once <see cref="MilestoneDefinition.RequiredCount"/> have been delivered. Unlike
/// <see cref="Machine"/> it never produces anything and has nothing to advance per tick —
/// it only reacts to deliveries, so <see cref="Tick"/> is a no-op.
///
/// Keeps accepting deliveries past the threshold rather than refusing them: a terminal is
/// an ongoing deposit point, not a bounded buffer, so blocking it once the goal is met
/// would just back a belt up all the way to its source for no gameplay benefit. The unlock
/// itself only ever fires once, at the exact delivery that first reaches the threshold.
/// </summary>
public sealed class MilestoneTerminal : ISimNode, IItemSink
{
    public MilestoneTerminal(MilestoneDefinition definition, UnlockState unlockState)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(unlockState);
        Definition = definition;
        UnlockState = unlockState;
    }

    public MilestoneDefinition Definition { get; }

    public UnlockState UnlockState { get; }

    /// <summary>Lifetime count of accepted deliveries. Never capped at <see cref="MilestoneDefinition.RequiredCount"/>.</summary>
    public int DeliveredCount { get; private set; }

    public bool IsThresholdMet => DeliveredCount >= Definition.RequiredCount;

    public bool CanAccept(ItemId item) => item.IsValid && item == Definition.RequiredItem;

    public bool TryAccept(ItemId item)
    {
        if (!CanAccept(item)) return false;

        DeliveredCount++;
        if (DeliveredCount >= Definition.RequiredCount)
            UnlockState.Unlock(Definition.UnlockId);

        return true;
    }

    public void Tick() { }

    public override string ToString()
        => $"MilestoneTerminal({Definition.Id}, {DeliveredCount}/{Definition.RequiredCount})";
}
