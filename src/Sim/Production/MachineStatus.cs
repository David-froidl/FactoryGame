namespace Factory.Sim.Production;

/// <summary>
/// A <see cref="Machine"/>'s current production state. Computed live from its internal
/// buffers (see <c>Machine.Status</c>), never stored separately — so it can never drift
/// out of sync with what the machine is actually doing.
///
/// Whether a belt/sink is attached to <see cref="Machine.Output"/> at all is a separate,
/// independent fact (<c>Machine.IsOutputConnected</c>), not folded into this enum: a
/// machine can be <see cref="OutputBlocked"/> whether or not anything is even plugged in.
/// </summary>
public enum MachineStatus
{
    /// <summary>Not producing, not blocked, not missing input — ready to start on the next tick.</summary>
    Idle,

    /// <summary>Not producing because at least one input slot doesn't have enough items yet.</summary>
    WaitingForInput,

    /// <summary>A cycle is in progress; see <c>Machine.ProgressTicks</c>.</summary>
    Producing,

    /// <summary>
    /// Has enough input to start a new cycle, but the output buffer has no room for the
    /// result yet (full itself, or nothing is pulling from it). The machine holds its
    /// inputs unconsumed and waits rather than starting and losing the output.
    /// </summary>
    OutputBlocked,
}
