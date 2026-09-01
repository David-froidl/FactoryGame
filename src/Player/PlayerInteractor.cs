using Godot;

namespace FactoryGame.Player;

/// <summary>
/// Casts a ray from the camera, once per physics frame, to find what the player is looking
/// at within <see cref="InteractionDistance"/>. This only reports the target — Phase 7's UI
/// reads <see cref="CurrentTarget"/> for a context prompt, and Phase 6's build system will
/// use <see cref="CurrentHitPoint"/> to place buildings. Nothing here acts on the target.
/// </summary>
public partial class PlayerInteractor : Node
{
    [Export] public NodePath CameraPath = new();
    [Export] public float InteractionDistance = 4.0f;

    /// <summary>Physics layers this ray tests against. Default (1) is Godot's default collision layer.</summary>
    [Export] public uint CollisionMask = 1;

    private Camera3D _camera = null!;

    public bool HasTarget { get; private set; }
    public Node3D? CurrentTarget { get; private set; }
    public Vector3 CurrentHitPoint { get; private set; }
    public Vector3 CurrentHitNormal { get; private set; }

    public override void _Ready() => _camera = GetNode<Camera3D>(CameraPath);

    public override void _PhysicsProcess(double delta)
    {
        Vector3 from = _camera.GlobalPosition;
        Vector3 forward = -_camera.GlobalTransform.Basis.Z;
        Vector3 to = from + forward * InteractionDistance;

        var query = PhysicsRayQueryParameters3D.Create(from, to, CollisionMask);
        Godot.Collections.Dictionary result = _camera.GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            HasTarget = true;
            CurrentTarget = result["collider"].As<Node3D>();
            CurrentHitPoint = result["position"].As<Vector3>();
            CurrentHitNormal = result["normal"].As<Vector3>();
        }
        else
        {
            HasTarget = false;
            CurrentTarget = null;
        }
    }
}
