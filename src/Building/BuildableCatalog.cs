using Factory.Sim.Production;
using FactoryGame.World;
using Godot;

namespace FactoryGame.Building;

/// <summary>
/// Loads every <see cref="BuildableResource"/> under <see cref="BuildablesFolder"/> once at
/// startup and filters to what's currently placeable given the scene's <see cref="UnlockState"/>
/// — new buildable data files need no code change here, just a new .tres in that folder.
/// </summary>
public partial class BuildableCatalog : Node
{
    [Export] public string BuildablesFolder = "res://data/buildables";

    private readonly List<BuildableResource> _all = new();

    public IReadOnlyList<BuildableResource> All => _all;

    public override void _Ready()
    {
        DirAccess? dir = DirAccess.Open(BuildablesFolder);
        if (dir is null)
        {
            GD.PushWarning($"{Name}: could not open buildables folder '{BuildablesFolder}'.");
            return;
        }

        dir.ListDirBegin();
        for (string fileName = dir.GetNext(); fileName != ""; fileName = dir.GetNext())
        {
            if (dir.CurrentIsDir() || !fileName.EndsWith(".tres")) continue;

            var resource = GD.Load<BuildableResource>($"{BuildablesFolder}/{fileName}");
            if (resource is not null) _all.Add(resource);
            else GD.PushWarning($"{Name}: '{fileName}' did not load as a BuildableResource.");
        }
        dir.ListDirEnd();
    }

    public IEnumerable<BuildableResource> Placeable(UnlockState unlockState)
        => _all.Where(b => string.IsNullOrEmpty(b.RequiredUnlockId) || unlockState.IsUnlocked(b.RequiredUnlockId));

    public BuildableResource? FindById(string id) => _all.FirstOrDefault(b => b.Id == id);
}
