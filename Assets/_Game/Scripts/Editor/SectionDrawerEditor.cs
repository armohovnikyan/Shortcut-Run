using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Inspector for SectionDrawer. "Preview" traces the image (same tracer, scale, forks and dots as the Level Drawer),
// turns each drawn line into a clean 45°-grid line and builds the road from the library's sections along it; the
// result is drawn as sections in the Scene view, with warnings for whatever the library couldn't draw.
// "Draw Road" writes those sections into the level's SplineRoad (exact arcs and straights), "Place Objects" puts
// islands / jump pads on their dots. Closed loops can't be built from sections and are skipped (use the Level Drawer).
[CustomEditor(typeof(SectionDrawer))]
public class SectionDrawerEditor : Editor
{
    private string summary;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "roadWidth", "knotTolerance");
        serializedObject.ApplyModifiedProperties();

        var drawer = (SectionDrawer)target;
        EditorGUILayout.Space();

        SplineRoad road = drawer.FindRoad();
        if (drawer.roadImage == null) EditorGUILayout.HelpBox("Assign a road image.", MessageType.Info);
        if (road == null) EditorGUILayout.HelpBox("No SplineRoad found in this level — assign one.", MessageType.Warning);
        else if (Mathf.Abs(road.DefaultWidth - SectionDef.BaseRoadWidth * drawer.sectionScale) > road.DefaultWidth * 0.03f)
            EditorGUILayout.HelpBox($"The road's default width is {road.DefaultWidth:0.##} m but sections of scale {drawer.sectionScale} " +
                                    $"are {SectionDef.BaseRoadWidth * drawer.sectionScale:0.##} m wide.", MessageType.Warning);

        if (GUILayout.Button("Fill Sample Library"))
        {
            Undo.RecordObject(drawer, "Fill Sample Library");
            drawer.library = SampleLibrary();
            EditorUtility.SetDirty(drawer);
        }

        using (new EditorGUI.DisabledScope(drawer.roadImage == null))
        {
            if (GUILayout.Button("Preview")) summary = Preview(drawer, out _);
            using (new EditorGUI.DisabledScope(road == null))
                if (GUILayout.Button("Draw Road")) Draw(drawer, road);
            using (new EditorGUI.DisabledScope(drawer.islandPrefab == null && drawer.jumpPadPrefab == null))
                if (GUILayout.Button(new GUIContent("Place Objects",
                        "Islands on brown dots, jump pads on yellow dots. Replaces what the last placing made.")))
                    PlaceObjects(drawer);
        }

        if (drawer.warnings != null && drawer.warnings.Count > 0)
            EditorGUILayout.HelpBox("Couldn't draw everything:\n• " + string.Join("\n• ", drawer.warnings), MessageType.Warning);
        else if (drawer.chains != null && drawer.chains.Count > 0)
            EditorGUILayout.HelpBox("Every part of the drawing is covered by a library section.", MessageType.Info);
        if (!string.IsNullOrEmpty(summary)) EditorGUILayout.HelpBox(summary, MessageType.None);
    }

    /// <summary>Preview without the drawing (for scripts and tests).</summary>
    public static string Preview(SectionDrawer drawer) => Preview(drawer, out _);

    // Traces the image, builds the chains and hands everything to the drawer's gizmos. Returns the summary text.
    internal static string Preview(SectionDrawer drawer, out LevelDrawerEditor.Drawing drawing)
    {
        drawing = LevelDrawerEditor.Build(drawer);
        drawer.chains = null;
        drawer.warnings = new List<string>();
        if (drawing.paths == null) return drawing.summary;

        var open = new List<List<Vector3>>();
        foreach (LevelDrawerEditor.DrawnPath path in drawing.smoothed)
            if (!path.closed) open.Add(path.points);
        if (open.Count == 0)
        {
            drawer.warnings.Add("Only closed loops were drawn — sections can't build those. Use the Level Drawer for loops.");
            return drawing.summary;
        }

        float offset = SectionMatcher.GridOffset(open);
        drawer.chains = new List<SectionDrawer.Chain>();
        drawer.previewPaths = new List<Vector3[]>();
        drawer.previewClosed = new List<bool>();
        float y = drawer.roadHeight;
        int number = 0, skipped = 0;
        foreach (LevelDrawerEditor.DrawnPath path in drawing.smoothed)
        {
            number++;
            if (path.closed) { skipped++; continue; }
            List<Vector2> target = SectionMatcher.BuildTarget(path.points, offset, out float startYaw);
            if (target == null) { drawer.warnings.Add($"Spline {number}: too short to build."); continue; }

            SectionDrawer.Chain chain = SectionMatcher.Match(target, startYaw, drawer.library, drawer.sectionScale,
                                                              drawing.roadWidth, drawer.flagDeviation, number, drawer.warnings);
            drawer.chains.Add(chain);
            var line = new Vector3[target.Count];
            for (int i = 0; i < line.Length; i++) line[i] = new Vector3(target[i].x, y, target[i].y);
            drawer.previewPaths.Add(line);
            drawer.previewClosed.Add(false);
        }
        if (skipped > 0) drawer.warnings.Add($"{skipped} closed loop(s) skipped — sections can't build loops. Use the Level Drawer for those.");

        drawer.previewStart = drawing.start;
        drawer.previewFinish = drawing.finish;
        drawer.previewIslands = drawing.islands;
        drawer.previewJumpPads = drawing.jumpPads;
        SceneView.RepaintAll();

        var text = new System.Text.StringBuilder(drawing.summary);
        text.Append($"\nGrid turned {offset:0.#}° to the drawing.");
        foreach (SectionDrawer.Chain chain in drawer.chains)
        {
            text.Append($"\nSpline {chain.number}: {chain.sections.Count} section(s), mean gap {chain.meanError:0.#} m, worst {chain.maxError:0.#} m — ");
            foreach (SectionDrawer.PlacedSection placed in chain.sections) text.Append(placed.def.name).Append(", ");
            text.Length -= 2;
        }
        return text.ToString();
    }

    private void Draw(SectionDrawer drawer, SplineRoad road)
    {
        summary = Preview(drawer, out _);
        if (drawer.chains == null || drawer.chains.Count == 0) return;

        var paths = new List<LevelDrawerEditor.DrawnPath>();
        foreach (SectionDrawer.Chain chain in drawer.chains)
        {
            if (chain.sections.Count == 0) continue;
            List<SectionMatcher.Knot> knots = SectionMatcher.BuildKnots(chain, drawer.roadHeight);
            var points = new List<Vector3>();
            foreach (SectionMatcher.Knot knot in knots) points.Add(knot.position);
            paths.Add(new LevelDrawerEditor.DrawnPath { points = points, closed = false, knots = knots });
        }
        if (paths.Count == 0) return;
        if (LevelDrawerEditor.WriteRoad(drawer, road, paths)) summary = Preview(drawer, out _);
    }

    private void PlaceObjects(SectionDrawer drawer)
    {
        summary = Preview(drawer, out LevelDrawerEditor.Drawing drawing);
        if (drawing.paths == null) return;
        string placed = LevelDrawerEditor.PlaceObjects(drawer, drawing);
        if (placed != null) summary += "\n" + placed;
    }

    // The section chain's sample library (without its Start / Finish copies of "S") plus the plain pieces and
    // plain corners the drawing needs: 90° corners ("rr", "ll", "RR", "LL") and single 45° turns.
    public static List<SectionDef> SampleLibrary()
    {
        var library = new List<SectionDef>();
        void Add(string name, string pieces) => library.Add(new SectionDef { name = name, pieces = pieces });
        Add("Straight", "S");
        Add("Long", "S S");
        Add("Long x3", "S S S");
        Add("Short", "s");
        Add("rr", "r r");
        Add("ll", "l l");
        Add("RR", "R R");
        Add("LL", "L L");
        Add("r", "r");
        Add("l", "l");
        Add("R", "R");
        Add("L", "L");

        var known = new HashSet<string>();
        foreach (SectionDef def in library) known.Add(def.pieces);
        foreach (SectionDef def in SectionChainPreviewEditor.SampleLibrary())
        {
            if ((def.tags & (SectionTags.Start | SectionTags.Finish)) != 0 || !known.Add(def.pieces)) continue;
            library.Add(def);
        }
        return library;
    }
}
