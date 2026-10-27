using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

// Inspector for LevelDrawer: "Preview" traces the image and shows the result as gizmos;
// "Draw Road" replaces the road's splines with it (one Undo step) and bakes the road;
// "Place Objects" puts helper platforms on brown dots and jump pads on yellow dots.
// Per path: pixels -> metres -> even resample -> Gaussian smoothing (rounds corners) -> Douglas-Peucker
// (keeps only the knots the shape needs) -> AutoSmooth knots.
[CustomEditor(typeof(LevelDrawer))]
public class LevelDrawerEditor : Editor
{
    private struct DrawnPath
    {
        public List<Vector3> points; // drawer-local
        public bool closed;
    }

    // Everything one trace of the image gives, in drawer-local space.
    private class Drawing
    {
        public List<DrawnPath> paths;
        public Vector3? start, finish;
        public readonly List<Vector3> platforms = new List<Vector3>();
        public readonly List<Pose> jumpPads = new List<Pose>();
        public string summary;
    }

    private string summary;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        var drawer = (LevelDrawer)target;
        EditorGUILayout.Space();

        SplineRoad road = drawer.FindRoad();
        if (drawer.roadImage == null)
            EditorGUILayout.HelpBox("Assign a road image.", MessageType.Info);
        if (road == null)
            EditorGUILayout.HelpBox("No SplineRoad found in this level — assign one.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(drawer.roadImage == null))
        {
            if (GUILayout.Button("Preview")) summary = Preview(drawer);
            using (new EditorGUI.DisabledScope(road == null))
                if (GUILayout.Button("Draw Road")) Draw(drawer, road);
            using (new EditorGUI.DisabledScope(drawer.helperPlatformPrefab == null && drawer.jumpPadPrefab == null))
                if (GUILayout.Button(new GUIContent("Place Objects",
                        "Helper platforms on brown dots, jump pads on yellow dots. Replaces what the last placing made.")))
                    PlaceObjects(drawer);
        }

        if (!string.IsNullOrEmpty(summary)) EditorGUILayout.HelpBox(summary, MessageType.None);
    }

    // Traces the image and hands the result to the drawer's gizmos. Returns the summary text.
    public static string Preview(LevelDrawer drawer)
    {
        Drawing drawing = Build(drawer);
        if (drawing.paths == null) return drawing.summary;
        drawer.previewPaths = new List<Vector3[]>();
        drawer.previewClosed = new List<bool>();
        foreach (DrawnPath path in drawing.paths)
        {
            drawer.previewPaths.Add(path.points.ToArray());
            drawer.previewClosed.Add(path.closed);
        }
        drawer.previewStart = drawing.start;
        drawer.previewFinish = drawing.finish;
        drawer.previewPlatforms = drawing.platforms;
        drawer.previewJumpPads = drawing.jumpPads;
        SceneView.RepaintAll();
        return drawing.summary;
    }

    private void Draw(LevelDrawer drawer, SplineRoad road)
    {
        Drawing drawing = Build(drawer);
        summary = drawing.summary;
        List<DrawnPath> paths = drawing.paths;
        if (paths == null || paths.Count == 0) return;

        SplineContainer container = road.Container;
        if (container.Splines.Count > 0 && !EditorUtility.DisplayDialog("Draw Road",
                $"Replace the {container.Splines.Count} spline(s) on '{road.name}' with {paths.Count} drawn spline(s)?\n\n" +
                "Things placed on the old splines may need re-placing. Undo (Ctrl+Z) brings the old road back.",
                "Replace", "Cancel"))
            return;

        Undo.RecordObject(container, "Draw Road From Image");
        while (container.Splines.Count > 0) container.RemoveSplineAt(0);

        foreach (DrawnPath path in paths)
        {
            var spline = new Spline();
            foreach (Vector3 local in path.points)
            {
                Vector3 roadLocal = road.transform.InverseTransformPoint(drawer.transform.TransformPoint(local));
                spline.Add(new BezierKnot(roadLocal), TangentMode.AutoSmooth);
            }
            spline.Closed = path.closed;
            container.AddSpline(spline);
        }
        EditorUtility.SetDirty(container);
        PrefabUtility.RecordPrefabInstancePropertyModifications(container);

        for (int i = 0; i < container.Splines.Count; i++) road.SetWidth(i, drawer.roadWidth);
        road.Bake();
        summary = Preview(drawer);
    }

    // Helper platforms on brown dots, jump pads on yellow dots, into the level's "HelperPlatforms" /
    // "JumpPads" holders (made when missing). What the previous placing made is removed first — all one Undo step.
    private void PlaceObjects(LevelDrawer drawer)
    {
        Drawing drawing = Build(drawer);
        summary = drawing.summary;
        if (drawing.paths == null) return;

        drawer.placedObjects.RemoveAll(o => o == null); // deleted by hand meanwhile
        if (drawer.placedObjects.Count > 0 && !EditorUtility.DisplayDialog("Place Objects",
                $"Remove the {drawer.placedObjects.Count} object(s) placed last time (moved or not) and place " +
                $"{drawing.platforms.Count} platform(s) + {drawing.jumpPads.Count} jump pad(s) from the image?\n\n" +
                "Undo (Ctrl+Z) brings the old ones back.", "Replace", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Place Level Objects");
        int group = Undo.GetCurrentGroup();
        Undo.RecordObject(drawer, "Place Level Objects");
        foreach (GameObject old in drawer.placedObjects) Undo.DestroyObjectImmediate(old);
        drawer.placedObjects.Clear();

        int platforms = 0, pads = 0;
        if (drawer.helperPlatformPrefab != null)
        {
            Transform holder = Holder(drawer, "HelperPlatforms");
            foreach (Vector3 local in drawing.platforms)
                Spawn(drawer, drawer.helperPlatformPrefab, holder, new Pose(local, Quaternion.identity), ++platforms);
        }
        if (drawer.jumpPadPrefab != null)
        {
            Transform holder = Holder(drawer, "JumpPads");
            foreach (Pose pad in drawing.jumpPads)
                Spawn(drawer, drawer.jumpPadPrefab, holder, pad, ++pads);
        }

        EditorUtility.SetDirty(drawer);
        Undo.CollapseUndoOperations(group);
        Preview(drawer);
        summary += $"\nPlaced {platforms} helper platform(s), {pads} jump pad(s).";
    }

    private static void Spawn(LevelDrawer drawer, GameObject prefab, Transform holder, Pose local, int number)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        Undo.RegisterCreatedObjectUndo(instance, "Place Level Objects");
        instance.name = $"{prefab.name} ({number})";
        instance.transform.SetPositionAndRotation(drawer.transform.TransformPoint(local.position),
                                                  drawer.transform.rotation * local.rotation);
        drawer.placedObjects.Add(instance);
    }

    // The level's holder object by name (a direct child of the level root), made when missing.
    private static Transform Holder(LevelDrawer drawer, string holderName)
    {
        Transform root = drawer.transform.root;
        Transform holder = root.Find(holderName);
        if (holder != null) return holder;
        holder = new GameObject(holderName).transform;
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "Place Level Objects");
        holder.SetParent(root, false);
        return holder;
    }

    // ---------- Image -> drawer-local paths ----------

    private static Drawing Build(LevelDrawer drawer)
    {
        var drawing = new Drawing();
        string assetPath = AssetDatabase.GetAssetPath(drawer.roadImage);
        // Decoded from the file itself: works whatever the texture's import settings (Read/Write, compression, max size).
        var texture = new Texture2D(2, 2);
        if (!texture.LoadImage(File.ReadAllBytes(assetPath)))
        {
            drawing.summary = $"Couldn't read '{assetPath}' as an image.";
            return drawing;
        }
        RoadImageTracer.Result traced = RoadImageTracer.Trace(texture.GetPixels32(), texture.width, texture.height,
                                                               drawer.darkThreshold, drawer.forkJoinDistance);
        int imageWidth = texture.width, imageHeight = texture.height;
        Object.DestroyImmediate(texture);

        if (traced.Paths.Count == 0)
        {
            drawing.summary = "No road found — is the road darker than the Dark Threshold?";
            return drawing;
        }

        float pixelLength = 0f;
        foreach (RoadImageTracer.Path path in traced.Paths) pixelLength += PolylineLength(path.Points, path.Closed);
        float scale = drawer.scaleMode == LevelDrawer.ScaleMode.FitLength
            ? drawer.targetLength / Mathf.Max(1f, pixelLength)
            : drawer.metersPerPixel;

        Vector2 origin = traced.Paths[0].Points[0];
        Vector3 ToLocal(Vector2 p) => new Vector3((p.x - origin.x) * scale, drawer.roadHeight, (p.y - origin.y) * scale);

        // Below ~1.5 pixels of smoothing the pixel staircase would still show.
        float sigma = Mathf.Max(drawer.cornerSmoothing, scale * 1.5f);
        float spacing = Mathf.Max(sigma / 4f, 0.05f);

        var smoothed = new List<DrawnPath>();
        foreach (RoadImageTracer.Path path in traced.Paths)
        {
            var local = new List<Vector3>();
            foreach (Vector2 p in path.Points) local.Add(ToLocal(p));
            List<Vector3> even = Resample(local, spacing, path.Closed);
            smoothed.Add(new DrawnPath { points = Smooth(even, sigma / spacing, path.Closed), closed = path.Closed });
        }

        float halfWidth = drawer.roadWidth * 0.5f;
        int forks = JoinForks(smoothed, traced.JoinDistance * scale, halfWidth);
        int trimmed = TrimAtCrossings(smoothed, halfWidth);

        drawing.paths = new List<DrawnPath>();
        int knots = 0, marked = 0;
        for (int i = 0; i < smoothed.Count; i++)
        {
            List<Vector3> simple = Simplify(smoothed[i].points, drawer.knotTolerance, smoothed[i].closed);
            if (simple.Count < 2) continue;
            knots += simple.Count;
            if (traced.Paths[i].Marked) marked++;
            drawing.paths.Add(new DrawnPath { points = simple, closed = smoothed[i].closed });
        }

        if (traced.Start.HasValue) drawing.start = ToLocal(traced.Start.Value);
        if (traced.Finish.HasValue) drawing.finish = ToLocal(traced.Finish.Value);
        foreach (Vector2 p in traced.Platforms) drawing.platforms.Add(ToLocal(p));
        // A jump pad faces the way the race runs along the road under it (taken from the smoothed line,
        // before knots are thinned out, so the direction is exact).
        foreach (Vector2 p in traced.JumpPads)
        {
            Vector3 position = ToLocal(p);
            drawing.jumpPads.Add(new Pose(position, Quaternion.LookRotation(RoadDirectionAt(smoothed, position), Vector3.up)));
        }

        int loops = drawing.paths.FindAll(p => p.closed).Count;
        drawing.summary = $"Image {imageWidth}×{imageHeight}, stroke ≈ {traced.StrokeThickness:0.#} px\n" +
                  $"{drawing.paths.Count} spline(s){(loops > 0 ? $" ({loops} loop)" : "")}, {knots} knots, " +
                  $"road length ≈ {pixelLength * scale:0} m ({scale:0.###} m/px)\n" +
                  $"Directed by green/blue dots: {marked} of {drawing.paths.Count} spline(s)\n" +
                  $"Forks joined: {forks}   Ends pulled back to a crossing road's edge: {trimmed}\n" +
                  $"Race start: {(traced.Start.HasValue ? "green-dotted line end" : "no green line end — lowest line end used")}   " +
                  $"Finish dot: {(traced.Finish.HasValue ? "found" : "none")}\n" +
                  $"Brown dots (helper platforms): {drawing.platforms.Count}   Yellow dots (jump pads): {drawing.jumpPads.Count}";
        return drawing;
    }

    // Race direction of the road segment nearest to `point` (flat). Forward (+Z) when there's no road.
    private static Vector3 RoadDirectionAt(List<DrawnPath> paths, Vector3 point)
    {
        Vector3 best = Vector3.forward;
        float bestDistance = float.PositiveInfinity;
        foreach (DrawnPath path in paths)
        {
            List<Vector3> p = path.points;
            int segments = path.closed ? p.Count : p.Count - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = p[i], b = p[(i + 1) % p.Count];
                if (Flat(b - a).sqrMagnitude < 1e-6f) continue;
                float d = DistanceToSegment(Flat(point), Flat(a), Flat(b));
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = FlatDir(b - a);
            }
        }
        return best;
    }

    // ---------- Junctions ----------

    private struct PathEnd
    {
        public int path;
        public bool atStart;
        public Vector3 position;
        public Vector3 outward; // flat unit direction pointing out of the path at this end
    }

    private struct EndEdit
    {
        public PathEnd end;
        public Vector3 point;      // the new end point
        public bool trim;          // drop the points inside the through road first
        public Vector3 axisPoint;  // through road: a point on its centre line ...
        public Vector3 normal;     // ... and its sideways direction
        public float side;         // which side of it this arm is on (+1 / -1)
    }

    private static List<PathEnd> OpenEnds(List<DrawnPath> paths)
    {
        var ends = new List<PathEnd>();
        for (int i = 0; i < paths.Count; i++)
        {
            List<Vector3> p = paths[i].points;
            if (paths[i].closed || p.Count < 2) continue;
            int k = Mathf.Min(3, p.Count - 1);
            ends.Add(new PathEnd { path = i, atStart = true, position = p[0], outward = FlatDir(p[0] - p[k]) });
            ends.Add(new PathEnd { path = i, atStart = false, position = p[p.Count - 1], outward = FlatDir(p[p.Count - 1] - p[p.Count - 1 - k]) });
        }
        return ends;
    }

    // A fork drawn with gaps: ONE road end with two or more road ends of the other kind around it
    // (a road ending where several start, or several ending where one starts). Built like a hand-made T:
    // the single road runs on across the junction by half a width, and each arm is stretched or cut
    // to end exactly on that road's side, so nothing overlaps. Returns how many forks were joined.
    private static int JoinForks(List<DrawnPath> paths, float joinDistance, float halfWidth)
    {
        List<PathEnd> ends = OpenEnds(paths);
        var edits = new List<EndEdit>();
        var used = new HashSet<int>();
        int forks = 0;

        for (int t = 0; t < ends.Count; t++)
        {
            if (used.Contains(t)) continue;
            PathEnd through = ends[t];
            var arms = new List<int>();
            bool unique = true;
            for (int o = 0; o < ends.Count; o++)
            {
                if (o == t || ends[o].path == through.path) continue;
                float d = FlatDistance(ends[o].position, through.position);
                if (d > joinDistance || d < 0.01f) continue; // touching ends = a drawn junction, handled elsewhere
                if (ends[o].atStart == through.atStart) unique = false;
                else arms.Add(o);
            }
            if (!unique || arms.Count < 2) continue;

            forks++;
            used.Add(t);
            Vector3 normal = Vector3.Cross(Vector3.up, through.outward);
            edits.Add(new EndEdit { end = through, point = through.position + through.outward * halfWidth });

            foreach (int a in arms)
            {
                used.Add(a);
                PathEnd arm = ends[a];
                float offset = Vector3.Dot(Flat(arm.position - through.position), normal);
                float facing = Vector3.Dot(arm.outward, normal);
                if (Mathf.Abs(facing) < 0.3f) continue; // arm runs alongside the road — nothing sensible to snap to
                float side = Mathf.Sign(offset);
                float s = (side * halfWidth - offset) / facing;
                edits.Add(new EndEdit
                {
                    end = arm, point = arm.position + arm.outward * s, trim = s < 0f,
                    axisPoint = through.position, normal = normal, side = side
                });
            }
        }

        foreach (EndEdit edit in edits)
        {
            List<Vector3> p = paths[edit.end.path].points;
            if (edit.trim)
                while (p.Count > 2)
                {
                    Vector3 q = edit.end.atStart ? p[0] : p[p.Count - 1];
                    if (edit.side * Vector3.Dot(Flat(q - edit.axisPoint), edit.normal) >= halfWidth) break;
                    p.RemoveAt(edit.end.atStart ? 0 : p.Count - 1);
                }
            if (edit.end.atStart) p.Insert(0, edit.point);
            else p.Add(edit.point);
        }
        return forks;
    }

    // A road end lying on the middle of another road (a drawn T, a line meeting a loop): pull the end
    // back to that road's edge so the two don't overlap. Ends meeting another road's END are left alone.
    private static int TrimAtCrossings(List<DrawnPath> paths, float halfWidth)
    {
        int trimmed = 0;
        foreach (PathEnd end in OpenEnds(paths))
        {
            for (int j = 0; j < paths.Count; j++)
            {
                if (j == end.path) continue;
                List<Vector3> other = paths[j].points;
                if (DistanceToPath(end.position, paths[j]) > halfWidth * 0.5f) continue;
                if (!paths[j].closed && (FlatDistance(end.position, other[0]) < halfWidth * 2f
                                         || FlatDistance(end.position, other[other.Count - 1]) < halfWidth * 2f))
                    continue;

                List<Vector3> p = paths[end.path].points;
                Vector3 inside = end.atStart ? p[0] : p[p.Count - 1];
                while (p.Count > 2)
                {
                    Vector3 q = end.atStart ? p[0] : p[p.Count - 1];
                    if (DistanceToPath(q, paths[j]) >= halfWidth) break;
                    inside = q;
                    p.RemoveAt(end.atStart ? 0 : p.Count - 1);
                }
                // The edge lies between the last point removed and the new end — find it exactly.
                Vector3 outside = end.atStart ? p[0] : p[p.Count - 1];
                float lo = 0f, hi = 1f;
                for (int k = 0; k < 12; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (DistanceToPath(Vector3.Lerp(outside, inside, mid), paths[j]) >= halfWidth) lo = mid; else hi = mid;
                }
                Vector3 edge = Vector3.Lerp(outside, inside, lo);
                if (Vector3.Distance(edge, outside) > 0.01f)
                {
                    if (end.atStart) p.Insert(0, edge); else p.Add(edge);
                }
                trimmed++;
                break;
            }
        }
        return trimmed;
    }

    private static float DistanceToPath(Vector3 point, DrawnPath path)
    {
        List<Vector3> p = path.points;
        float best = float.PositiveInfinity;
        int segments = path.closed ? p.Count : p.Count - 1;
        for (int i = 0; i < segments; i++)
            best = Mathf.Min(best, DistanceToSegment(Flat(point), Flat(p[i]), Flat(p[(i + 1) % p.Count])));
        return best;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    private static Vector3 FlatDir(Vector3 v) => Flat(v).normalized;
    private static float FlatDistance(Vector3 a, Vector3 b) => Vector3.Distance(Flat(a), Flat(b));

    private static float PolylineLength(List<Vector2> points, bool closed)
    {
        float length = 0f;
        for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
        if (closed && points.Count > 2) length += Vector2.Distance(points[points.Count - 1], points[0]);
        return length;
    }

    // Points every `spacing` metres along the line (closed: around the loop, last point not repeated).
    private static List<Vector3> Resample(List<Vector3> points, float spacing, bool closed)
    {
        var source = new List<Vector3>(points);
        if (closed) source.Add(points[0]);

        var result = new List<Vector3> { source[0] };
        float carried = 0f;
        for (int i = 1; i < source.Count; i++)
        {
            Vector3 a = source[i - 1], b = source[i];
            float segment = Vector3.Distance(a, b);
            float d = spacing - carried;
            while (d <= segment)
            {
                result.Add(Vector3.Lerp(a, b, d / segment));
                d += spacing;
            }
            carried = segment - (d - spacing);
        }

        if (closed)
        {
            if (result.Count > 1 && Vector3.Distance(result[result.Count - 1], source[0]) < spacing * 0.5f)
                result.RemoveAt(result.Count - 1);
        }
        else if (Vector3.Distance(result[result.Count - 1], source[source.Count - 1]) > spacing * 0.01f)
        {
            result.Add(source[source.Count - 1]);
        }
        return result;
    }

    // Gaussian blur along the line. Open lines are extended by point reflection so the ends stay
    // put and keep their direction — that's what keeps junctions joined.
    private static List<Vector3> Smooth(List<Vector3> points, float sigmaInSamples, bool closed)
    {
        int n = points.Count;
        if (n < 3 || sigmaInSamples < 0.5f) return points;
        int radius = Mathf.CeilToInt(sigmaInSamples * 2.5f);
        var weights = new float[radius + 1];
        for (int k = 0; k <= radius; k++) weights[k] = Mathf.Exp(-k * k / (2f * sigmaInSamples * sigmaInSamples));

        Vector3 At(int i)
        {
            if (closed) return points[((i % n) + n) % n];
            if (i < 0) return 2f * points[0] - points[Mathf.Min(-i, n - 1)];
            if (i >= n) return 2f * points[n - 1] - points[Mathf.Max(2 * (n - 1) - i, 0)];
            return points[i];
        }

        var result = new List<Vector3>(n);
        for (int i = 0; i < n; i++)
        {
            Vector3 sum = Vector3.zero;
            float total = 0f;
            for (int k = -radius; k <= radius; k++)
            {
                float weight = weights[Mathf.Abs(k)];
                sum += At(i + k) * weight;
                total += weight;
            }
            result.Add(sum / total);
        }
        if (!closed)
        {
            result[0] = points[0];
            result[n - 1] = points[n - 1];
        }
        return result;
    }

    // Douglas-Peucker: keeps the fewest points that stay within `tolerance` of the line.
    private static List<Vector3> Simplify(List<Vector3> points, float tolerance, bool closed)
    {
        var source = new List<Vector3>(points);
        if (closed) source.Add(points[0]);
        if (source.Count < 3) return points;

        var keep = new bool[source.Count];
        keep[0] = keep[source.Count - 1] = true;
        if (closed) keep[source.Count / 2] = true; // a loop's start and end coincide — split it first
        var stack = new Stack<(int, int)>();
        if (closed) { stack.Push((0, source.Count / 2)); stack.Push((source.Count / 2, source.Count - 1)); }
        else stack.Push((0, source.Count - 1));

        while (stack.Count > 0)
        {
            (int first, int last) = stack.Pop();
            float worst = 0f;
            int worstIndex = -1;
            for (int i = first + 1; i < last; i++)
            {
                float d = DistanceToSegment(source[i], source[first], source[last]);
                if (d > worst) { worst = d; worstIndex = i; }
            }
            if (worstIndex < 0 || worst <= tolerance) continue;
            keep[worstIndex] = true;
            stack.Push((first, worstIndex));
            stack.Push((worstIndex, last));
        }

        var result = new List<Vector3>();
        int count = closed ? source.Count - 1 : source.Count;
        for (int i = 0; i < count; i++) if (keep[i]) result.Add(source[i]);
        return result;
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSq);
        return Vector3.Distance(p, a + ab * t);
    }
}
