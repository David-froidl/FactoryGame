using FactoryGame.World;
using Godot;

namespace FactoryGame.Building;

/// <summary>
/// The translucent ghost shown while a buildable is selected in the build menu: follows the
/// aim point <see cref="BuildSystem"/> feeds it, rotates with Q/E, and recolors green/red
/// based on whether the current pose would be a valid placement — <see cref="BuildSystem"/>
/// owns the actual <see cref="PlacementValidator"/> call and just reports the verdict here
/// via <see cref="SetValid"/>; this class only displays it.
///
/// No custom constructor on purpose (Godot node instantiation prefers a parameterless one):
/// call <see cref="Initialize"/> right after <c>new BuildPreviewController()</c>, before
/// adding it to the tree.
/// </summary>
public partial class BuildPreviewController : Node3D
{
    private static readonly Color ValidColor = new(0.2f, 1f, 0.3f, 0.5f);
    private static readonly Color InvalidColor = new(1f, 0.2f, 0.2f, 0.5f);

    private MeshInstance3D _mesh = null!;
    private StandardMaterial3D _material = null!;
    private float _yaw;

    public BuildableResource Buildable { get; private set; } = null!;

    /// <summary>Current preview yaw, in radians — <see cref="BuildSystem"/> reads this to orient the placed instance.</summary>
    public float YawRadians => _yaw;

    public void Initialize(BuildableResource buildable) => Buildable = buildable;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = InvalidColor,
        };
        _mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Buildable.FootprintSize },
            MaterialOverride = _material,
            Position = new Vector3(0, Buildable.FootprintSize.Y / 2f, 0),
        };
        AddChild(_mesh);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key) return;

        if (key.Keycode == Key.Q) Spin(Mathf.Pi / 2f);
        else if (key.Keycode == Key.E) Spin(-Mathf.Pi / 2f);
    }

    public void UpdatePosition(Vector3 worldPosition) => GlobalPosition = worldPosition;

    public void SetValid(bool valid) => _material.AlbedoColor = valid ? ValidColor : InvalidColor;

    private void Spin(float deltaRadians)
    {
        _yaw = Mathf.Wrap(_yaw + deltaRadians, 0f, Mathf.Tau);
        Rotation = new Vector3(0, _yaw, 0);
    }
}
