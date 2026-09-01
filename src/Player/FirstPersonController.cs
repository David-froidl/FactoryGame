using Godot;

namespace FactoryGame.Player;

/// <summary>
/// Ego-perspective movement: WASD walk/strafe, mouse look, jump, sprint, gravity, via
/// <see cref="CharacterBody3D.MoveAndSlide"/>. Reads raw key/mouse state
/// (<see cref="Input.IsPhysicalKeyPressed"/>) instead of Godot's Input Map action system —
/// custom input actions live in <c>project.godot</c>, which this project treats as build
/// configuration requiring separate sign-off, so this deliberately needs zero project
/// settings changes to work.
///
/// Expects two children: a "Head" <see cref="Node3D"/> (pitch pivot, mouse-look up/down)
/// holding a "Camera3D" (see <c>scenes/player/player.tscn</c>). Yaw rotates this node
/// directly; only pitch is separated onto Head, the standard FPS rig split.
///
/// Owns none of the mouse-capture/Escape logic itself — <c>PauseMenuUI</c> (Phase 7) is the
/// single place that toggles <see cref="Input.MouseMode"/> and this controller's
/// <see cref="Enabled"/> flag, so the two systems can't fight over the same key/state.
/// </summary>
public partial class FirstPersonController : CharacterBody3D
{
    [Export] public float WalkSpeed = 5.0f;
    [Export] public float SprintSpeed = 8.5f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float MouseSensitivity = 0.0025f;
    [Export] public float Gravity = 9.8f;

    /// <summary>When false (set by PauseMenuUI while paused), movement and mouse look are ignored.</summary>
    public bool Enabled { get; set; } = true;

    private Node3D _head = null!;
    private float _pitch;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled) return;

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-motion.Relative.X * MouseSensitivity);

            const float pitchLimit = Mathf.Pi / 2f - 0.05f;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -pitchLimit, pitchLimit);
            _head.Rotation = new Vector3(_pitch, 0, 0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Enabled)
        {
            Velocity = Vector3.Zero;
            return;
        }

        Vector3 velocity = Velocity;
        float dt = (float)delta;

        if (!IsOnFloor()) velocity.Y -= Gravity * dt;
        else if (Input.IsPhysicalKeyPressed(Key.Space)) velocity.Y = JumpVelocity;

        Vector2 inputDir = new(
            (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f),
            (Input.IsPhysicalKeyPressed(Key.S) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.W) ? 1f : 0f));
        inputDir = inputDir.Normalized();

        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? SprintSpeed : WalkSpeed;

        if (direction.LengthSquared() > 0.0001f)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, speed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
