using Factory.Sim;
using Godot;

namespace FactoryGame.World;

/// <summary>
/// Placement + identity data for one buildable type — extractor, smelter, assembler, a
/// belt tier, or the milestone terminal. Purely data, authored as a <c>.tres</c> exactly
/// like <see cref="OreNodeResource"/>; the build menu (Phase 6) lists these, and the
/// visuals (<c>MachineVisual3D</c>, <c>TerminalVisual3D</c>) read them to construct the
/// matching sim object. No placement/validation logic lives here — see <c>Machine</c> rule
/// #4: one generic machine, differing only by which of these resources it was built from.
///
/// Fields are grouped by which <see cref="BuildableCategory"/> actually reads them; a field
/// outside a buildable's own category is simply left at its default and ignored.
/// </summary>
[GlobalClass]
public partial class BuildableResource : Resource
{
    [Export] public string Id = "";
    [Export] public string DisplayName = "";
    [Export] public BuildableCategory Category = BuildableCategory.Machine;
    [Export] public Vector3 FootprintSize = new(2f, 2f, 2f);
    [Export] public Color PlaceholderColor = Colors.White;
    [Export] public Godot.Collections.Array<PortDefinition> Ports = new();

    /// <summary>Non-empty means this buildable can't be placed until this id is in the scene's <c>UnlockState</c>.</summary>
    [Export] public string RequiredUnlockId = "";

    // ---- Category == Machine (extractor, smelter, assembler) ----

    /// <summary>Key into recipes.json.</summary>
    [Export] public string RecipeKey = "";

    [Export] public int InputCapacityPerSlot = 10;
    [Export] public int OutputCapacity = 10;

    /// <summary>True only for extractors: placement additionally requires standing on a matching ore node.</summary>
    [Export] public bool RequiresOreNode;

    [Export] public OreType RequiredOreType = OreType.Iron;

    // ---- Category == Belt ----

    [Export] public int BeltItemsPerMinute = BeltTiers.Mk1;

    // ---- Category == Terminal ----

    /// <summary>Key into items.json for what the terminal accepts.</summary>
    [Export] public string MilestoneRequiredItemKey = "";

    [Export] public int MilestoneRequiredCount = 10;

    /// <summary>Unlock id granted once <see cref="MilestoneRequiredCount"/> deliveries are reached.</summary>
    [Export] public string MilestoneUnlockId = "";
}
