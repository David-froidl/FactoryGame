using Godot;

namespace FactoryGame.World;

/// <summary>
/// Implemented by any visual the build system (Phase 6) can place and must check for
/// overlap against — currently <c>MachineVisual3D</c> and <c>TerminalVisual3D</c>. Belts
/// deliberately don't implement this: their footprint is a line, not a box, and this
/// vertical slice's placement rules only need box/box overlap (see <c>PlacementValidator</c>).
/// </summary>
public interface IPlaceable
{
    Vector3 FootprintSize { get; }
}
