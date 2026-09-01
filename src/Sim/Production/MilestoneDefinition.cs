using Factory.Sim.Items;

namespace Factory.Sim.Production;

/// <summary>
/// Static definition of one progress milestone: deliver <see cref="RequiredCount"/> of
/// <see cref="RequiredItem"/> to a <see cref="MilestoneTerminal"/> and <see cref="UnlockId"/>
/// becomes unlocked. Deliberately not loaded from JSON yet (Phase 3 needs exactly one:
/// assembly cores unlocking belt tier 2) — the buildable data added in Phase 4 is the more
/// natural place to carry this alongside the terminal's other placement data.
/// </summary>
public sealed class MilestoneDefinition
{
    public MilestoneDefinition(string id, ItemId requiredItem, int requiredCount, string unlockId)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Milestone id must not be null or empty.", nameof(id));
        if (!requiredItem.IsValid)
            throw new ArgumentException("Milestone requires a valid item.", nameof(requiredItem));
        if (requiredCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredCount), requiredCount, "Must be positive.");
        if (string.IsNullOrWhiteSpace(unlockId))
            throw new ArgumentException("Unlock id must not be null or empty.", nameof(unlockId));

        Id = id;
        RequiredItem = requiredItem;
        RequiredCount = requiredCount;
        UnlockId = unlockId;
    }

    public string Id { get; }

    public ItemId RequiredItem { get; }

    public int RequiredCount { get; }

    /// <summary>What becomes unlocked in <see cref="UnlockState"/> once the threshold is met.</summary>
    public string UnlockId { get; }
}
