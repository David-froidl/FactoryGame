using Godot;

namespace FactoryGame.World;

/// <summary>
/// Shared box-collision builder for placed buildings (<c>MachineVisual3D</c>,
/// <c>TerminalVisual3D</c>) — without this they're invisible to <c>PlayerInteractor</c>'s
/// raycast (so the UI could never show "you're looking at a smelter") and the player could
/// walk straight through them. Belts don't use this; see <see cref="IPlaceable"/>.
/// </summary>
public static class BuildingCollision
{
    public static StaticBody3D BuildStaticBody(Vector3 footprint)
    {
        var collisionShape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = footprint },
            Position = new Vector3(0, footprint.Y / 2f, 0),
        };
        var body = new StaticBody3D();
        body.AddChild(collisionShape);
        return body;
    }
}
