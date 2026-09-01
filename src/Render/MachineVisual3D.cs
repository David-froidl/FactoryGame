using Factory.Sim.Production;
using FactoryGame.Game;
using FactoryGame.World;
using Godot;

namespace FactoryGame.Render;

/// <summary>
/// Godot-facing wrapper around one <see cref="Machine"/>: builds it from a
/// <see cref="BuildableResource"/> plus the scene's loaded recipe data, registers it into
/// the shared <see cref="GameRoot"/> network, and renders a placeholder box plus a live
/// status label. One visual class covers extractor, smelter and assembler alike — same
/// reason <see cref="Machine"/> itself is one class: they differ only in which
/// <see cref="BuildableResource"/> (and therefore which recipe) is assigned.
///
/// Placement (position, rotation, port wiring, ore-node validity) is not this class's job —
/// that is Phase 6's <c>BuildSystem</c>. This only constructs and displays the sim object
/// once something else has already decided a placement is valid and set <see cref="Buildable"/>.
/// </summary>
public partial class MachineVisual3D : Node3D, IPlaceable
{
    [Export] public BuildableResource? Buildable;

    /// <summary>The underlying sim machine. Null until a valid <see cref="Buildable"/> has been assigned and _Ready has run.</summary>
    public Machine? Sim { get; private set; }

    public Vector3 FootprintSize => Buildable?.FootprintSize ?? Vector3.Zero;

    private Label3D _statusLabel = null!;

    public override void _Ready()
    {
        if (Buildable is null)
        {
            GD.PushWarning($"{Name}: MachineVisual3D has no Buildable resource assigned.");
            return;
        }

        var gameRoot = GetTree().GetFirstNodeInGroup("game_root") as GameRoot;
        if (gameRoot is null)
        {
            GD.PushWarning($"{Name}: no GameRoot in this scene; this machine will never tick.");
            return;
        }

        RecipeDefinition recipe = gameRoot.Data.Recipes.Get(Buildable.RecipeKey);
        Sim = new Machine(recipe, Buildable.InputCapacityPerSlot, Buildable.OutputCapacity);
        gameRoot.Network.Add(Sim);
        AddToGroup(BuildingGroups.PlacedBuildings);

        AddChild(BuildMesh());
        _statusLabel = BuildStatusLabel();
        AddChild(_statusLabel);
    }

    public override void _Process(double delta)
    {
        if (Sim is null) return;
        _statusLabel.Text = $"{Buildable!.DisplayName}\n{Sim.Status}\n{Sim.OutputCount}/{Sim.OutputCapacity} out";
    }

    private MeshInstance3D BuildMesh() => new()
    {
        Mesh = new BoxMesh
        {
            Size = Buildable!.FootprintSize,
            Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel, AlbedoColor = Buildable.PlaceholderColor },
        },
        Position = new Vector3(0, Buildable.FootprintSize.Y / 2f, 0),
    };

    private Label3D BuildStatusLabel() => new()
    {
        Position = new Vector3(0, Buildable!.FootprintSize.Y + 0.5f, 0),
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FontSize = 32,
        OutlineSize = 6,
    };
}
