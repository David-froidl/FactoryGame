using Godot;

namespace FactoryGame.World;

/// <summary>
/// One connection point on a <see cref="BuildableResource"/>: where a belt may snap to it
/// (Phase 6), which way items flow there, and which single item type it accepts. An empty
/// <see cref="ItemFilterKey"/> means "any item" — belt-to-belt ports use that; a machine's
/// input port names the one item its recipe slot requires.
/// </summary>
[GlobalClass]
public partial class PortDefinition : Resource
{
    /// <summary>Position of this port relative to the buildable's origin, in metres.</summary>
    [Export] public Vector3 LocalOffset;

    /// <summary>Which way items flow through this port, relative to the buildable's own facing.</summary>
    [Export] public Vector3 LocalDirection = Vector3.Forward;

    [Export] public PortDirection Direction = PortDirection.Input;

    /// <summary>Item key (from items.json) this port accepts, or empty for "any item".</summary>
    [Export] public string ItemFilterKey = "";
}
