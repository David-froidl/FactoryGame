namespace FactoryGame.World;

/// <summary>
/// Scene-tree group names used to find placed buildings and ore nodes without a physics
/// query (see <c>PlacementValidator</c>) — centralised here so <c>Render</c> (which adds
/// nodes to these groups) and <c>Building</c> (which searches them) share one spelling.
/// </summary>
public static class BuildingGroups
{
    public const string OreNodes = "ore_nodes";
    public const string PlacedBuildings = "placed_buildings";
    public const string PlacedBelts = "placed_belts";
}
