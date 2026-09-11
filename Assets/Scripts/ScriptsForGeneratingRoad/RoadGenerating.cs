using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.AI.Navigation;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[RequireComponent(typeof(SplineContainer))]
public class RoadGenerating : MonoBehaviour
{
    [Header("Shape")]
    public float roadWidth = 4f;
    [Tooltip("How thick the road slab is, in world units.")]
    public float roadThickness = 0.3f;
    [Tooltip("Total points sampled along the whole spline. Higher = smoother.")]
    public int resolution = 200;

    [Header("Texturing")]
    [Tooltip("How many times the texture repeats per world unit of road length.")]
    public float uvTilingPerUnit = 0.25f;
    [Tooltip("Material used only on the top driving surface (this is the one with the road texture).")]
    public Material topMaterial;
    [Tooltip("Solid-color material used on the side walls and underside (no texture needed).")]
    public Material sideMaterial;

    private SplineContainer splineContainer;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private NavMeshSurface navSurface;
    public BotsManager botsManager;
    int sampleCount;

    void OnEnable()
    {
    splineContainer = GetComponent<SplineContainer>();
    meshFilter = GetComponent<MeshFilter>();
    meshRenderer = GetComponent<MeshRenderer>();
    navSurface = GetComponent<NavMeshSurface>();

    
    Spline.Changed += OnSplineChanged;
    GenerateRoad();
    }
    void OnDisable() => Spline.Changed -= OnSplineChanged;

    void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        if (spline == splineContainer.Spline) GenerateRoad();
    }

    [ContextMenu("Regenerate Road")]
    public void GenerateRoad()
    {
        if (splineContainer == null) splineContainer = GetComponent<SplineContainer>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        Spline spline = splineContainer.Spline;
        if (spline == null || spline.Count < 2)
        {
            Debug.LogWarning("SplineRoadGenerator needs a spline with at least 2 knots.");
            return;
        }

        Mesh mesh = BuildRibbonMesh(spline);
        meshFilter.mesh = mesh;

        // Submesh 0 (top) gets the textured road material, submesh 1 (sides + bottom) gets the flat color.
        meshRenderer.sharedMaterials = new Material[] { topMaterial, sideMaterial };

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null) collider.sharedMesh = mesh;

         //if (navSurface != null)
         //   navSurface.BuildNavMesh();
    }



    // Samples the spline at even intervals (0..1) and builds a solid ribbon
    // with real thickness: a top surface, a bottom surface, and two side walls.
    Mesh BuildRibbonMesh(Spline spline)
    {
        int count = resolution;

        // Per sample we now need 4 vertices: topLeft, topRight, bottomLeft, bottomRight
        Vector3[] vertices = new Vector3[count * 4];
        Vector3[] normals = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];

        // Top face: 2 tris per segment (6 indices). Sides + bottom: 6 tris per segment (18 indices).
        // Kept as separate buffers so they can be two submeshes with two different materials.
        int[] topTriangles = new int[(count - 1) * 6];
        int[] sideTriangles = new int[(count - 1) * 18];

        float distanceAccum = 0f;
        Vector3 prevPos = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);

            // Evaluate gives us position, tangent (forward), and up at t.
            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);

            Vector3 worldPos = splineContainer.transform.TransformPoint((Vector3)pos);
            Vector3 forward = ((Vector3)tangent).normalized;
            Vector3 upVec = ((Vector3)up).normalized;
            if (upVec == Vector3.zero) upVec = Vector3.up;

            Vector3 right = Vector3.Cross(upVec, forward).normalized * (roadWidth * 0.5f);
            Vector3 down = upVec * roadThickness;

            Vector3 topLeftWorld = worldPos - right;
            Vector3 topRightWorld = worldPos + right;
            Vector3 bottomLeftWorld = topLeftWorld - down;
            Vector3 bottomRightWorld = topRightWorld - down;

            int baseIdx = i * 4;
            vertices[baseIdx + 0] = transform.InverseTransformPoint(topLeftWorld);
            vertices[baseIdx + 1] = transform.InverseTransformPoint(topRightWorld);
            vertices[baseIdx + 2] = transform.InverseTransformPoint(bottomLeftWorld);
            vertices[baseIdx + 3] = transform.InverseTransformPoint(bottomRightWorld);

            if (i > 0) distanceAccum += Vector3.Distance(worldPos, prevPos);
            prevPos = worldPos;

            float v = distanceAccum * uvTilingPerUnit;
            // Top and bottom share simple 0..1 U mapping; sides reuse the same V so textures still tile along length.
            uvs[baseIdx + 0] = new Vector2(0f, v);
            uvs[baseIdx + 1] = new Vector2(1f, v);
            uvs[baseIdx + 2] = new Vector2(0f, v);
            uvs[baseIdx + 3] = new Vector2(1f, v);
        }

        int topTri = 0;
        int sideTri = 0;
        for (int i = 0; i < count - 1; i++)
        {
            int a0 = i * 4;       // top left
            int b0 = i * 4 + 1;   // top right
            int c0 = i * 4 + 2;   // bottom left
            int d0 = i * 4 + 3;   // bottom right

            int a1 = (i + 1) * 4;
            int b1 = (i + 1) * 4 + 1;
            int c1 = (i + 1) * 4 + 2;
            int d1 = (i + 1) * 4 + 3;

            // Top face (facing up) -- textured submesh
            topTriangles[topTri++] = a0;
            topTriangles[topTri++] = a1;
            topTriangles[topTri++] = b0;

            topTriangles[topTri++] = b0;
            topTriangles[topTri++] = a1;
            topTriangles[topTri++] = b1;

            // Bottom face (facing down, reversed winding) -- solid color submesh
            sideTriangles[sideTri++] = c0;
            sideTriangles[sideTri++] = d0;
            sideTriangles[sideTri++] = c1;

            sideTriangles[sideTri++] = d0;
            sideTriangles[sideTri++] = d1;
            sideTriangles[sideTri++] = c1;

            // Left wall (topLeft/bottomLeft edge) -- solid color submesh
            sideTriangles[sideTri++] = a0;
            sideTriangles[sideTri++] = c0;
            sideTriangles[sideTri++] = a1;

            sideTriangles[sideTri++] = a1;
            sideTriangles[sideTri++] = c0;
            sideTriangles[sideTri++] = c1;

            // Right wall (topRight/bottomRight edge) -- solid color submesh
            sideTriangles[sideTri++] = b0;
            sideTriangles[sideTri++] = b1;
            sideTriangles[sideTri++] = d0;

            sideTriangles[sideTri++] = d0;
            sideTriangles[sideTri++] = b1;
            sideTriangles[sideTri++] = d1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "GeneratedSplineRoad";
        if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}