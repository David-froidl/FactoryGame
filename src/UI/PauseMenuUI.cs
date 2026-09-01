using FactoryGame.Player;
using Godot;

namespace FactoryGame.UI;

/// <summary>
/// Escape toggles this: pauses the whole scene tree (<see cref="SceneTree.Paused"/>, which
/// freezes <c>GameRoot</c>'s simulation tick along with everything else by default) and
/// shows a simple modal with Resume/Restart.
///
/// The single owner of <see cref="Input.MouseMode"/> toggling and
/// <see cref="FirstPersonController.Enabled"/> — see that class's own doc comment for why
/// that ownership isn't split between the two.
/// </summary>
public partial class PauseMenuUI : CanvasLayer
{
    [Export] public NodePath PlayerControllerPath = new();

    private FirstPersonController _player = null!;
    private Control _panel = null!;
    private bool _isPaused;

    public override void _Ready()
    {
        _player = GetNode<FirstPersonController>(PlayerControllerPath);
        ProcessMode = ProcessModeEnum.Always; // keep receiving input while the tree is paused

        BuildPanel();
        SetPaused(false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            SetPaused(!_isPaused);
    }

    private void BuildPanel()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel = panel;

        var box = new VBoxContainer();
        box.AddChild(new Label { Text = "Pausiert", HorizontalAlignment = HorizontalAlignment.Center });

        var resumeButton = new Button { Text = "Weiter" };
        resumeButton.Pressed += () => SetPaused(false);
        box.AddChild(resumeButton);

        var restartButton = new Button { Text = "Neustart" };
        restartButton.Pressed += Restart;
        box.AddChild(restartButton);

        panel.AddChild(box);
        AddChild(panel);
    }

    private void SetPaused(bool paused)
    {
        _isPaused = paused;
        _panel.Visible = paused;
        _player.Enabled = !paused;
        Input.MouseMode = paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        GetTree().Paused = paused;
    }

    private void Restart()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
