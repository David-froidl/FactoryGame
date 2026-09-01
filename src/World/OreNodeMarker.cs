using Godot;

namespace FactoryGame.World;

/// <summary>
/// Visual + data anchor for one ore deposit. The node's own <see cref="Node3D.Position"/>
/// is where the deposit sits in the world; <see cref="Data"/> is what it is. Splitting it
/// this way means the same <see cref="OreNodeResource"/> could back several nodes, and
/// moving a node in the editor never touches the data file.
///
/// Joins the <see cref="BuildingGroups.OreNodes"/> group so <c>PlacementValidator</c> can
/// find nearby deposits by simple distance instead of a physics query — this marker still
/// has no collision shape of its own.
/// </summary>
public partial class OreNodeMarker : Node3D
{
    [Export] public OreNodeResource? Data { get; set; }

    public override void _Ready()
    {
        if (Data is null)
        {
            GD.PushWarning($"{Name}: OreNodeMarker has no Data resource assigned.");
            return;
        }

        AddToGroup(BuildingGroups.OreNodes);
        AddChild(BuildMesh());
        AddChild(BuildLabel());
    }

    private MeshInstance3D BuildMesh()
    {
        var mesh = new CylinderMesh
        {
            TopRadius = 3.5f,
            BottomRadius = 4.5f,
            Height = 1.2f,
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                AlbedoColor = ColorFor(Data!.OreType),
            },
        };
        return new MeshInstance3D { Mesh = mesh, Position = new Vector3(0, 0.6f, 0) };
    }

    private Label3D BuildLabel()
    {
        return new Label3D
        {
            Text = $"{Data!.OreType} ({Data.Purity})",
            Position = new Vector3(0, 3.5f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48,
            OutlineSize = 8,
        };
    }

    private static Color ColorFor(OreType type) => type switch
    {
        OreType.Iron => new Color(0.62f, 0.35f, 0.28f),
        OreType.Copper => new Color(0.85f, 0.48f, 0.20f),
        OreType.Limestone => new Color(0.80f, 0.78f, 0.68f),
        OreType.Coal => new Color(0.12f, 0.12f, 0.13f),
        _ => Colors.Magenta,
    };
}
