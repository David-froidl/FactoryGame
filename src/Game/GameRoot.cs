using Factory.Sim.Belts;
using Factory.Sim.Data;
using Factory.Sim.Production;
using Godot;

namespace FactoryGame.Game;

/// <summary>
/// Scene-level owner of everything a playable scene's buildings share: the one
/// <see cref="BeltNetwork"/>, the loaded item/recipe <see cref="Data"/>, and the session's
/// <see cref="UnlockState"/>. Ticks the network once per physics frame at 20 Hz — the only
/// place the simulation advances — and, right before/after that tick, drives every
/// registered visual's render-snapshot capture in a guaranteed order.
///
/// That ordering is the reason the tick-driving half of this exists: <c>BeltVisual3D</c>
/// needs a "positions before this tick" and "positions after this tick" snapshot to
/// interpolate between in <c>_Process</c>. Godot does not guarantee what order sibling
/// nodes' own <c>_PhysicsProcess</c> callbacks run in, so letting each visual snapshot
/// itself independently could race the network tick. Pushing both snapshot calls from here,
/// wrapped tightly around the one <see cref="BeltNetwork.Tick"/> call, removes that race.
///
/// <see cref="Data"/> is loaded once, here, from OS paths resolved via
/// <see cref="ProjectSettings.GlobalizePath(string)"/> — this is the one place a res:// path
/// crosses into <see cref="GameDataLoader"/>, which itself stays Godot-free.
///
/// Found by visuals via the "game_root" group (see <see cref="_Ready"/>) rather than a static
/// singleton, so a scene reload can't leave anything holding a stale reference.
/// </summary>
public partial class GameRoot : Node
{
    [Export] public string ItemsDataPath = "res://data/items/items.json";
    [Export] public string RecipesDataPath = "res://data/recipes/recipes.json";

    private readonly BeltNetwork _network = new();
    private readonly List<ITickSnapshotConsumer> _consumers = new();

    /// <summary>The one simulation network for this scene. Belts/machines register into it and connect to each other through it.</summary>
    public BeltNetwork Network => _network;

    /// <summary>The loaded, validated item/recipe data for this scene. Available from <see cref="_Ready"/> onward.</summary>
    public GameData Data { get; private set; } = null!;

    /// <summary>Shared, session-only unlock tracking for every <see cref="MilestoneTerminal"/> in this scene.</summary>
    public UnlockState UnlockState { get; } = new();

    public override void _Ready()
    {
        AddToGroup("game_root");
        Data = GameDataLoader.LoadGameData(
            ProjectSettings.GlobalizePath(ItemsDataPath),
            ProjectSettings.GlobalizePath(RecipesDataPath));
    }

    public void RegisterConsumer(ITickSnapshotConsumer consumer) => _consumers.Add(consumer);

    public void UnregisterConsumer(ITickSnapshotConsumer consumer) => _consumers.Remove(consumer);

    public override void _PhysicsProcess(double delta)
    {
        for (int i = 0; i < _consumers.Count; i++) _consumers[i].BeforeNetworkTick();
        _network.Tick();
        for (int i = 0; i < _consumers.Count; i++) _consumers[i].AfterNetworkTick();
    }
}

/// <summary>
/// Implemented by any render visual that needs a "before this tick" / "after this tick"
/// snapshot pair to interpolate from — see <see cref="GameRoot"/> for why these are called
/// from here instead of from each visual's own <c>_PhysicsProcess</c>.
/// </summary>
public interface ITickSnapshotConsumer
{
    void BeforeNetworkTick();
    void AfterNetworkTick();
}
