using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

// Rebuilds one level of the original game (a Section prefab exported by AssetRipper) in the open scene
// out of OUR components: the road as a SplineRoad, round islands as TrackPlatforms (PlatformSlab cylinders),
// plank groups as track-placed BoardStacks, plus Start/End markers. Original sizes, no scaling.
// Not imported yet: traps (pendulum, cross), raised side paths, keys.
// Editor tool only — nothing at runtime knows about it.
public class OriginalLevelImporter : EditorWindow
{
    // Where the road's look (materials, thickness, tiling) is copied from, so imported roads match ours.
    private const string ReferenceLevelPath = "Assets/_Game/Prefabs/Levels/Level_17/Level_17.prefab";
    private const string IslandPrefabPath = "Assets/_Game/Prefabs/LevelComponents/Islands/Island.prefab";
    private const string StackPrefabFolder = "Assets/_Game/Prefabs/Board/BoardStacks";
    private const string SourcePathPref = "ShortcutRun.OriginalLevelImporter.Source";

    // path_cylinder mesh is 8.1 across before the piece's own scale, and its pivot is on the rim (where it
    // meets the road), not in the middle: the centre is 4 units ahead along the piece's forward.
    private const float IslandMeshDiameter = 8.098f;
    private static readonly Vector3 IslandMeshCentre = new Vector3(0f, 0f, 4f);
    private const string IslandPiece = "path_Round_Island";
    private const string PlankSpawner = "Spawner_platformPickup";

    private string sourcePath;
    private string levelName = "Level_Original_01";

    [MenuItem("Window/Shortcut Run/Original Level Import")]
    private static void Open() => GetWindow<OriginalLevelImporter>("Original Level Import");

    private void OnEnable() => sourcePath = EditorPrefs.GetString(SourcePathPref, "");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Pick a Section prefab from the AssetRipper export (…/ExportedProject/Assets/GameObject/" +
                                "Final_3_TutoVersion.prefab). It is rebuilt in the open scene, which must be saved.",
                                MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            sourcePath = EditorGUILayout.TextField("Section prefab", sourcePath);
            if (GUILayout.Button("…", GUILayout.Width(28)))
            {
                string picked = EditorUtility.OpenFilePanel("Original section prefab", Path.GetDirectoryName(sourcePath), "prefab");
                if (!string.IsNullOrEmpty(picked)) sourcePath = picked;
            }
        }

        using (new EditorGUI.DisabledScope(!File.Exists(sourcePath)))
        {
            if (GUILayout.Button("Import into open scene", GUILayout.Height(28)))
            {
                EditorPrefs.SetString(SourcePathPref, sourcePath);
                Import(sourcePath);
            }

            EditorGUILayout.Space();
            levelName = EditorGUILayout.TextField("Level prefab name", levelName);
            if (GUILayout.Button($"Save as level prefab in {OriginalLevelPrefabBuilder.OutputFolder}", GUILayout.Height(28)))
            {
                EditorPrefs.SetString(SourcePathPref, sourcePath);
                Selection.activeObject = OriginalLevelPrefabBuilder.Build(sourcePath, levelName);
            }
        }
    }

    /// <param name="mirrored">Build the level flipped left-right (a mirror twin for the level pool).</param>
    public static GameObject Import(string prefabPath, bool mirrored = false)
    {
        if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
        {
            Debug.LogError("Original import: save the scene first — the road mesh is saved next to it.");
            return null;
        }

        OriginalPrefabNode section = OriginalPrefabReader.Read(prefabPath, mirrored);
        OriginalPrefabNode path = section.FindChild("Path");
        if (path == null)
        {
            Debug.LogError($"Original import: '{section.Name}' has no 'Path' child — not a Section prefab?");
            return null;
        }

        var root = new GameObject("Original_" + section.Name);
        Undo.RegisterCreatedObjectUndo(root, "Import Original Level");

        // The original's designers didn't keep to their groups (islands under Pickups, plank groups under Path),
        // so islands and plank groups are picked out of both by what they are, not where they sit.
        List<OriginalPrefabNode> placed = path.Children.ToList();
        OriginalPrefabNode pickups = section.FindChild("Pickups");
        if (pickups != null) placed.AddRange(pickups.Children);

        Vector3 start = section.FindChild("Start")?.WorldPosition ?? Vector3.zero;
        Vector3 end = section.FindChild("End")?.WorldPosition ?? Vector3.zero;
        SplineRoad road = BuildRoad(path, start, end, root.transform, mirrored);
        List<TrackPlatform> islands = BuildIslands(placed, root.transform);
        int boards = PlaceStacks(placed, road, islands, root.transform);
        AddMarker("Start", section.FindChild("Start"), root.transform);
        AddMarker("End", section.FindChild("End"), root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"Original import: '{section.Name}' — {road.SectionCount} road sections, {islands.Count} islands, " +
                  $"{boards} boards.", root);
        return root;
    }

    // ---------- Road ----------

    private static SplineRoad BuildRoad(OriginalPrefabNode path, Vector3 start, Vector3 end, Transform parent, bool mirrored)
    {
        var pieces = new List<OriginalTrackBuilder.Piece>();
        foreach (OriginalPrefabNode node in path.Children)
        {
            if (!OriginalTrackBuilder.IsRoadPiece(node.BaseName)) continue;
            pieces.Add(new OriginalTrackBuilder.Piece
            {
                type = node.BaseName, position = node.WorldPosition, yaw = node.WorldYaw,
                scale = node.WorldScale, mirrored = mirrored,
            });
        }
        float width = OriginalTrackBuilder.RoadWidth(pieces.Count > 0 ? pieces[0].scale : 1.5f);

        var go = new GameObject("Road") { layer = LayerMask.NameToLayer(TrackPlatform.RoadLayerName) };
        go.transform.SetParent(parent, false);
        var road = go.AddComponent<SplineRoad>();
        CopyRoadLook(road, width);

        road.Container.Splines = OriginalTrackBuilder.Build(pieces, start, end);
        for (int i = 0; i < road.SectionCount; i++) road.SetWidth(i, width);
        road.Bake();
        return road;
    }

    // Only the look — not the whole component: the reference's baked mesh must never be shared.
    private static void CopyRoadLook(SplineRoad road, float width)
    {
        var target = new SerializedObject(road);
        target.FindProperty("defaultWidth").floatValue = width;

        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceLevelPath)?.GetComponentInChildren<SplineRoad>(true);
        if (reference != null)
        {
            var source = new SerializedObject(reference);
            foreach (string field in new[] { "roadThickness", "samplesPerMeter", "uvTilingPerUnit", "topMaterial", "sideMaterial" })
                target.CopyFromSerializedProperty(source.FindProperty(field));
        }
        else Debug.LogWarning($"Original import: reference road not found at {ReferenceLevelPath} — default look used.");

        target.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Islands ----------

    private static List<TrackPlatform> BuildIslands(List<OriginalPrefabNode> placed, Transform parent)
    {
        var islands = new List<TrackPlatform>();
        var prefab = AssetDatabase.LoadAssetAtPath<TrackPlatform>(IslandPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Original import: island prefab not found at {IslandPrefabPath} — islands skipped.");
            return islands;
        }

        var group = new GameObject("Islands").transform;
        group.SetParent(parent, false);

        foreach (OriginalPrefabNode node in placed.Where(n => n.BaseName == IslandPiece))
        {
            var island = (TrackPlatform)PrefabUtility.InstantiatePrefab(prefab, group);
            island.name = node.Name;
            Vector3 centre = node.LocalToWorld.MultiplyPoint3x4(IslandMeshCentre);
            island.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, node.WorldYaw, 0f));

            PlatformSlab slab = island.GetComponentInChildren<PlatformSlab>();
            float diameter = IslandMeshDiameter * node.WorldScale;
            var so = new SerializedObject(slab);
            so.FindProperty("shape").enumValueIndex = (int)PlatformSlab.SlabShape.Cylinder;
            so.FindProperty("width").floatValue = diameter;
            so.FindProperty("length").floatValue = diameter;
            so.ApplyModifiedPropertiesWithoutUndo();
            slab.Bake();

            islands.Add(island);
        }
        return islands;
    }

    // ---------- Boards ----------

    // Each plank group becomes one stack. Its rows are read from the group itself (spawners per height
    // layer), so any group type works as long as a matching shape exists — missing shapes are created.
    private static int PlaceStacks(List<OriginalPrefabNode> placed, SplineRoad road, List<TrackPlatform> islands, Transform parent)
    {
        var group = new GameObject("Boards").transform;
        group.SetParent(parent, false);

        var library = new StackLibrary();
        int boards = 0;

        foreach (OriginalPrefabNode node in placed)
        {
            int[] rows = RowsOf(node);
            if (rows.Length == 0) continue;

            TrackPlacedStack stack = library.Instantiate(rows, group);
            stack.name = $"{node.BaseName} [{string.Join(",", rows)}]";
            boards += rows.Sum();

            // Original groups lay their rows along local Z, ours along local X — a quarter turn apart.
            float yaw = node.WorldYaw + 90f;
            Vector3 position = node.WorldPosition;
            TrackPlatform island = IslandUnder(position, islands);

            if (island != null)
            {
                stack.PlaceOnPlatform(island, position, Quaternion.Euler(0f, yaw, 0f), "Import Original Level");
                continue;
            }

            TrackPlacement placement = TrackPlacement.Default;
            if (!TrackMath.TryProject(road, position, ref placement)) continue;
            // Placement yaw is relative to the road, so measure the road's own heading at that spot first.
            TrackMath.TryEvaluate(placement, out _, out Quaternion along);
            placement.yaw = Mathf.DeltaAngle(along.eulerAngles.y, yaw);
            stack.SetPlacement(placement, "Import Original Level");
        }
        return boards;
    }

    private static int[] RowsOf(OriginalPrefabNode group) =>
        group.Children
            .Where(c => c.BaseName == PlankSpawner)
            .GroupBy(c => Mathf.RoundToInt(c.LocalPosition.y * 10f))
            .OrderBy(layer => layer.Key)
            .Select(layer => layer.Count())
            .ToArray();

    // Radius from the slab's own width, not the collider: a just-baked collider can still report empty bounds.
    private static TrackPlatform IslandUnder(Vector3 position, List<TrackPlatform> islands)
    {
        foreach (TrackPlatform island in islands)
        {
            var slab = new SerializedObject(island.GetComponentInChildren<PlatformSlab>());
            float radius = slab.FindProperty("width").floatValue * 0.5f;
            Vector3 offset = position - island.transform.position;
            offset.y = 0f;
            if (offset.magnitude < radius) return island;
        }
        return null;
    }

    // ---------- Markers ----------

    private static void AddMarker(string name, OriginalPrefabNode node, Transform parent)
    {
        if (node == null) return;
        var marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.SetPositionAndRotation(node.WorldPosition, Quaternion.Euler(0f, node.WorldYaw, 0f));
    }

    // Finds the stack prefab and shape asset for a row pattern, creating the shape when we have none.
    private class StackLibrary
    {
        private readonly List<BoardStackShapeSO> shapes;
        private readonly List<TrackPlacedStack> prefabs;

        public StackLibrary()
        {
            shapes = AssetDatabase.FindAssets("t:BoardStackShapeSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<BoardStackShapeSO>(AssetDatabase.GUIDToAssetPath(g)))
                .ToList();
            prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { StackPrefabFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<TrackPlacedStack>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .ToList();
        }

        public TrackPlacedStack Instantiate(int[] rows, Transform parent)
        {
            BoardStackShapeSO shape = shapes.Find(s => s.Rows.SequenceEqual(rows)) ?? CreateShape(rows);
            TrackPlacedStack prefab = prefabs.Find(p => ShapeOf(p) == shape) ?? prefabs[0];

            var stack = (TrackPlacedStack)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (ShapeOf(prefab) != shape)
            {
                var boardStack = stack.GetComponent<BoardStack>();
                var boardPrefab = (CollectableBoard)new SerializedObject(boardStack).FindProperty("boardPrefab").objectReferenceValue;
                boardStack.Configure(shape, boardPrefab);
            }
            return stack;
        }

        private static BoardStackShapeSO ShapeOf(TrackPlacedStack stackPrefab) =>
            (BoardStackShapeSO)new SerializedObject(stackPrefab.GetComponent<BoardStack>()).FindProperty("shape").objectReferenceValue;

        // Saved next to the existing shapes, named like them: BoardStackShape[4,3,2,1].asset.
        private BoardStackShapeSO CreateShape(int[] rows)
        {
            string folder = shapes.Count > 0 ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(shapes[0])).Replace('\\', '/') : "Assets";
            var shape = CreateInstance<BoardStackShapeSO>();
            var so = new SerializedObject(shape);
            SerializedProperty list = so.FindProperty("rows");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) list.GetArrayElementAtIndex(i).intValue = rows[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(shape, $"{folder}/BoardStackShape[{string.Join(",", rows)}].asset");
            shapes.Add(shape);
            Debug.Log($"Original import: created shape [{string.Join(",", rows)}].", shape);
            return shape;
        }
    }
}
