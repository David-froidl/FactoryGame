using FactoryGame.Building;
using FactoryGame.Game;
using FactoryGame.Player;
using FactoryGame.Render;
using FactoryGame.World;
using Godot;

namespace FactoryGame.UI;

/// <summary>
/// The always-on gameplay HUD: crosshair, interaction/inspector prompt, build menu,
/// placement feedback, terminal progress, and a one-shot unlock toast. Builds its own
/// Control tree procedurally in <see cref="_Ready"/> — the same "build it in code" pattern
/// <c>MachineVisual3D</c> and <c>BeltVisual3D</c> already use — rather than a hand-authored
/// <c>.tscn</c> layout this session has no way to visually check.
/// </summary>
public partial class HudRoot : CanvasLayer
{
    [Export] public NodePath BuildSystemPath = new();
    [Export] public NodePath PlayerInteractorPath = new();

    private BuildSystem _buildSystem = null!;
    private PlayerInteractor _interactor = null!;
    private GameRoot? _gameRoot;

    private Label _interactionLabel = null!;
    private VBoxContainer _buildMenu = null!;
    private Label _selectedLabel = null!;
    private Label _errorLabel = null!;
    private Label _unlockToast = null!;

    private double _toastTimer;

    public override void _Ready()
    {
        _buildSystem = GetNode<BuildSystem>(BuildSystemPath);
        _interactor = GetNode<PlayerInteractor>(PlayerInteractorPath);
        _gameRoot = GetTree().GetFirstNodeInGroup("game_root") as GameRoot;

        BuildCrosshair();
        BuildInteractionLabel();
        BuildBuildMenu();
        BuildFeedbackLabels();
        BuildUnlockToast();

        RefreshBuildMenu();
        if (_gameRoot is not null) _gameRoot.UnlockState.Unlocked += OnUnlocked;
    }

    public override void _Process(double delta)
    {
        UpdateInteractionAndInspector();
        UpdateSelectionAndErrorLabels();
        UpdateToast(delta);
    }

    // ---- Crosshair ----

    private void BuildCrosshair()
    {
        var crosshair = new Label
        {
            Text = "+",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        crosshair.Position = new Vector2(-8, -12);
        AddChild(crosshair);
    }

    // ---- Interaction prompt + machine/terminal inspector ----

    private void BuildInteractionLabel()
    {
        _interactionLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _interactionLabel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _interactionLabel.Position = new Vector2(-200, -140);
        _interactionLabel.Size = new Vector2(400, 100);
        AddChild(_interactionLabel);
    }

    private void UpdateInteractionAndInspector()
    {
        if (!_interactor.HasTarget || _interactor.CurrentTarget is null)
        {
            _interactionLabel.Text = "";
            return;
        }

        if (FindAncestor<MachineVisual3D>(_interactor.CurrentTarget) is { Sim: { } machine } machineVisual)
        {
            _interactionLabel.Text = $"{machineVisual.Buildable?.DisplayName}\n" +
                $"Status: {machine.Status}\n" +
                $"Ausgang: {machine.OutputCount}/{machine.OutputCapacity}\n" +
                $"Fortschritt: {machine.ProgressTicks}/{machine.Recipe.DurationTicks} Ticks\n" +
                $"Verbunden: {(machine.IsOutputConnected ? "ja" : "nein")}";
            return;
        }

        if (FindAncestor<TerminalVisual3D>(_interactor.CurrentTarget) is { Sim: { } terminal } terminalVisual)
        {
            _interactionLabel.Text = $"{terminalVisual.Buildable?.DisplayName}\n" +
                $"{terminal.DeliveredCount}/{terminal.Definition.RequiredCount}" +
                (terminal.IsThresholdMet ? "\nFREIGESCHALTET" : "");
            return;
        }

        _interactionLabel.Text = "";
    }

    private static T? FindAncestor<T>(Node node) where T : class
    {
        Node? current = node;
        while (current is not null)
        {
            if (current is T match) return match;
            current = current.GetParent();
        }
        return null;
    }

    // ---- Build menu ----

    private void BuildBuildMenu()
    {
        _buildMenu = new VBoxContainer();
        _buildMenu.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _buildMenu.Position = new Vector2(20, -150);
        AddChild(_buildMenu);
    }

    private void RefreshBuildMenu()
    {
        foreach (Node child in _buildMenu.GetChildren()) child.QueueFree();
        if (_gameRoot is null) return;

        foreach (BuildableResource buildable in _buildSystem.Catalog.Placeable(_gameRoot.UnlockState))
        {
            var button = new Button { Text = buildable.DisplayName };
            button.Pressed += () => _buildSystem.SelectBuildable(buildable);
            _buildMenu.AddChild(button);
        }
    }

    private void OnUnlocked(string unlockId)
    {
        RefreshBuildMenu();
        _unlockToast.Text = $"Freigeschaltet: {unlockId}";
        _toastTimer = 4.0;
    }

    // ---- Selected buildable / placement error ----

    private void BuildFeedbackLabels()
    {
        _selectedLabel = new Label();
        _selectedLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _selectedLabel.Position = new Vector2(20, 20);
        AddChild(_selectedLabel);

        _errorLabel = new Label { Modulate = new Color(1f, 0.3f, 0.3f) };
        _errorLabel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _errorLabel.Position = new Vector2(-200, -170);
        _errorLabel.Size = new Vector2(400, 30);
        _errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_errorLabel);
    }

    private void UpdateSelectionAndErrorLabels()
    {
        BuildableResource? selected = _buildSystem.SelectedBuildable;
        _selectedLabel.Text = selected is null ? "" : $"Bauen: {selected.DisplayName} (Q/E drehen, RMT abbrechen)";

        PlacementResult result = _buildSystem.CurrentPlacementResult;
        _errorLabel.Text = selected is not null && !result.IsValid ? result.Reason : "";
    }

    // ---- Unlock toast ----

    private void BuildUnlockToast()
    {
        _unlockToast = new Label { Modulate = new Color(1f, 0.85f, 0.2f), HorizontalAlignment = HorizontalAlignment.Center };
        _unlockToast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _unlockToast.Position = new Vector2(-200, 20);
        _unlockToast.Size = new Vector2(400, 30);
        AddChild(_unlockToast);
    }

    private void UpdateToast(double delta)
    {
        if (_toastTimer <= 0) return;
        _toastTimer -= delta;
        if (_toastTimer <= 0) _unlockToast.Text = "";
    }
}
