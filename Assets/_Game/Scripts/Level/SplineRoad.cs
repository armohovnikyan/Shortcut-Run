using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Builds the road slab (textured top + solid sides/bottom) for EVERY spline in the SplineContainer
// on this object, all into one mesh: submesh 0 = every section's top, submesh 1 = every section's sides.
// Editor-time only: the mesh is saved as an asset next to the level prefab and referenced by the
// MeshFilter/MeshCollider, so the saved prefab is complete and nothing is generated at runtime.
// Rebakes by itself when a spline or a field changes; "Bake Road" in the Inspector forces it.
[ExecuteAlways]
[RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class SplineRoad : MonoBehaviour
{
    // Each section's width is stored inside its own spline (embedded spline data), not in a list here,
    // so it stays with the right section when splines are deleted, reordered or copied.
    public const string WidthDataKey = "RoadWidth";
    // Bump whenever the mesh-building code changes: it's part of the fingerprint, so every road
    // rebakes once by itself instead of keeping a mesh made by the old code.
    private const int MeshBuilderVersion = 2; // 2 = end caps on open sections

    [Header("Shape")]
    [Tooltip("Width for a section that has no width of its own yet (e.g. a newly added spline).")]
    [SerializeField, Min(0.1f)] private float defaultWidth = 20f;
    [Tooltip("How thick the road slab is, in world units.")]
    [SerializeField, Min(0.01f)] private float roadThickness = 0.3f;
    [Tooltip("Cross-sections per metre of road. Higher = smoother curves, heavier mesh.")]
    [SerializeField, Min(0.1f)] private float samplesPerMeter = 1f;

    [Header("Texturing")]
    [Tooltip("How many times the texture repeats per metre of road length.")]
    [SerializeField, Min(0f)] private float uvTilingPerUnit = 0.25f;
    [Tooltip("Top driving surface (the one with the road texture). Submesh 0.")]
    [SerializeField] private Material topMaterial;
    [Tooltip("Side walls and underside. Submesh 1.")]
    [SerializeField] private Material sideMaterial;

    [Tooltip("The saved mesh asset this road bakes into. Created on the first bake.")]
    [SerializeField] private Mesh bakedMesh;
    // Fingerprint of everything the mesh is built from, taken at the last bake. Lets automatic
    // rebakes skip the work when a scene/prefab is merely opened (OnValidate runs on load too).
    [SerializeField, HideInInspector] private int bakedHash;

    private SplineContainer container;

    public SplineContainer Container => container != null ? container : container = GetComponent<SplineContainer>();
    public int SectionCount => Container.Splines.Count;

    // Permanent ID of each section, stored inside its spline like the width. Things placed on the road
    // remember this instead of the spline index, which shifts when splines are deleted or reordered.
    public const string SectionIdKey = "SectionId";

    // 0 = the spline has no ID yet (they're assigned in the editor, see EnsureSectionIds).
    public int GetSectionId(int splineIndex)
    {
        return Container.Splines[splineIndex].TryGetIntData(SectionIdKey, out SplineData<int> data)
            ? data.DefaultValue
            : 0;
    }

    // -1 = no section with that ID (e.g. its spline was deleted).
    public int FindSectionIndex(int sectionId)
    {
        if (sectionId == 0) return -1;
        for (int i = 0; i < SectionCount; i++)
            if (GetSectionId(i) == sectionId) return i;
        return -1;
    }

    public float GetWidth(int splineIndex)
    {
        Spline spline = Container.Splines[splineIndex];
        return spline.TryGetFloatData(WidthDataKey, out SplineData<float> data) && data.DefaultValue > 0f
            ? data.DefaultValue
            : defaultWidth;
    }

    // Mesh buffers shared by all sections while building.
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> topTriangles = new List<int>();
    private readonly List<int> sideTriangles = new List<int>();

    private void BuildMesh(Mesh mesh)
    {
        vertices.Clear(); normals.Clear(); uvs.Clear();
        topTriangles.Clear(); sideTriangles.Clear();

        for (int i = 0; i < SectionCount; i++)
        {
            Spline spline = Container.Splines[i];
            if (spline.Count >= 2) AppendSection(spline, GetWidth(i));
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    // Samples one spline evenly and appends its slab. Every face gets its own vertices,
    // so the edges between top, walls and bottom stay sharp instead of being smoothed.
    // Built in this object's local space — the splines live on the same transform.
    private void AppendSection(Spline spline, float width)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(spline.GetLength() * samplesPerMeter) + 1);
        const int perSample = 8; // top L/R, bottom L/R, left wall T/B, right wall T/B
        int first = vertices.Count;

        float distance = 0f;
        Vector3 previous = Vector3.zero;
        Vector3 startForward = Vector3.forward, endForward = Vector3.forward;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 upward);

            Vector3 center = pos;
            Vector3 forward = ((Vector3)tangent).normalized;
            Vector3 up = ((Vector3)upward).normalized;
            if (up == Vector3.zero) up = Vector3.up;
            Vector3 rightDir = Vector3.Cross(up, forward).normalized;
            if (i == 0) startForward = forward;
            if (i == count - 1) endForward = forward;

            Vector3 right = rightDir * (width * 0.5f);
            Vector3 down = up * roadThickness;
            Vector3 topLeft = center - right, topRight = center + right;
            Vector3 bottomLeft = topLeft - down, bottomRight = topRight - down;

            if (i > 0) distance += Vector3.Distance(center, previous);
            previous = center;
            float v = distance * uvTilingPerUnit;

            AddVertex(topLeft, up, 0f);        AddVertex(topRight, up, 1f);
            AddVertex(bottomLeft, -up, 0f);    AddVertex(bottomRight, -up, 1f);
            AddVertex(topLeft, -rightDir, 0f); AddVertex(bottomLeft, -rightDir, 1f);
            AddVertex(topRight, rightDir, 0f); AddVertex(bottomRight, rightDir, 1f);

            void AddVertex(Vector3 position, Vector3 normal, float u)
            {
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int i = 0; i < count - 1; i++)
        {
            int c = first + i * perSample, n = c + perSample; // this and next cross-section

            AddQuad(topTriangles, c + 0, n + 0, c + 1, n + 1);  // top, facing up
            AddQuad(sideTriangles, c + 3, n + 3, c + 2, n + 2); // bottom, facing down
            AddQuad(sideTriangles, c + 5, n + 5, c + 4, n + 4); // left wall
            AddQuad(sideTriangles, c + 6, n + 6, c + 7, n + 7); // right wall
        }

        // An open section would otherwise show its hollow inside at both ends. A closed loop has no ends.
        if (!spline.Closed)
        {
            AddEndCap(first, -startForward, false);
            AddEndCap(first + (count - 1) * perSample, endForward, true);
        }
    }

    // Flat face across the slab's cross-section, using the corners of the first/last sample
    // (sample + 0..3 = top left, top right, bottom left, bottom right). Own vertices, so the
    // cap's edges stay sharp and it's lit as facing straight out of the road end.
    private void AddEndCap(int sample, Vector3 normal, bool isEnd)
    {
        int s = vertices.Count;
        for (int corner = 0; corner < 4; corner++)
        {
            vertices.Add(vertices[sample + corner]);
            normals.Add(normal);
            uvs.Add(new Vector2(corner % 2, corner < 2 ? 1f : 0f));
        }

        // Unity draws triangles that are clockwise from the viewer's side. Looking at the start cap
        // you face along the road, looking at the end cap you face back against it — so left and
        // right swap on screen and the two caps need opposite orders.
        if (isEnd)
        {
            sideTriangles.Add(s + 1); sideTriangles.Add(s + 0); sideTriangles.Add(s + 2);
            sideTriangles.Add(s + 1); sideTriangles.Add(s + 2); sideTriangles.Add(s + 3);
        }
        else
        {
            sideTriangles.Add(s + 0); sideTriangles.Add(s + 1); sideTriangles.Add(s + 3);
            sideTriangles.Add(s + 0); sideTriangles.Add(s + 3); sideTriangles.Add(s + 2);
        }
    }

    // (a0, a1) = one edge along the road, (b0, b1) = the opposite edge; 0 = this sample, 1 = next.
    private static void AddQuad(List<int> triangles, int a0, int a1, int b0, int b1)
    {
        triangles.Add(a0); triangles.Add(a1); triangles.Add(b0);
        triangles.Add(b0); triangles.Add(a1); triangles.Add(b1);
    }

#if UNITY_EDITOR
    // Raised after every bake. Every road change (knots, widths, splines added/removed, undo) ends in
    // a bake, so objects placed on the road only need this one event to follow it.
    public static event System.Action<SplineRoad> Baked;

    private bool rebuildQueued;

    // Gives every spline a unique non-zero ID. Also fixes duplicates: a copied spline carries its
    // original's ID along with its other data, so the later one gets a fresh ID.
    public void EnsureSectionIds()
    {
        var used = new HashSet<int>();
        bool changed = false;

        for (int i = 0; i < SectionCount; i++)
        {
            int id = GetSectionId(i);
            if (id != 0 && used.Add(id)) continue;

            do id = UnityEngine.Random.Range(1, int.MaxValue); while (used.Contains(id));
            used.Add(id);
            Container.Splines[i].GetOrCreateIntData(SectionIdKey).DefaultValue = id;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(Container);
            PrefabUtility.RecordPrefabInstancePropertyModifications(Container);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        Spline.Changed += OnSplineChanged;
        SplineContainer.SplineAdded += OnSplineListChanged;
        SplineContainer.SplineRemoved += OnSplineListChanged;
        Undo.undoRedoPerformed += QueueRebuild;
    }

    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
        SplineContainer.SplineAdded -= OnSplineListChanged;
        SplineContainer.SplineRemoved -= OnSplineListChanged;
        Undo.undoRedoPerformed -= QueueRebuild;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) QueueRebuild();
    }

    private void OnSplineChanged(Spline changed, int knotIndex, SplineModification modification)
    {
        foreach (Spline spline in Container.Splines)
            if (spline == changed) { QueueRebuild(); return; }
    }

    private void OnSplineListChanged(SplineContainer changed, int splineIndex)
    {
        if (changed == Container) QueueRebuild();
    }

    public void SetWidth(int splineIndex, float width)
    {
        Undo.RecordObject(Container, "Change Road Width");
        Container.Splines[splineIndex].GetOrCreateFloatData(WidthDataKey).DefaultValue = Mathf.Max(0.1f, width);
        EditorUtility.SetDirty(Container);
        PrefabUtility.RecordPrefabInstancePropertyModifications(Container);
        QueueRebuild(); // embedded data changes don't raise Spline.Changed
    }

    // Asset creation isn't allowed inside OnValidate, and a knot drag fires many changes per frame —
    // so collapse them into one rebuild on the next editor tick.
    private void QueueRebuild()
    {
        if (rebuildQueued) return;
        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return; // destroyed meanwhile
            rebuildQueued = false;
            // Prefab assets in the Project window also get OnValidate — never write through those.
            if (Application.isPlaying || EditorUtility.IsPersistent(this)) return;
            EnsureSectionIds(); // even when the mesh is current: splines made before IDs existed need one
            bool upToDate = bakedMesh != null && bakedHash == ComputeBakeHash()
                            && GetComponent<MeshFilter>().sharedMesh == bakedMesh;
            if (!upToDate) Bake();
        };
    }

    // Deterministic across editor sessions (no System.HashCode, no instance IDs) because it's saved.
    private int ComputeBakeHash()
    {
        int hash = 17;
        Mix(MeshBuilderVersion);
        Mix(defaultWidth.GetHashCode()); Mix(roadThickness.GetHashCode());
        Mix(samplesPerMeter.GetHashCode()); Mix(uvTilingPerUnit.GetHashCode());
        MixText(AssetDatabase.GetAssetPath(topMaterial)); MixText(AssetDatabase.GetAssetPath(sideMaterial));

        for (int i = 0; i < SectionCount; i++)
        {
            Spline spline = Container.Splines[i];
            Mix(GetWidth(i).GetHashCode());
            Mix(spline.Closed ? 1 : 0);
            Mix(spline.Count);
            foreach (BezierKnot knot in spline.Knots)
            {
                Mix(knot.Position.GetHashCode()); Mix(knot.TangentIn.GetHashCode());
                Mix(knot.TangentOut.GetHashCode()); Mix(knot.Rotation.GetHashCode());
            }
        }
        return hash;

        void Mix(int value) => hash = unchecked(hash * 31 + value);
        void MixText(string text) { foreach (char ch in text) Mix(ch); }
    }

    public void Bake()
    {
        EnsureSectionIds();
        bool anySection = false;
        foreach (Spline spline in Container.Splines)
            if (spline.Count >= 2) anySection = true;
        if (!anySection)
        {
            Debug.LogWarning($"{name}: SplineRoad needs at least one spline with 2 or more knots.", this);
            return;
        }

        Mesh mesh = GetOrCreateMeshAsset();
        if (mesh == null)
        {
            Debug.LogWarning($"{name}: save the scene or open the level prefab in Prefab Mode first — " +
                             "the road mesh needs a folder to be saved into.", this);
            return;
        }

        BuildMesh(mesh);
        EditorUtility.SetDirty(mesh);
        ScheduleSave(mesh);

        var filter = GetComponent<MeshFilter>();
        if (filter.sharedMesh != mesh) { filter.sharedMesh = mesh; EditorUtility.SetDirty(filter); }

        // Same reference but new contents: the collider only re-cooks when the mesh is reassigned.
        var meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
        EditorUtility.SetDirty(meshCollider);

        var meshRenderer = GetComponent<MeshRenderer>();
        Material[] materials = meshRenderer.sharedMaterials;
        if (materials.Length != 2 || materials[0] != topMaterial || materials[1] != sideMaterial)
        {
            meshRenderer.sharedMaterials = new[] { topMaterial, sideMaterial };
            EditorUtility.SetDirty(meshRenderer);
        }

        bakedHash = ComputeBakeHash();
        EditorUtility.SetDirty(this);

        Baked?.Invoke(this);
    }

    // Reuses this road's own asset; makes a new one on the first bake, or when a duplicated
    // road still points at the original's mesh (otherwise both would overwrite one file).
    private Mesh GetOrCreateMeshAsset()
    {
        if (bakedMesh != null && EditorUtility.IsPersistent(bakedMesh) && !IsMeshSharedWithAnotherRoad())
            return bakedMesh;

        string folder = GetMeshFolder();
        if (folder == null) return null;

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));

        string fileName = string.Join("_", gameObject.name.Split(Path.GetInvalidFileNameChars()));
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
        // Mesh name = file name, or Unity warns that the main object doesn't match its file.
        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(mesh, path);

        bakedMesh = mesh;
        EditorUtility.SetDirty(this);
        return mesh;
    }

    private bool IsMeshSharedWithAnotherRoad()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (SplineRoad other in root.GetComponentsInChildren<SplineRoad>(true))
                if (other != this && other.bakedMesh == bakedMesh) return true;
        return false;
    }

    // "<folder of the level prefab>/<prefab name>_Meshes". Falls back to the scene when the road
    // isn't part of a prefab. Null when there's nothing saved on disk to put it next to.
    private string GetMeshFolder()
    {
        string ownerPath = PrefabStageUtility.GetPrefabStage(gameObject)?.assetPath;
        if (string.IsNullOrEmpty(ownerPath)) ownerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        if (string.IsNullOrEmpty(ownerPath)) ownerPath = gameObject.scene.path;
        return string.IsNullOrEmpty(ownerPath) ? null : MeshFolderFor(ownerPath);
    }

    public const string MeshFolderSuffix = "_Meshes";

    // The one rule for where a prefab's/scene's road meshes live — also used by the mesh tidy-up tool,
    // so the two always agree. "Assets/Levels/Level_01.prefab" -> "Assets/Levels/Level_01_Meshes".
    public static string MeshFolderFor(string ownerPath)
    {
        string directory = Path.GetDirectoryName(ownerPath).Replace('\\', '/');
        return $"{directory}/{Path.GetFileNameWithoutExtension(ownerPath)}{MeshFolderSuffix}";
    }

    // Writing the .asset file on every drag frame would stutter — save once things go quiet.
    private static readonly HashSet<Mesh> pendingSaves = new HashSet<Mesh>();
    private static double saveTime;

    private static void ScheduleSave(Mesh mesh)
    {
        if (pendingSaves.Count == 0) EditorApplication.update += SaveWhenQuiet;
        pendingSaves.Add(mesh);
        saveTime = EditorApplication.timeSinceStartup + 0.5;
    }

    private static void SaveWhenQuiet()
    {
        if (EditorApplication.timeSinceStartup < saveTime) return;
        EditorApplication.update -= SaveWhenQuiet;
        foreach (Mesh mesh in pendingSaves)
            if (mesh != null) AssetDatabase.SaveAssetIfDirty(mesh);
        pendingSaves.Clear();
    }
#endif
}
