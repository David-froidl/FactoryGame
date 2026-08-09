using Godot;

namespace FactoryGame.World;

/// <summary>The four ore types placed in this phase. Not tied to the future item registry yet.</summary>
public enum OreType
{
    Iron,
    Copper,
    Limestone,
    Coal,
}

/// <summary>
/// Satisfactory-style extraction multiplier tiers. Stored as data only in this phase — no
/// miner/extractor exists yet to read it, but the value has to live somewhere once nodes
/// are placed, and it belongs on the node, not invented later per-instance.
/// </summary>
public enum OrePurity
{
    Impure,
    Normal,
    Pure,
}

/// <summary>
/// Data for one ore deposit: what it is and how rich it is. Deliberately not a scene or a
/// node — placement (position) is a property of the <see cref="OreNodeMarker"/> instance
/// that references this resource, not of the resource itself, so the same deposit type
/// could be reused across multiple nodes if needed.
/// </summary>
[GlobalClass]
public partial class OreNodeResource : Resource
{
    [Export] public OreType OreType { get; set; } = OreType.Iron;
    [Export] public OrePurity Purity { get; set; } = OrePurity.Normal;
}
