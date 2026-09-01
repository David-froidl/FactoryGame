using FactoryGame.Game;
using FactoryGame.Render;
using FactoryGame.World;
using Godot;

namespace FactoryGame.Building;

/// <summary>
/// Orchestrates build-menu selection, the placement preview, confirm/cancel, and demolish.
/// Phase 7's UI calls <see cref="SelectBuildable"/> and reads <see cref="CurrentPlacementResult"/>
/// for the error toast and <see cref="Catalog"/> for the menu list; mouse/key handling for
/// confirm/cancel/rotate/demolish lives here since it is gameplay logic, not UI.
///
/// Owns and creates its own <see cref="BuildableCatalog"/>, <see cref="PlacementValidator"/>
/// and <see cref="PortConnector"/> as children in <see cref="_Ready"/>, so dropping this one
/// node into a scene (with a camera and player path wired) is enough — no other setup.
/// </summary>
public partial class BuildSystem : Node
{
    [Export] public NodePath CameraPath = new();
    [Export] public NodePath PlayerPath = new();
    [Export] public NodePath BuildingsContainerPath = new();
    [Export] public float MaxAimDistance = 20f;
    [Export] public uint TerrainCollisionMask = 1;

    private Camera3D _camera = null!;
    private Node3D _player = null!;
    private Node _buildingsContainer = null!;
    private GameRoot _gameRoot = null!;

    public BuildableCatalog Catalog { get; private set; } = null!;
    public PlacementValidator Validator { get; private set; } = null!;

    private PortConnector _portConnector = null!;
    private BuildPreviewController? _preview;

    public BuildableResource? SelectedBuildable => _preview?.Buildable;
    public PlacementResult CurrentPlacementResult { get; private set; } = PlacementResult.Invalid("");

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>(CameraPath);
        _player = GetNode<Node3D>(PlayerPath);
        _buildingsContainer = BuildingsContainerPath.IsEmpty ? this : GetNode(BuildingsContainerPath);

        if (GetTree().GetFirstNodeInGroup("game_root") is not GameRoot gameRoot)
        {
            GD.PushWarning($"{Name}: no GameRoot in this scene; build system disabled.");
            SetPhysicsProcess(false);
            SetProcessUnhandledInput(false);
            return;
        }
        _gameRoot = gameRoot;

        Catalog = new BuildableCatalog();
        AddChild(Catalog);
        Validator = new PlacementValidator();
        AddChild(Validator);
        _portConnector = new PortConnector();
        AddChild(_portConnector);
    }

    /// <summary>Starts (or switches) placement of <paramref name="buildable"/>. Call again with the same one to keep placing more.</summary>
    public void SelectBuildable(BuildableResource buildable)
    {
        _preview?.QueueFree();
        _preview = new BuildPreviewController();
        _preview.Initialize(buildable);
        AddChild(_preview);
    }

    public void CancelPlacing()
    {
        _preview?.QueueFree();
        _preview = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_preview is null) return;

        if (RaycastGround() is Vector3 point)
        {
            _preview.UpdatePosition(point);
            CurrentPlacementResult = Validator.Validate(_preview.Buildable, _preview.GlobalTransform, _player.GlobalPosition);
        }
        else
        {
            CurrentPlacementResult = PlacementResult.Invalid("Kein gültiger Zielpunkt.");
        }

        _preview.SetValid(CurrentPlacementResult.IsValid);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left) TryConfirmPlacement();
            else if (mouseButton.ButtonIndex == MouseButton.Right) CancelPlacing();
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.X })
        {
            TryDemolishLookedAtBuilding();
        }
    }

    private void TryConfirmPlacement()
    {
        if (_preview is null || !CurrentPlacementResult.IsValid) return;

        BuildableResource buildable = _preview.Buildable;
        Transform3D transform = _preview.GlobalTransform;

        Node3D instance = buildable.Category switch
        {
            BuildableCategory.Machine => new MachineVisual3D { Buildable = buildable, Name = buildable.Id },
            BuildableCategory.Terminal => new TerminalVisual3D { Buildable = buildable, Name = buildable.Id },
            BuildableCategory.Belt => new BeltVisual3D
            {
                Name = buildable.Id,
                ItemsPerMinute = buildable.BeltItemsPerMinute,
                LengthInTiles = 1,
                StandaloneDemoMode = false,
            },
            _ => throw new System.InvalidOperationException($"Unknown buildable category {buildable.Category}."),
        };

        _buildingsContainer.AddChild(instance);
        instance.GlobalTransform = transform;

        if (instance is BeltVisual3D beltVisual)
            _portConnector.ConnectBeltEnds(beltVisual, _gameRoot);
    }

    private void TryDemolishLookedAtBuilding()
    {
        // Buildings have no collision shape of their own (see MachineVisual3D/TerminalVisual3D),
        // so this raycasts the terrain for an aim point and finds the nearest placed building
        // to it by proximity — the same approach PlacementValidator uses, for the same reason.
        Vector3? hit = RaycastGround();
        if (hit is null) return;

        Node? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Node node in GetTree().GetNodesInGroup(BuildingGroups.PlacedBuildings))
        {
            if (node is not Node3D placed) continue;
            float distance = placed.GlobalPosition.DistanceTo(hit.Value);
            if (distance < nearestDistance) { nearestDistance = distance; nearest = node; }
        }

        // Demolition removes the visual; the underlying sim node (Machine/MilestoneTerminal)
        // stays registered in GameRoot.Network — it has no BeltNetwork.Remove to call yet.
        // An orphaned node with no input feed and no reachable output is harmless (it just
        // sits idle forever), but this is a known gap, not a fully clean deconstruction.
        if (nearest is not null && nearestDistance <= 2f) nearest.QueueFree();
    }

    private Vector3? RaycastGround()
    {
        Vector3 from = _camera.GlobalPosition;
        Vector3 to = from + (-_camera.GlobalTransform.Basis.Z) * MaxAimDistance;

        var query = PhysicsRayQueryParameters3D.Create(from, to, TerrainCollisionMask);
        Godot.Collections.Dictionary result = _camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return result.Count > 0 ? result["position"].As<Vector3>() : null;
    }
}
