using Factory.Sim.Items;
using Factory.Sim.Production;
using FactoryGame.Game;
using FactoryGame.World;
using Godot;

namespace FactoryGame.Render;

/// <summary>
/// Godot-facing wrapper around one <see cref="MilestoneTerminal"/>: builds its definition
/// from a <see cref="BuildableResource"/> (category <see cref="BuildableCategory.Terminal"/>)
/// plus the scene's loaded item data, registers it into the shared <see cref="GameRoot"/>
/// network, and renders a placeholder box plus a live "delivered/required" status label.
/// </summary>
public partial class TerminalVisual3D : Node3D
{
    [Export] public BuildableResource? Buildable;

    /// <summary>The underlying sim terminal. Null until a valid <see cref="Buildable"/> has been assigned and _Ready has run.</summary>
    public MilestoneTerminal? Sim { get; private set; }

    private Label3D _statusLabel = null!;

    public override void _Ready()
    {
        if (Buildable is null)
        {
            GD.PushWarning($"{Name}: TerminalVisual3D has no Buildable resource assigned.");
            return;
        }

        var gameRoot = GetTree().GetFirstNodeInGroup("game_root") as GameRoot;
        if (gameRoot is null)
        {
            GD.PushWarning($"{Name}: no GameRoot in this scene; this terminal will never receive deliveries.");
            return;
        }

        ItemId requiredItem = gameRoot.Data.Items.Get(Buildable.MilestoneRequiredItemKey).Id;
        var definition = new MilestoneDefinition(Buildable.Id, requiredItem, Buildable.MilestoneRequiredCount, Buildable.MilestoneUnlockId);
        Sim = new MilestoneTerminal(definition, gameRoot.UnlockState);
        gameRoot.Network.Add(Sim);

        AddChild(BuildMesh());
        _statusLabel = BuildStatusLabel();
        AddChild(_statusLabel);
    }

    public override void _Process(double delta)
    {
        if (Sim is null) return;
        string unlocked = Sim.IsThresholdMet ? "\nUNLOCKED" : "";
        _statusLabel.Text = $"{Buildable!.DisplayName}\n{Sim.DeliveredCount}/{Sim.Definition.RequiredCount}{unlocked}";
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
