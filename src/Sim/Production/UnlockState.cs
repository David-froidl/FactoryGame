namespace Factory.Sim.Production;

/// <summary>
/// Which unlock ids are active for the current session. Session-only by design (nothing
/// here persists to disk) — this vertical slice only needs unlocks to survive for as long
/// as the game is running.
///
/// One instance is meant to be shared by every <see cref="MilestoneTerminal"/> and queried
/// by the build menu (Phase 6) to decide what's placeable. <see cref="Unlocked"/> fires
/// exactly once per id, the first time it becomes unlocked — the natural hook for a
/// one-shot "unlocked!" UI notification (Phase 7).
/// </summary>
public sealed class UnlockState
{
    private readonly HashSet<string> _unlocked = new();

    /// <summary>Raised exactly once, the first time the named id is unlocked. Never re-raised for an id already unlocked.</summary>
    public event Action<string>? Unlocked;

    public IReadOnlyCollection<string> All => _unlocked;

    public bool IsUnlocked(string unlockId) => _unlocked.Contains(unlockId);

    /// <summary>Marks <paramref name="unlockId"/> unlocked. Returns false if it already was (idempotent, no duplicate event).</summary>
    public bool Unlock(string unlockId)
    {
        if (string.IsNullOrWhiteSpace(unlockId))
            throw new ArgumentException("Unlock id must not be null or empty.", nameof(unlockId));
        if (!_unlocked.Add(unlockId)) return false;

        Unlocked?.Invoke(unlockId);
        return true;
    }
}
