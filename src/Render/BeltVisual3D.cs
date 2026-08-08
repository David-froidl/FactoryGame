using Factory.Sim;
using Factory.Sim.Belts;
using Factory.Sim.Items;
using Godot;

namespace FactoryGame.Render;

/// <summary>
/// Renders one <see cref="BeltSegment"/> through a single MultiMesh instance — no item is
/// ever a Node. This is the only place simulation state (fixed-point units, integer ticks)
/// crosses into Godot state (metres, floats, frames), so the unit conversion and the
/// tick/frame decoupling both live here and nowhere else.
///
/// Simulation and rendering run at different rates on purpose: <see cref="_PhysicsProcess"/>
/// only fires at the project's fixed physics rate (20 Hz, set in project.godot) and is the
/// only place the sim advances. <see cref="_Process"/> fires once per rendered frame and
/// only interpolates positions — it never touches sim state — so visuals stay smooth at
/// 144 Hz while the sim itself stays perfectly reproducible at 20 Hz.
/// </summary>
public partial class BeltVisual3D : Node3D
{
    /// <summary>Belt length, in world tiles.</summary>
    [Export] public int LengthInTiles = 10;

    /// <summary>Belt rate, in items per minute. Must be one of <see cref="BeltTiers"/>.</summary>
    [Export] public int ItemsPerMinute = BeltTiers.Mk3;

    /// <summary>Visual size of one item cube, in metres.</summary>
    [Export] public Vector3 ItemSize = new(0.35f, 0.2f, 0.35f);

    private static readonly ItemId DemoItem = new(1);

    private BeltSegment _belt = null!;
    private BeltNetwork _network = null!;
    private ItemVoid _sink = null!;

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
        _belt = new BeltSegment(LengthInTiles * SimConstants.UnitsPerTile, speed);
        _sink = new ItemVoid();
        _network = new BeltNetwork();
        _network.Connect(_belt, _sink);

        _prevPositions = new int[_belt.Capacity];
        _curPositions = new int[_belt.Capacity];
        _curItems = new ItemId[_belt.Capacity];

        BuildVisual();
    }

    private void BuildVisual()
    {
        var mesh = new BoxMesh { Size = ItemSize };
        _multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = _belt.Capacity,
            VisibleInstanceCount = 0,
        };

        var multiMeshInstance = new MultiMeshInstance3D { Multimesh = _multiMesh };
        AddChild(multiMeshInstance);

        // Placeholder belt surface. A real belt mesh/shader is a Phase 1 concern.
        float lengthMetres = MetresFromUnits(_belt.Length);
        var beltInstance = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.05f, lengthMetres) },
            Position = new Vector3(0, -0.15f, lengthMetres / 2f),
        };
        AddChild(beltInstance);
    }

    public override void _PhysicsProcess(double delta)
    {
        System.Array.Copy(_curPositions, _prevPositions, _curCount);
        _prevCount = _curCount;
        _prevTotalPopped = _belt.TotalPopped;

        // Keep the belt saturated so motion is continuously visible in the demo.
        _belt.TryAccept(DemoItem);
        _network.Tick();

        _curCount = _belt.CopyTo(_curPositions, _curItems);
        _tickProgress = 0.0;
    }

    public override void _Process(double delta)
    {
        _tickProgress += delta * SimConstants.TicksPerSecond;
        float t = (float)Mathf.Clamp(_tickProgress, 0.0, 1.0);

        // A belt pops at most one item per tick (from the front) and this demo inserts at
        // most one per tick (at the rear), so this is always 0 or 1.
        int poppedThisTick = (int)(_belt.TotalPopped - _prevTotalPopped);
        int carriedOver = _prevCount - poppedThisTick;

        _multiMesh.VisibleInstanceCount = _curCount;
        float lengthMetres = MetresFromUnits(_belt.Length);

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
