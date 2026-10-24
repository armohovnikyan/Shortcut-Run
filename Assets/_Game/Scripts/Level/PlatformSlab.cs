using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A box- or cylinder-shaped piece of road for platforms (TrackPlatform), built like SplineRoad's slab:
// submesh 0 = textured top, submesh 1 = sides + bottom, so it takes a top and a side material.
// The top face sits at this object's origin and the slab hangs down by its thickness; the top's
// U runs across the width and V repeats along the length (local Z), same as the road texture.
// A cylinder fits inside the same width x length (equal values = round, different = oval).
// Editor-time only: "Bake Mesh" in the Inspector saves the mesh as an asset and points the
// MeshFilter/MeshCollider at it. Changing a field does nothing until you bake, so tweaking stays cheap.
// The file is named after the size, so equal platforms share one and a resize never overwrites another's.
// Size comes from the fields below — keep this object at scale 1, or the texture stretches.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class PlatformSlab : MonoBehaviour
{
    // Bump whenever the mesh-building code changes: it's part of the file name, so every platform
    // switches to a freshly built mesh instead of keeping one made by the old code.
    private const int MeshBuilderVersion = 2; // 2 = cylinder shape
    public const string MeshFolder = "Assets/_Game/Meshes/PlatformSlabs";

    public enum SlabShape { Box, Cylinder }

    [Header("Shape")]
    [SerializeField] private SlabShape shape = SlabShape.Box;
    [Tooltip("Cylinder only: how many flat strips make up the round side. Higher = rounder, heavier mesh.")]
    [SerializeField, Range(8, 128)] private int segments = 48;
    [Tooltip("Across the platform (local X), in world units. For a cylinder: its diameter across.")]
    [SerializeField, Min(0.1f)] private float width = 20f;
    [Tooltip("Along the platform (local Z), in world units. For a cylinder: its diameter along.")]
    [SerializeField, Min(0.1f)] private float length = 20f;
    [Tooltip("How thick the slab is, in world units. The top face stays at this object's origin.")]
    [SerializeField, Min(0.01f)] private float thickness = 0.3f;

    [Header("Texturing")]
    [Tooltip("How many times the texture repeats per metre of platform length — match the road's value.")]
    [SerializeField, Min(0f)] private float uvTilingPerUnit = 0.25f;
    [Tooltip("Top driving surface (the one with the road texture). Submesh 0.")]
    [SerializeField] private Material topMaterial;
    [Tooltip("Side walls and underside. Submesh 1.")]
    [SerializeField] private Material sideMaterial;

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> topTriangles = new List<int>();
    private readonly List<int> sideTriangles = new List<int>();

    private void BuildMesh(Mesh mesh)
    {
        vertices.Clear(); normals.Clear(); uvs.Clear();
        topTriangles.Clear(); sideTriangles.Clear();

        if (shape == SlabShape.Cylinder) AppendCylinder();
        else AppendBox();

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    private void AppendBox()
    {
        float x = width * 0.5f, z = length * 0.5f, y = -thickness;
        float vEnd = length * uvTilingPerUnit;

        // Corners named from above: back = -Z, front = +Z. Each face gets its own vertices so edges stay sharp.
        Vector3 topBackLeft = new Vector3(-x, 0f, -z), topBackRight = new Vector3(x, 0f, -z);
        Vector3 topFrontLeft = new Vector3(-x, 0f, z), topFrontRight = new Vector3(x, 0f, z);
        Vector3 botBackLeft = new Vector3(-x, y, -z), botBackRight = new Vector3(x, y, -z);
        Vector3 botFrontLeft = new Vector3(-x, y, z), botFrontRight = new Vector3(x, y, z);

        AddFace(topTriangles, Vector3.up, topBackLeft, topFrontLeft, topFrontRight, topBackRight, vEnd);
        AddFace(sideTriangles, Vector3.down, botFrontLeft, botBackLeft, botBackRight, botFrontRight, 1f);
        AddFace(sideTriangles, Vector3.left, botFrontLeft, topFrontLeft, topBackLeft, botBackLeft, 1f);
        AddFace(sideTriangles, Vector3.right, botBackRight, topBackRight, topFrontRight, botFrontRight, 1f);
        AddFace(sideTriangles, Vector3.back, botBackLeft, topBackLeft, topBackRight, botBackRight, 1f);
        AddFace(sideTriangles, Vector3.forward, botFrontRight, topFrontRight, topFrontLeft, botFrontLeft, 1f);
    }

    // Flat top and bottom caps (a fan around the centre) plus a smooth round wall. The top uses the
    // same planar mapping as the box top, so the road texture lies the same way on both shapes.
    // Angle 0 points along +Z and grows towards +X, i.e. clockwise seen from above.
    private void AppendCylinder()
    {
        float rx = width * 0.5f, rz = length * 0.5f, y = -thickness;
        float vEnd = length * uvTilingPerUnit;

        Vector3 Ring(int i, float height)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * rx, height, Mathf.Cos(angle) * rz);
        }

        Vector2 TopUv(Vector3 p) => new Vector2(p.x / width + 0.5f, (p.z / length + 0.5f) * vEnd);

        // Top cap: centre, then the ring. Fan triangles follow the ring clockwise = facing up.
        int top = vertices.Count;
        AddVertex(Vector3.zero, Vector3.up, TopUv(Vector3.zero));
        for (int i = 0; i < segments; i++) AddVertex(Ring(i, 0f), Vector3.up, TopUv(Ring(i, 0f)));
        for (int i = 0; i < segments; i++)
        {
            topTriangles.Add(top); topTriangles.Add(top + 1 + i); topTriangles.Add(top + 1 + (i + 1) % segments);
        }

        // Bottom cap: same fan in the opposite order = facing down.
        int bottom = vertices.Count;
        AddVertex(new Vector3(0f, y, 0f), Vector3.down, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < segments; i++) AddVertex(Ring(i, y), Vector3.down, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < segments; i++)
        {
            sideTriangles.Add(bottom); sideTriangles.Add(bottom + 1 + (i + 1) % segments); sideTriangles.Add(bottom + 1 + i);
        }

        // Wall: a top/bottom pair per ring point, with the first column repeated at the end so U can
        // run 0..1 around without wrapping back mid-strip. Normals are shared per column = smooth shading;
        // dividing by the radii keeps them perpendicular to the wall on an oval too.
        int wall = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            Vector3 p = Ring(i, 0f);
            Vector3 normal = new Vector3(p.x / (rx * rx), 0f, p.z / (rz * rz)).normalized;
            float u = i / (float)segments;
            AddVertex(p, normal, new Vector2(u, 1f));
            AddVertex(Ring(i, y), normal, new Vector2(u, 0f));
        }
        // Seen from outside, the next column is to the left (the ring runs clockwise from above).
        for (int i = 0; i < segments; i++)
        {
            int t0 = wall + i * 2, b0 = t0 + 1, t1 = t0 + 2, b1 = t0 + 3;
            sideTriangles.Add(b1); sideTriangles.Add(t1); sideTriangles.Add(t0);
            sideTriangles.Add(b1); sideTriangles.Add(t0); sideTriangles.Add(b0);
        }

        void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
        }
    }

    // Corners in clockwise order as seen from outside (Unity's front-face winding), starting bottom-left
    // of the face: (0,0) → (0,v) → (1,v) → (1,0) in UV space.
    private void AddFace(List<int> triangles, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float vEnd)
    {
        int s = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        for (int i = 0; i < 4; i++) normals.Add(normal);
        uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, vEnd));
        uvs.Add(new Vector2(1f, vEnd)); uvs.Add(new Vector2(1f, 0f));
        triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
        triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
    }

#if UNITY_EDITOR
    // False after a field changed and before the next bake — the Inspector shows a reminder then.
    public bool IsBakeCurrent
    {
        get
        {
            var filter = GetComponent<MeshFilter>();
            var meshCollider = GetComponent<MeshCollider>();
            Material[] materials = GetComponent<MeshRenderer>().sharedMaterials;
            return filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh) == MeshPath()
                   && meshCollider.sharedMesh == filter.sharedMesh
                   && materials.Length == 2 && materials[0] == topMaterial && materials[1] == sideMaterial;
        }
    }

    // "Box_v2_W20_L30_T0.3_UV0.25" / "Cylinder_v2_W20_L20_T0.3_UV0.25_S48" — everything the mesh is
    // built from, so the name alone says whether an existing file fits.
    private string MeshPath()
    {
        string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        string segmentPart = shape == SlabShape.Cylinder ? $"_S{segments}" : "";
        return $"{MeshFolder}/{shape}_v{MeshBuilderVersion}_W{F(width)}_L{F(length)}_T{F(thickness)}" +
               $"_UV{F(uvTilingPerUnit)}{segmentPart}.asset";
    }

    public void Bake()
    {
        string path = MeshPath();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            // CreateFolder makes one level at a time, so walk down the path creating what's missing.
            string folder = "Assets";
            foreach (string part in MeshFolder.Substring("Assets/".Length).Split('/'))
            {
                if (!AssetDatabase.IsValidFolder($"{folder}/{part}")) AssetDatabase.CreateFolder(folder, part);
                folder = $"{folder}/{part}";
            }
            // Mesh name = file name, or Unity warns that the main object doesn't match its file.
            mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            BuildMesh(mesh);
            AssetDatabase.CreateAsset(mesh, path);
        }

        // Recorded so a bake can be undone, and so it sticks as an override on prefab instances.
        var filter = GetComponent<MeshFilter>();
        var meshCollider = GetComponent<MeshCollider>();
        var meshRenderer = GetComponent<MeshRenderer>();
        Undo.RecordObjects(new Object[] { filter, meshCollider, meshRenderer }, "Bake Platform Mesh");

        filter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
        meshRenderer.sharedMaterials = new[] { topMaterial, sideMaterial };

        foreach (Object component in new Object[] { filter, meshCollider, meshRenderer })
        {
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        if (Vector3.Distance(transform.lossyScale, Vector3.one) > 0.001f)
            Debug.LogWarning($"{name}: PlatformSlab is scaled {transform.lossyScale} — the road texture will stretch. " +
                             "Set the scale to 1 and use Width / Length / Thickness instead.", this);
    }
#endif
}
