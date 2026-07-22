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
    [Tooltip("Total points sampled along the whole spline. Higher = smoother.")]
    public int resolution = 200;

    [Header("Texturing")]
    [Tooltip("How many times the texture repeats per world unit of road length.")]
    public float uvTilingPerUnit = 0.25f;

    private SplineContainer splineContainer;
    private MeshFilter meshFilter;

    private NavMeshSurface navSurface;
    int sampleCount;

    void OnEnable()
    {
    splineContainer = GetComponent<SplineContainer>();
    meshFilter = GetComponent<MeshFilter>();
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

        Spline spline = splineContainer.Spline;
        if (spline == null || spline.Count < 2)
        {
            Debug.LogWarning("SplineRoadGenerator needs a spline with at least 2 knots.");
            return;
        }

        Mesh mesh = BuildRibbonMesh(spline);
        meshFilter.mesh = mesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null) collider.sharedMesh = mesh;

         if (navSurface != null)
            navSurface.BuildNavMesh();
    }

    

    // Samples the spline at even intervals (0..1) and builds a flat ribbon,
    // producing ONE continuous mesh no matter how the curve bends.
    Mesh BuildRibbonMesh(Spline spline)
    {
        int count = resolution;
        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        int[] triangles = new int[(count - 1) * 6];

        float splineLength = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
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

            vertices[i * 2] = transform.InverseTransformPoint(worldPos - right);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(worldPos + right);

            if (i > 0) distanceAccum += Vector3.Distance(worldPos, prevPos);
            prevPos = worldPos;

            float v = distanceAccum * uvTilingPerUnit;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        int tri = 0;
        for (int i = 0; i < count - 1; i++)
        {
            int a = i * 2;
            int b = i * 2 + 1;
            int c = (i + 1) * 2;
            int d = (i + 1) * 2 + 1;

            triangles[tri++] = a;
            triangles[tri++] = c;
            triangles[tri++] = b;

            triangles[tri++] = b;
            triangles[tri++] = c;
            triangles[tri++] = d;
        }

        Mesh mesh = new Mesh();
        mesh.name = "GeneratedSplineRoad";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}