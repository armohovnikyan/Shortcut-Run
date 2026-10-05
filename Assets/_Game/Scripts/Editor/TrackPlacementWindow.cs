using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Level-building tool: a palette of stack/stamp prefabs to click onto roads, snapping settings,
// and a board counter per road section. Works in Prefab Mode (level prefab) and in open scenes.
public class TrackPlacementWindow : EditorWindow
{
    private readonly List<GameObject> palette = new List<GameObject>();
    private GameObject selectedPrefab;
    private Transform parent;
    private Vector2 scroll;

    // Where the prefab would land this frame (preview) — valid only while hasPreview.
    private bool hasPreview;
    private Target preview;

    // A click target: a spot on the road (placement), or a spot on a TrackPlatform (platform set).
    private struct Target
    {
        public TrackPlacement placement;
        public TrackPlatform platform;
        public Vector3 point;        // platform only: the surface point under the mouse
        public Quaternion rotation;  // platform only: platform's turn + Facing
    }

    [MenuItem("Window/Shortcut Run/Track Placement")]
    private static void Open() => GetWindow<TrackPlacementWindow>("Track Placement");

    private void OnEnable()
    {
        RefreshPalette();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ---------- Window ----------

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawPalette();
        EditorGUILayout.Space();
        DrawSettings();
        EditorGUILayout.Space();
        DrawBoardCounter();
        EditorGUILayout.EndScrollView();

        // Thumbnails load in the background; keep repainting until they arrive.
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    private void DrawPalette()
    {
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            string folder = EditorGUILayout.TextField("Folder", TrackPlacementSettings.PaletteFolder);
            if (EditorGUI.EndChangeCheck()) TrackPlacementSettings.PaletteFolder = folder;
            if (GUILayout.Button("Refresh", GUILayout.Width(60))) RefreshPalette();
        }

        if (palette.Count == 0)
        {
            EditorGUILayout.HelpBox("No prefabs with TrackPlacedStack or TrackPlacedStamp in this folder " +
                                    "(subfolders included).", MessageType.Info);
            return;
        }

        const float cell = 84f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / cell));
        for (int start = 0; start < palette.Count; start += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(start + columns, palette.Count); i++)
                    DrawPaletteButton(palette[i], cell);
                GUILayout.FlexibleSpace();
            }
        }

        EditorGUILayout.HelpBox(selectedPrefab != null
                ? $"Placing '{selectedPrefab.name}': click on a road or a platform in the Scene view. " +
                  "Esc or click the prefab again to stop."
                : "Pick a prefab, then click on a road or a platform (TrackPlatform) in the Scene view.",
            selectedPrefab != null ? MessageType.Info : MessageType.None);
    }

    private void DrawPaletteButton(GameObject prefab, float cell)
    {
        Texture preview = AssetPreview.GetAssetPreview(prefab);
        if (preview == null) preview = AssetPreview.GetMiniThumbnail(prefab);

        var content = new GUIContent(prefab.name, preview, prefab.name);
        var style = new GUIStyle(GUI.skin.button)
        {
            imagePosition = ImagePosition.ImageAbove,
            wordWrap = true,
            fontSize = 9
        };

        bool selected = prefab == selectedPrefab;
        bool pressed = GUILayout.Toggle(selected, content, style, GUILayout.Width(cell - 4), GUILayout.Height(cell));
        if (pressed != selected) SetSelectedPrefab(pressed ? prefab : null);
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("New Objects & Snapping", EditorStyles.boldLabel);

        TrackPlacementSettings.Facing = (TrackFacing)EditorGUILayout.EnumPopup(
            new GUIContent("Facing", "Facing given to newly placed objects."), TrackPlacementSettings.Facing);

        parent = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("Parent", "New objects go under this. Empty = the prefab root in Prefab Mode, " +
                                     "otherwise the scene root."), parent, typeof(Transform), true);

        TrackPlacementSettings.DistanceStep = EditorGUILayout.FloatField(
            new GUIContent("Distance Step", "Snap distance along the road to multiples of this, in metres. " +
                                            "0 = off. Also used when dragging with the track handles."),
            TrackPlacementSettings.DistanceStep);

        TrackPlacementSettings.Lanes = EditorGUILayout.IntField(
            new GUIContent("Lanes", "Snap across the road to the middle of this many equal lanes " +
                                    "(3 = left / centre / right). 0 = free. Also used by the track handles."),
            TrackPlacementSettings.Lanes);
    }

    private void DrawBoardCounter()
    {
        EditorGUILayout.LabelField("Board Counter", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        TrackPlacementSettings.ShowSectionLabels = EditorGUILayout.Toggle(
            new GUIContent("Labels In Scene", "Show each section's board count over its middle in the Scene view."),
            TrackPlacementSettings.ShowSectionLabels);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        List<SplineRoad> roads = FindInEditedLevel<SplineRoad>();
        if (roads.Count == 0)
        {
            EditorGUILayout.HelpBox("No SplineRoad in the open level.", MessageType.None);
            return;
        }

        Dictionary<(SplineRoad, int), Count> counts = CountBoards(out Count unplaced, out Count onPlatforms);
        int totalBoards = unplaced.boards + onPlatforms.boards;

        foreach (SplineRoad road in roads)
        {
            EditorGUILayout.LabelField(road.name, EditorStyles.miniBoldLabel);
            for (int i = 0; i < road.SectionCount; i++)
            {
                counts.TryGetValue((road, road.GetSectionId(i)), out Count c);
                totalBoards += c.boards;
                EditorGUILayout.LabelField($"    Spline {i}", $"{c.boards} boards  ({c.objects} placed)");
            }
        }

        if (onPlatforms.objects > 0)
            EditorGUILayout.LabelField("    On platforms", $"{onPlatforms.boards} boards  ({onPlatforms.objects} placed)");
        if (unplaced.objects > 0)
            EditorGUILayout.LabelField("    Not on a road", $"{unplaced.boards} boards  ({unplaced.objects} placed)");
        EditorGUILayout.LabelField("Total", $"{totalBoards} boards", EditorStyles.boldLabel);
    }

    // ---------- Scene view ----------

    private void OnSceneGUI(SceneView sceneView)
    {
        if (TrackPlacementSettings.ShowSectionLabels) DrawSectionLabels();
        if (selectedPrefab != null) HandlePlacement();
    }

    private void HandlePlacement()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            SetSelectedPrefab(null);
            e.Use();
            return;
        }

        // Owning the default control stops clicks from selecting whatever is under the mouse.
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
            hasPreview = TryGetTargetUnderMouse(e.mousePosition, out preview);

        if (hasPreview && e.type == EventType.Repaint) DrawPreview(preview);

        // Alt + click is the Scene camera's orbit — leave it alone.
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (TryGetTargetUnderMouse(e.mousePosition, out Target target))
            {
                if (target.platform != null) PlaceOnPlatform(target);
                else PlaceSelectedPrefab(target.placement);
            }
            e.Use();
        }

        if (e.type == EventType.MouseMove) SceneView.RepaintAll();
    }

    // Road or platform under the mouse, whichever is nearer to the camera.
    private bool TryGetTargetUnderMouse(Vector2 mousePosition, out Target target)
    {
        target = default;
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        bool onRoad = TryGetPlacementUnderMouse(ray, out TrackPlacement placement);
        float roadDistance = float.PositiveInfinity;
        if (onRoad && TrackMath.TryEvaluate(placement, out Vector3 roadPoint, out _))
            roadDistance = Vector3.Distance(ray.origin, roadPoint);

        // Collider.Raycast tests that one collider's shape — no physics scene query, so it works in Prefab Mode.
        float best = roadDistance;
        foreach (TrackPlatform platform in FindInEditedLevel<TrackPlatform>())
        {
            Collider surface = platform.Surface;
            if (surface == null || !surface.enabled || !surface.Raycast(ray, out RaycastHit hit, best)) continue;

            best = hit.distance;
            target.platform = platform;
            target.point = hit.point;
            target.rotation = Quaternion.Euler(0f,
                platform.transform.eulerAngles.y + (float)TrackPlacementSettings.Facing, 0f);
        }

        if (target.platform != null) return true;
        target.placement = placement;
        return onRoad;
    }

    // The road surface under the mouse on any road in the level, snapped. Misses count only within 1 m of an edge.
    private bool TryGetPlacementUnderMouse(Ray ray, out TrackPlacement placement)
    {
        placement = TrackPlacement.Default;
        float bestOffRoad = 1f;
        bool found = false;

        foreach (SplineRoad road in FindInEditedLevel<SplineRoad>())
        {
            road.EnsureSectionIds();
            TrackPlacement candidate = TrackPlacement.Default;
            if (!TrackMath.TryProjectRay(road, ray, ref candidate, out float offRoad) || offRoad > bestOffRoad) continue;

            bestOffRoad = offRoad;
            placement = candidate;
            found = true;
        }

        if (!found) return false;
        placement.facing = TrackPlacementSettings.Facing;
        TrackPlacementSettings.Snap(ref placement);
        return true;
    }

    private static void DrawPreview(Target target)
    {
        Vector3 position;
        Quaternion rotation;
        if (target.platform != null)
        {
            position = target.point;
            rotation = target.rotation;
        }
        else if (!TrackMath.TryEvaluate(target.placement, out position, out rotation)) return;

        float size = HandleUtility.GetHandleSize(position) * 0.5f;
        Vector3 up = rotation * Vector3.up;
        Handles.color = new Color(0.3f, 0.9f, 1f);
        Handles.DrawWireDisc(position, up, size);
        Handles.ArrowHandleCap(0, position, rotation, size * 1.5f, EventType.Repaint); // shows Facing
    }

    private void PlaceSelectedPrefab(TrackPlacement placement)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        GameObject instance = InstantiateInEditedLevel(selectedPrefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place " + selectedPrefab.name);

        var placed = instance.GetComponent<ITrackPlaced>();
        TrackPlacement fromPrefab = placed.Placement;
        placement.yaw = fromPrefab.yaw;       // keep what the prefab itself was set up with
        placement.height = fromPrefab.height;
        placed.SetPlacement(placement, "Place " + selectedPrefab.name);

        Undo.CollapseUndoOperations(undoGroup); // one Ctrl+Z removes the whole placement
    }

    // Stacks and stamps alike. On a platform a stamp's pattern is laid out flat around the clicked point.
    private void PlaceOnPlatform(Target target)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        GameObject instance = InstantiateInEditedLevel(selectedPrefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place " + selectedPrefab.name);

        var placed = instance.GetComponent<ITrackPlaced>();
        TrackPlacement fromPrefab = placed.Placement; // keep the prefab's own lift and extra turn
        Quaternion rotation = target.rotation * Quaternion.Euler(0f, fromPrefab.yaw, 0f);
        Vector3 position = target.point + Vector3.up * fromPrefab.height;
        placed.PlaceOnPlatform(target.platform, position, rotation, "Place " + selectedPrefab.name);

        Undo.CollapseUndoOperations(undoGroup); // one Ctrl+Z removes the whole placement
    }

    private GameObject InstantiateInEditedLevel(GameObject prefab)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        Transform target = parent != null ? parent : stage != null ? stage.prefabContentsRoot.transform : null;

        return target != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, target)
            : (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
    }

    private void DrawSectionLabels()
    {
        if (Event.current.type != EventType.Repaint) return;

        Dictionary<(SplineRoad, int), Count> counts = CountBoards(out _, out _);
        foreach (SplineRoad road in FindInEditedLevel<SplineRoad>())
        {
            for (int i = 0; i < road.SectionCount; i++)
            {
                Spline spline = road.Container.Splines[i];
                if (spline.Count < 2) continue;

                var middle = new TrackPlacement(2f) // 2 m above the road so it isn't hidden by stacks
                {
                    road = road,
                    sectionId = road.GetSectionId(i),
                    distance = spline.GetLength() * 0.5f
                };
                if (!TrackMath.TryEvaluate(middle, out Vector3 position, out _)) continue;

                counts.TryGetValue((road, middle.sectionId), out Count c);
                Handles.Label(position, $"Spline {i}: {c.boards} boards", EditorStyles.whiteLargeLabel);
            }
        }
    }

    // ---------- Data ----------

    private struct Count
    {
        public int boards, objects;
    }

    // Boards per (road, section). A stamp counts all its boards toward its anchor's section.
    // Stacks on platforms are counted on their own.
    private static Dictionary<(SplineRoad, int), Count> CountBoards(out Count unplaced, out Count onPlatforms)
    {
        var counts = new Dictionary<(SplineRoad, int), Count>();
        unplaced = default;
        onPlatforms = default;

        foreach (TrackPlacedStack stack in FindInEditedLevel<TrackPlacedStack>())
        {
            int boards = stack.GetComponent<BoardStack>().BoardCount;
            if (stack.Platform != null)
            {
                onPlatforms.boards += boards;
                onPlatforms.objects++;
            }
            else Add(stack.Placement, boards, ref unplaced);
        }

        foreach (TrackPlacedStamp stamp in FindInEditedLevel<TrackPlacedStamp>())
        {
            int boards = stamp.Stamp != null ? stamp.Stamp.BoardCount : 0;
            if (stamp.Platform != null)
            {
                onPlatforms.boards += boards;
                onPlatforms.objects++;
            }
            else Add(stamp.Placement, boards, ref unplaced);
        }

        return counts;

        void Add(TrackPlacement placement, int boards, ref Count missing)
        {
            if (placement.road == null || placement.road.FindSectionIndex(placement.sectionId) < 0)
            {
                missing.boards += boards;
                missing.objects++;
                return;
            }

            var key = (placement.road, placement.sectionId);
            counts.TryGetValue(key, out Count c);
            c.boards += boards;
            c.objects++;
            counts[key] = c;
        }
    }

    // The level being edited: the open prefab in Prefab Mode, otherwise every loaded scene.
    private static List<T> FindInEditedLevel<T>() where T : Component
    {
        var found = new List<T>();
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            found.AddRange(stage.prefabContentsRoot.GetComponentsInChildren<T>(true));
            return found;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return found;
    }

    private void RefreshPalette()
    {
        palette.Clear();
        string folder = TrackPlacementSettings.PaletteFolder;
        if (!AssetDatabase.IsValidFolder(folder)) return;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<ITrackPlaced>() != null) palette.Add(prefab);
        }
        palette.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));

        if (!palette.Contains(selectedPrefab)) SetSelectedPrefab(null);
    }

    private void SetSelectedPrefab(GameObject prefab)
    {
        selectedPrefab = prefab;
        hasPreview = false;
        Repaint();
        SceneView.RepaintAll();
    }
}
