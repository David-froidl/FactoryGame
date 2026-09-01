using FactoryGame.World;
using Godot;

namespace FactoryGame.Building;

/// <summary>
/// Pure placement rules: world bounds, distance to the player, footprint overlap against
/// already-placed buildings, and ore-node matching for extractors.
///
/// Deliberately uses simple AABB/radius checks against the <see cref="BuildingGroups"/>
/// scene groups rather than physics shape/ray queries — placement validity then doesn't
/// depend on collision-layer setup this session has no way to visually verify. Good enough
/// for this vertical slice's flat, static world; a precise physics-based version is a
/// reasonable later upgrade once someone can playtest the collision layers interactively.
/// Belt-vs-belt and belt-vs-machine overlap is out of scope here — see
/// <see cref="IPlaceable"/> for why belts don't participate in the overlap check at all.
/// </summary>
public partial class PlacementValidator : Node
{
    [Export] public float WorldHalfSize = 250f;
    [Export] public float MaxDistanceToPlayer = 8f;
    [Export] public float OreNodeMatchRadius = 4.5f;

    public PlacementResult Validate(BuildableResource buildable, Transform3D candidate, Vector3 playerPosition)
    {
        Vector3 pos = candidate.Origin;

        if (Mathf.Abs(pos.X) > WorldHalfSize || Mathf.Abs(pos.Z) > WorldHalfSize)
            return PlacementResult.Invalid("Außerhalb der Welt.");

        if (pos.DistanceTo(playerPosition) > MaxDistanceToPlayer)
            return PlacementResult.Invalid("Zu weit entfernt.");

        if (OverlapsPlacedBuilding(pos, buildable.FootprintSize))
            return PlacementResult.Invalid("Kollidiert mit vorhandenem Gebäude.");

        if (buildable.RequiresOreNode && FindMatchingOreNode(pos, buildable) is null)
            return PlacementResult.Invalid($"Muss auf einem passenden Rohstoffvorkommen stehen ({buildable.RequiredOreType}).");

        return PlacementResult.Valid();
    }

    public OreNodeMarker? FindMatchingOreNode(Vector3 position, BuildableResource buildable)
    {
        foreach (Node node in GetTree().GetNodesInGroup(BuildingGroups.OreNodes))
        {
            if (node is OreNodeMarker marker
                && marker.Data is not null
                && marker.Data.OreType == buildable.RequiredOreType
                && marker.GlobalPosition.DistanceTo(position) <= OreNodeMatchRadius)
            {
                return marker;
            }
        }
        return null;
    }

    private bool OverlapsPlacedBuilding(Vector3 position, Vector3 footprint)
    {
        foreach (Node node in GetTree().GetNodesInGroup(BuildingGroups.PlacedBuildings))
        {
            if (node is not Node3D placed || node is not IPlaceable other) continue;

            if (AabbOverlap(position, footprint, placed.GlobalPosition, other.FootprintSize))
                return true;
        }
        return false;
    }

    private static bool AabbOverlap(Vector3 posA, Vector3 sizeA, Vector3 posB, Vector3 sizeB)
    {
        Vector3 halfA = sizeA / 2f;
        Vector3 halfB = sizeB / 2f;
        return Mathf.Abs(posA.X - posB.X) < halfA.X + halfB.X
            && Mathf.Abs(posA.Z - posB.Z) < halfA.Z + halfB.Z;
    }
}
