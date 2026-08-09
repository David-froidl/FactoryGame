using Godot;

namespace FactoryGame.World;

/// <summary>
/// Builds a static ground mesh + collision from an authored heightmap image. No runtime
/// generation and no chunking: this phase is one fixed 500x500m map, so a single mesh
/// built once in <see cref="_Ready"/> is simpler than anything dynamic and cheap enough
/// to not matter for load time at this size.
///
/// The heightmap is read as a raw file (bypassing Godot's texture import/compression) so
/// pixel values stay exact — a compressed VRAM texture format would quantize height data.
/// </summary>
public partial class HeightmapTerrain : Node3D
{
    /// <summary>Grayscale heightmap; red channel 0..1 maps to 0..<see cref="HeightScale"/> metres.</summary>
    [Export] public string HeightmapPath = "res://assets/terrain/heightmap.png";

    /// <summary>Terrain footprint, in metres, centered on this node's origin.</summary>
    [Export] public float WorldSize = 500f;

    /// <summary>World-space height at heightmap value 1.0 (pure white).</summary>
    [Export] public float HeightScale = 20f;

    public override void _Ready()
    {
        Image heightmap = Image.LoadFromFile(ProjectSettings.GlobalizePath(HeightmapPath));
        int res = heightmap.GetWidth();

        var heightData = new float[res * res];
        ArrayMesh mesh = BuildMesh(heightmap, res, heightData);

        AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
                AlbedoColor = new Color(0.35f, 0.45f, 0.28f),
                Roughness = 0.95f,
            },
        });

        AddChild(BuildCollision(res, heightData));
    }

    private ArrayMesh BuildMesh(Image heightmap, int res, float[] heightData)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int j = 0; j < res; j++)
        {
            for (int i = 0; i < res; i++)
            {
                float u = (float)i / (res - 1);
                float v = (float)j / (res - 1);
                float x = (u - 0.5f) * WorldSize;
                float z = (v - 0.5f) * WorldSize;
                float y = heightmap.GetPixel(i, j).R * HeightScale;

                heightData[j * res + i] = y;
                st.SetUV(new Vector2(u, v));
                st.AddVertex(new Vector3(x, y, z));
            }
        }

        for (int j = 0; j < res - 1; j++)
        {
            for (int i = 0; i < res - 1; i++)
            {
                int v00 = j * res + i;
                int v10 = j * res + (i + 1);
                int v01 = (j + 1) * res + i;
                int v11 = (j + 1) * res + (i + 1);

                // Wound so the front face (the one Godot doesn't cull) points up (+Y).
                st.AddIndex(v00); st.AddIndex(v10); st.AddIndex(v01);
                st.AddIndex(v10); st.AddIndex(v11); st.AddIndex(v01);
            }
        }

        st.GenerateNormals();
        return st.Commit();
    }

    private StaticBody3D BuildCollision(int res, float[] heightData)
    {
        // HeightMapShape3D's grid has a fixed 1-metre cell size and is centered on its own
        // origin, so it's scaled up to WorldSize on the node rather than baked into MapData.
        float cellScale = WorldSize / (res - 1);
        var shape = new HeightMapShape3D
        {
            MapWidth = res,
            MapDepth = res,
            MapData = heightData,
        };

        var collisionShape = new CollisionShape3D
        {
            Shape = shape,
            Scale = new Vector3(cellScale, 1f, cellScale),
        };

        var body = new StaticBody3D();
        body.AddChild(collisionShape);
        return body;
    }
}
