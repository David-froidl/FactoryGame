namespace Factory.Sim;

/// <summary>
/// Fixed-point world units and tick rate for the whole simulation.
///
/// Everything positional is a 32-bit integer count of "units". Floating point is
/// deliberately kept out of the sim so that a tick is bit-for-bit reproducible on
/// any machine (needed for deterministic saves, replays and future multiplayer).
/// Floats appear only at the render boundary, where positions are converted to
/// metres and interpolated.
/// </summary>
public static class SimConstants
{
    /// <summary>Simulation frequency. Decoupled from render framerate.</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Seconds per tick, for the render-side interpolation clock.</summary>
    public const double SecondsPerTick = 1.0 / TicksPerSecond;

    /// <summary>Fixed-point units in one world tile (one belt tile is 1 unit of building grid).</summary>
    public const int UnitsPerTile = 4800;

    /// <summary>
    /// Centre-to-centre distance between two items packed solid on a belt.
    /// 1200 units = 4 items per tile.
    ///
    /// Chosen so that belt speed in units/tick equals items-per-minute exactly:
    ///     speed = ItemSpacing * ipm / (60 * TicksPerSecond) = 1200 * ipm / 1200 = ipm
    /// </summary>
    public const int ItemSpacing = 1200;

    /// <summary>Items per tile at maximum density.</summary>
    public const int ItemsPerTile = UnitsPerTile / ItemSpacing;

    /// <summary>Converts a designer-facing belt rate to fixed-point units per tick.</summary>
    public static int ItemsPerMinuteToSpeed(int itemsPerMinute)
        => checked(ItemSpacing * itemsPerMinute / (60 * TicksPerSecond));

    /// <summary>Inverse of <see cref="ItemsPerMinuteToSpeed"/>.</summary>
    public static int SpeedToItemsPerMinute(int unitsPerTick)
        => checked(unitsPerTick * 60 * TicksPerSecond / ItemSpacing);

    /// <summary>
    /// True when a belt rate is achievable exactly at the current tick rate.
    ///
    /// A saturated belt emits one item every <c>ItemSpacing / speed</c> ticks. If that
    /// is not a whole number the head item's travel is truncated at the belt end and the
    /// belt under-delivers. Equivalently: a rate is exact iff it divides
    /// <c>60 * TicksPerSecond</c> = 1200 items/min. Keep every belt tier on that list.
    /// </summary>
    public static bool IsExactRate(int itemsPerMinute)
        => itemsPerMinute > 0 && (60 * TicksPerSecond) % itemsPerMinute == 0;
}

/// <summary>
/// Belt tiers, as items per minute. Every value divides 1200 so it is exact at 20 Hz
/// (see <see cref="SimConstants.IsExactRate"/>); <c>BeltSpeedTests</c> enforces that.
/// These live here only as sane defaults — real buildable definitions become data in Phase 1.
/// </summary>
public static class BeltTiers
{
    public const int Mk1 = 60;
    public const int Mk2 = 120;
    public const int Mk3 = 240;
    public const int Mk4 = 400;
    public const int Mk5 = 600;
    public const int Mk6 = 1200;

    public static readonly int[] All = { Mk1, Mk2, Mk3, Mk4, Mk5, Mk6 };
}
