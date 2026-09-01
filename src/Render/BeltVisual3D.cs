using Factory.Sim;
using Factory.Sim.Belts;
using Factory.Sim.Items;
using FactoryGame.Game;
using FactoryGame.World;
using Godot;

namespace FactoryGame.Render;

/// <summary>
/// Renders one <see cref="BeltSegment"/> through a single MultiMesh instance — no item is
/// ever a Node. This is the only place simulation state (fixed-point units, integer ticks)
/// crosses into Godot state (metres, floats, frames), so the unit conversion and the
/// tick/frame decoupling both live here and nowhere else.
///
/// Simulation and rendering run at different rates on purpose: the sim only advances once
/// per physics tick (20 Hz, set in project.godot); <see cref="_Process"/> fires once per
/// rendered frame and only interpolates positions — it never touches sim state — so visuals
/// stay smooth at 144 Hz while the sim itself stays perfectly reproducible at 20 Hz.
///
/// Two ticking modes, chosen by <see cref="StandaloneDemoMode"/>:
/// <list type="bullet">
/// <item><b>Registered (default, every real placed belt from Phase 6 on):</b> this belt is
/// added to the scene's shared <see cref="GameRoot"/>.<see cref="GameRoot.Network"/> and
/// never feeds itself — items only arrive by another node connecting into <see cref="Belt"/>,
/// exactly like a player-placed belt must. <see cref="GameRoot"/> ticks the shared network
/// and calls <see cref="BeforeNetworkTick"/>/<see cref="AfterNetworkTick"/> on every belt in
/// a guaranteed order (see <see cref="GameRoot"/> for why that ordering matters).</item>
/// <item><b>Standalone (<c>scenes/belt_demo</c> only):</b> this belt owns a private network
/// and sink and saturates itself with a demo item every tick, exactly as before this class
/// supported a shared network at all. A demo scene has no <see cref="GameRoot"/> and needs
/// none — self-feeding is demo scaffolding that must never leak into a real, player-placed
/// belt, which is why it isn't the default.</item>
/// </list>
/// </summary>
public partial class BeltVisual3D : Node3D, ITickSnapshotConsumer
{
    /// <summary>Belt length, in world tiles.</summary>
    [Export] public int LengthInTiles = 10;

    /// <summary>Belt rate, in items per minute. Must be one of <see cref="BeltTiers"/>.</summary>
    [Export] public int ItemsPerMinute = BeltTiers.Mk3;

    /// <summary>
    /// Visual size of one item cube, in metres. The Z size should stay under the belt's
    /// item spacing (0.25m at the default <see cref="SimConstants.ItemSpacing"/>) or a
    /// saturated belt renders as one continuous bar instead of visibly separate items.
    /// </summary>
    [Export] public Vector3 ItemSize = new(0.3f, 0.25f, 0.18f);

    /// <summary>
    /// True only for <c>scenes/belt_demo</c>: self-contained, self-feeding, self-ticking.
    /// Every real, player-placed belt must leave this false — see the class summary.
    /// </summary>
    [Export] public bool StandaloneDemoMode;

    private static readonly ItemId DemoItem = new(1);

    /// <summary>The underlying sim belt. Connect other sim nodes to/from this to wire up gameplay.</summary>
    public BeltSegment Belt { get; private set; } = null!;

    private BeltNetwork? _standaloneNetwork; // set only in standalone mode
    private ItemVoid? _standaloneSink;       // set only in standalone mode
    private GameRoot? _gameRoot;             // set only in registered mode, once found

    private MultiMesh _multiMesh = null!;

    // Two snapshots of "distance from the belt's output end", captured every physics tick,
    // so _Process can interpolate between them. Plain int[]/ItemId[] scratch buffers reused
    // every tick — this is the render-side analogue of the sim's own allocation-free design.
    private int[] _prevPositions = System.Array.Empty<int>();
    private int[] _curPositions = System.Array.Empty<int>();
    private ItemId[] _curItems = System.Array.Empty<ItemId>();
    private int _prevCount;
    private int _curCount;
    private long _prevTotalPopped;

    // How far the render clock has drifted past the last physics tick, as a 0..1 fraction
    // of one tick. Self-tracked rather than relying on an engine-provided fraction, so this
    // component has no dependency on a specific Godot minor version's physics-interpolation API.
    private double _tickProgress;

    public override void _Ready()
    {
        int speed = SimConstants.ItemsPerMinuteToSpeed(ItemsPerMinute);
        Belt = new BeltSegment(LengthInTiles * SimConstants.UnitsPerTile, speed);

        _prevPositions = new int[Belt.Capacity];
        _curPositions = new int[Belt.Capacity];
        _curItems = new ItemId[Belt.Capacity];

        if (StandaloneDemoMode)
        {
            _standaloneSink = new ItemVoid();
            _standaloneNetwork = new BeltNetwork();
            _standaloneNetwork.Connect(Belt, _standaloneSink);
        }
        else
        {
            _gameRoot = GetTree().GetFirstNodeInGroup("game_root") as GameRoot;
            if (_gameRoot is null)
            {
                GD.PushWarning($"{Name}: no GameRoot in this scene; this belt will never tick.");
            }
            else
            {
                _gameRoot.Network.Add(Belt);
                _gameRoot.RegisterConsumer(this);
                AddToGroup(BuildingGroups.PlacedBelts);
            }
        }

        BuildVisual();
    }

    public override void _ExitTree() => _gameRoot?.UnregisterConsumer(this);

    private void BuildVisual()
    {
        // Unshaded materials: this demo has no lighting setup to get right, and item
        // visibility shouldn't depend on one. A real belt/item material is a Phase 1 concern.
        var mesh = new BoxMesh
        {
            Size = ItemSize,
            Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.55f, 0.1f) },
        };
        _multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = Belt.Capacity,
            VisibleInstanceCount = 0,
        };

        float lengthMetres = MetresFromUnits(Belt.Length);
        var multiMeshInstance = new MultiMeshInstance3D
        {
            Multimesh = _multiMesh,
            // The auto-computed AABB doesn't account for instances spread along the belt's
            // length until they've actually been placed once, so a explicit box avoids a
            // frustum-culling false negative on the first rendered frames.
            CustomAabb = new Aabb(new Vector3(-1, -1, -1), new Vector3(2, 2, lengthMetres + 2)),
        };
        AddChild(multiMeshInstance);

        // Placeholder belt surface. A real belt mesh/shader is a Phase 1 concern.
        var beltInstance = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(0.6f, 0.05f, lengthMetres),
                Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.3f, 0.3f, 0.32f) },
            },
            Position = new Vector3(0, -0.15f, lengthMetres / 2f),
        };
        AddChild(beltInstance);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!StandaloneDemoMode) return; // registered mode: GameRoot drives the snapshot calls below

        BeforeNetworkTick();
        Belt.TryAccept(DemoItem); // keep the belt saturated so motion is continuously visible in the demo
        _standaloneNetwork!.Tick();
        AfterNetworkTick();
    }

    public void BeforeNetworkTick()
    {
        System.Array.Copy(_curPositions, _prevPositions, _curCount);
        _prevCount = _curCount;
        _prevTotalPopped = Belt.TotalPopped;
    }

    public void AfterNetworkTick()
    {
        _curCount = Belt.CopyTo(_curPositions, _curItems);
        _tickProgress = 0.0;
    }

    public override void _Process(double delta)
    {
        _tickProgress += delta * SimConstants.TicksPerSecond;
        float t = (float)Mathf.Clamp(_tickProgress, 0.0, 1.0);

        // Every pusher in the sim (a belt's head, a machine's output, a splitter) hands over
        // at most one item per tick, so at most one item can have entered this belt's rear
        // since the last snapshot — this is always 0 or 1.
        int poppedThisTick = (int)(Belt.TotalPopped - _prevTotalPopped);
        int carriedOver = _prevCount - poppedThisTick;

        _multiMesh.VisibleInstanceCount = _curCount;
        float lengthMetres = MetresFromUnits(Belt.Length);

        for (int i = 0; i < _curCount; i++)
        {
            float currentMetres = MetresFromUnits(_curPositions[i]);
            float renderMetres;

            if (i < carriedOver)
            {
                // Same physical item as last tick, shifted down by however many popped.
                float prevMetres = MetresFromUnits(_prevPositions[i + poppedThisTick]);
                renderMetres = Mathf.Lerp(prevMetres, currentMetres, t);
            }
            else
            {
                // Entered the belt this tick; nothing to interpolate from yet.
                renderMetres = currentMetres;
            }

            var transform = new Transform3D(Basis.Identity, new Vector3(0, 0, lengthMetres - renderMetres));
            _multiMesh.SetInstanceTransform(i, transform);
        }
    }

    private static float MetresFromUnits(int units) => units / (float)SimConstants.UnitsPerTile;
}
