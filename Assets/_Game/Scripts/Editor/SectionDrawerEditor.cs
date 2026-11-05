using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

// Inspector for SectionDrawer. "Preview" traces the image (same tracer, scale, forks and dots as the Level Drawer),
// turns each drawn line into a clean 45°-grid line and builds the road from the library's sections along it; the
// result is drawn as sections in the Scene view, with warnings for whatever the library couldn't draw.
// "Draw Road" writes those sections into the level's SplineRoad (exact arcs and straights), "Place Objects" puts
// islands / jump pads on their dots. Closed loops can't be built from sections and are skipped (use the Level Drawer).
[CustomEditor(typeof(SectionDrawer))]
public class SectionDrawerEditor : Editor
{
    private string summary;

    // A split and a merge with the same two arms: one symmetric fork.
    private class ForkPair
    {
        public SectionForks.Fork split, merge;
        public int left, right;      // path indices of the arms
        public string problem;       // why it can't be mirrored (null = it can)
        public float shift;          // metres the road after the fork was moved sideways
        public bool fromPoints;      // the arm was built from points because the drawing's form didn't fit sections
    }

    // `count` points evenly spread along the line's length.
    private static List<Vector2> ResampleByCount(List<Vector2> line, int count)
    {
        var result = new List<Vector2>(count);
        if (line.Count < 2) { for (int i = 0; i < count; i++) result.Add(line.Count > 0 ? line[0] : Vector2.zero); return result; }
        var length = new float[line.Count];
        for (int i = 1; i < line.Count; i++) length[i] = length[i - 1] + Vector2.Distance(line[i - 1], line[i]);
        int seg = 1;
        for (int k = 0; k < count; k++)
        {
            float at = length[line.Count - 1] * k / (count - 1);
            while (seg < line.Count - 1 && length[seg] < at) seg++;
            float span = length[seg] - length[seg - 1];
            result.Add(Vector2.Lerp(line[seg - 1], line[seg], span > 1e-5f ? Mathf.Clamp01((at - length[seg - 1]) / span) : 0f));
        }
        return result;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, LevelDrawerEditor.HiddenFields((LevelDrawer)target, "knotTolerance", "forkJoinDistance"));
        serializedObject.ApplyModifiedProperties();

        var drawer = (SectionDrawer)target;
        EditorGUILayout.Space();

        SplineRoad road = drawer.FindRoad();
        if (drawer.roadImage == null) EditorGUILayout.HelpBox("Assign a road image.", MessageType.Info);
        if (road == null) EditorGUILayout.HelpBox("No SplineRoad found in this level — assign one.", MessageType.Warning);
        else
        {
            EditorGUILayout.HelpBox($"Road width {drawer.roadWidth:0.##} m: sections are scaled ×{drawer.roadWidth / SectionDef.BaseRoadWidth:0.##}" +
                                    (Mathf.Abs(road.DefaultWidth - drawer.roadWidth) > 0.05f
                                        ? $" (the road's own default width is {road.DefaultWidth:0.##} m — Draw Road sets this width on the splines)."
                                        : " (the road's default width)."), MessageType.None);
        }

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
            using (new EditorGUI.DisabledScope(!LevelDrawerEditor.CanPlaceObjects(drawer)))
                if (GUILayout.Button(new GUIContent("Place Objects",
                        "Islands on brown dots, jump pads on yellow dots, the finish platform to the red dot. Replaces what the last placing made.")))
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
        float y = drawer.roadHeight, width = drawing.roadWidth;
        float sectionScale = width / SectionDef.BaseRoadWidth; // the sections shrink / grow with the road
        float lead = width * 1.2f; // straight bit an arm runs sideways before it may turn / after it has turned back in
        int count = drawing.smoothed.Count;

        var points = new List<List<Vector3>>();
        var closed = new List<bool>();
        foreach (LevelDrawerEditor.DrawnPath path in drawing.smoothed) { points.Add(path.points); closed.Add(path.closed); }
        List<SectionForks.Fork> forks = SectionForks.Find(points, closed, drawing.joinDistance);

        // Which fork (and which of its arms) each line's start / end belongs to.
        var startFork = new SectionForks.Fork[count];
        var startSlot = new float[count];
        var endFork = new SectionForks.Fork[count];
        var endSlot = new float[count];
        foreach (SectionForks.Fork fork in forks)
            for (int i = 0; i < fork.arms.Count; i++)
            {
                if (fork.split) { startFork[fork.arms[i]] = fork; startSlot[fork.arms[i]] = fork.slots[i]; }
                else { endFork[fork.arms[i]] = fork; endSlot[fork.arms[i]] = fork.slots[i]; }
            }

        // A split and a merge that share the same two arms are one symmetric fork: road -> two arms -> road.
        var pairs = new List<ForkPair>();
        var pairArm = new ForkPair[count];
        var pairRoad = new ForkPair[count]; // the road after a paired fork
        foreach (SectionForks.Fork split in forks)
        {
            if (!split.split || split.arms.Count != 2) continue;
            foreach (SectionForks.Fork merge in forks)
            {
                if (merge.split || merge.arms.Count != 2) continue;
                bool same = (merge.arms[0] == split.arms[0] && merge.arms[1] == split.arms[1]) ||
                            (merge.arms[0] == split.arms[1] && merge.arms[1] == split.arms[0]);
                if (!same) continue;
                var pair = new ForkPair { split = split, merge = merge, left = split.arms[0], right = split.arms[1] };
                pairs.Add(pair);
                pairArm[pair.left] = pairArm[pair.right] = pair;
                pairRoad[merge.through] = pair;
                break;
            }
        }

        var chains = new SectionDrawer.Chain[count];
        var targets = new List<Vector2>[count];

        Vector2 RightOf(Vector2 v) => new Vector2(v.y, -v.x);

        // A merge's single road starts where it was drawn, so its start edge is known up front.
        Vector2 StartEdge(SectionForks.Fork fork, out Vector2 direction)
        {
            List<Vector2> baseTarget = SectionMatcher.BuildTarget(points[fork.through], offset, out float yaw, width);
            direction = baseTarget != null ? SectionDef.Forward(yaw) : fork.direction;
            return baseTarget != null ? baseTarget[0] : fork.position;
        }
        // A split's single road ends where its sections really end (within half a width of where it was drawn).
        Vector2 EndEdge(SectionForks.Fork fork, bool nominal, out Vector2 direction)
        {
            SectionDrawer.Chain chain = chains[fork.through];
            if (!nominal && chain != null && chain.sections.Count > 0)
            {
                SectionDrawer.PlacedSection last = chain.sections[chain.sections.Count - 1];
                SectionDef.Sample exit = last.samples[last.samples.Count - 1];
                direction = SectionDef.Forward(exit.yaw);
                return exit.position;
            }
            direction = fork.direction;
            return fork.position;
        }

        SectionDrawer.Chain MatchLine(List<Vector3> line, int number, List<string> sink, out List<Vector2> target)
        {
            target = SectionMatcher.BuildTarget(line, offset, out float startYaw, width);
            if (target == null) return null;
            return SectionMatcher.Match(target, startYaw, drawer.library, sectionScale, width, drawer.flagDeviation, number, sink);
        }

        // The road after a paired fork is moved sideways onto the centre line of the road before it, so the fork can be
        // mirrored. Not possible (runs another way, too close, too far off): the fork's arms are built one by one instead.
        List<Vector3> AlignRoad(ForkPair pair, List<Vector3> line)
        {
            Vector2 face = EndEdge(pair.split, false, out Vector2 dir);
            List<Vector2> probe = SectionMatcher.BuildTarget(line, offset, out float yaw, width);
            if (probe == null) { pair.problem = "the road after it is too short"; return line; }
            float lateral = Vector2.Dot(probe[0] - face, RightOf(dir)), ahead = Vector2.Dot(probe[0] - face, dir);
            if (Vector2.Angle(SectionDef.Forward(yaw), dir) > 10f) pair.problem = "the road after it doesn't run the same way as the road before it";
            else if (ahead < width) pair.problem = "the road after it starts too close to the road before it";
            else if (Mathf.Abs(lateral) > width) pair.problem = $"the road after it is {Mathf.Abs(lateral):0.#} m off the road before it's centre line";
            if (pair.problem != null) return line;

            pair.shift = lateral;
            Vector2 right = RightOf(dir);
            var shifted = new List<Vector3>(line.Count);
            foreach (Vector3 p in line) shifted.Add(p - new Vector3(right.x, 0f, right.y) * lateral);
            return shifted;
        }

        // Fork = the road before (its real end) -> left arm -> the road after (its real start), the right arm is the left
        // one mirrored. The drawing is only a reference for the arms' form: both drawn arms are folded onto one side and
        // averaged, stretched to the real distance between the two roads. If the sections can't follow that form, the arm
        // is built from a few points instead (out, along, back) - a plain rectangle.
        void BuildPair(ForkPair pair)
        {
            Vector2 face = EndEdge(pair.split, false, out Vector2 dir);
            Vector2 right = RightOf(dir);
            float axisYaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            SectionDrawer.Chain after = chains[pair.merge.through];
            float distance = after != null && after.sections.Count > 0
                ? Vector2.Dot(after.sections[0].samples[0].position - face, dir)
                : Vector2.Dot(pair.merge.position - pair.split.position, dir);
            Vector2 afterFace = face + dir * distance;

            // The form: both drawn arms in the drawn road's frame, folded to the left, averaged.
            Vector2 drawnFace = pair.split.position, drawnDir = pair.split.direction, drawnRight = RightOf(drawnDir);
            float drawnDistance = Vector2.Dot(pair.merge.position - drawnFace, drawnDir);
            float stretch = drawnDistance > 1f ? Mathf.Clamp(distance / drawnDistance, 0.25f, 4f) : 1f;
            List<Vector2> Fold(List<Vector3> line)
            {
                var folded = new List<Vector2>(line.Count);
                foreach (Vector3 p in line)
                {
                    Vector2 q = new Vector2(p.x, p.z) - drawnFace;
                    folded.Add(new Vector2(-Mathf.Abs(Vector2.Dot(q, drawnRight)), Vector2.Dot(q, drawnDir) * stretch));
                }
                return ResampleByCount(folded, 48);
            }
            List<Vector2> a = Fold(points[pair.left]), b = Fold(points[pair.right]);
            var form = new List<Vector2>(a.Count);
            // How far out the arms go: as drawn, but room for a short 90° corner after leaving the road's side.
            float extent = width * 0.5f + SectionDef.ShortTurnRadius * sectionScale * 1.25f;
            for (int k = 0; k < a.Count; k++)
            {
                form.Add((a[k] + b[k]) * 0.5f);
                extent = Mathf.Max(extent, -form[k].x);
            }
            Vector3 World(Vector2 uv) => new Vector3(face.x + right.x * uv.x + dir.x * uv.y, y, face.y + right.y * uv.x + dir.y * uv.y);

            SectionForks.ArmStart(face, dir, -1f, width, out Vector2 start, out Vector2 startHeading);
            SectionForks.ArmEnd(afterFace, dir, -1f, width, out Vector2 end, out Vector2 endHeading);

            SectionDrawer.Chain chain = null;
            List<Vector2> target = null;
            List<string> keep = null;
            bool haveClean = false;
            void Consider(SectionDrawer.Chain candidate, List<Vector2> candidateTarget, List<string> candidateWarnings, bool fromPoints)
            {
                if (candidate == null || haveClean) return;
                bool clean = candidate.complete && candidateWarnings.Count == 0;
                if (chain != null && !clean && !(candidate.complete && !chain.complete)) return;
                chain = candidate; target = candidateTarget; keep = candidateWarnings; pair.fromPoints = fromPoints; haveClean = clean;
            }

            // The half fork from points: out from the road's side, along, back in. 1st try: a rectangle with the corners
            // already rounded to the short 90° turn (what the library draws), so the sections follow it exactly.
            float half = width * 0.5f;
            float corner = SectionDef.ShortTurnRadius * sectionScale;

            // Best try: the arm is symmetric end to end as well. Only its first half (out from the road before the fork, round
            // the corner, up to the middle) is matched; an exact straight bridges the middle; the second half is the first half
            // mirrored and reversed. So the arm ends exactly where it should - on the side of the road after the fork - just
            // like it starts exactly on the side of the road before.
            if (distance >= corner * 2f + 0.5f)
            {
                var uv = new List<Vector2> { new Vector2(-half, -half) };
                Vector2 c1 = new Vector2(-extent + corner, -half + corner);
                for (int s = 0; s <= 16; s++)
                {
                    float t = (-90f - 90f * s / 16f) * Mathf.Deg2Rad;
                    uv.Add(c1 + corner * new Vector2(Mathf.Cos(t), Mathf.Sin(t)));
                }
                uv.Add(new Vector2(-extent, distance * 0.5f - half)); // the match may end up to half a width past this: still before the middle
                var quarter = new List<Vector3>();
                foreach (Vector2 p in uv) quarter.Add(World(p));
                var quarterTarget = new List<Vector2>();
                foreach (Vector3 p in LevelDrawerEditor.Resample(quarter, 1f, false)) quarterTarget.Add(new Vector2(p.x, p.z));
                var quarterWarnings = new List<string>();
                SectionDrawer.Chain half1 = SectionMatcher.Match(quarterTarget, axisYaw - 90f, drawer.library, sectionScale, width,
                                                                 drawer.flagDeviation, pair.left + 1, quarterWarnings);
                if (half1 != null && half1.complete && quarterWarnings.Count == 0)
                {
                    // Keep sections up to the last one that leaves the road heading along the fork (the corner is done).
                    while (half1.sections.Count > 0)
                    {
                        SectionDrawer.PlacedSection tail = half1.sections[half1.sections.Count - 1];
                        if (Mathf.Abs(Mathf.DeltaAngle(tail.samples[tail.samples.Count - 1].yaw, axisYaw)) < 1f) break;
                        half1.sections.RemoveAt(half1.sections.Count - 1);
                    }
                    if (half1.sections.Count > 0)
                    {
                        SectionDrawer.PlacedSection tail = half1.sections[half1.sections.Count - 1];
                        Vector2 turnEnd = tail.samples[tail.samples.Count - 1].position;
                        float along = Vector2.Dot(turnEnd - face, dir);
                        float bridge = distance - 2f * along; // exact straight between the halves
                        if (bridge >= 0f)
                        {
                            var whole = new SectionDrawer.Chain
                            {
                                number = pair.left + 1, complete = true, endGap = 0f,
                                meanError = half1.meanError, maxError = half1.maxError,
                                uncovered = new List<Vector2>(),
                            };
                            whole.sections.AddRange(half1.sections);
                            if (bridge > 0.05f)
                            {
                                var straight = new SectionDef
                                {
                                    name = $"Straight {bridge:0.#} m", scale = sectionScale,
                                    pieces = "S" + bridge.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                                };
                                var placed = new SectionDrawer.PlacedSection { def = straight, position = turnEnd, yaw = axisYaw };
                                foreach (SectionDef.Sample local in straight.Samples)
                                    placed.samples.Add(new SectionDef.Sample { position = turnEnd + local.position.y * dir, yaw = axisYaw });
                                whole.sections.Add(placed);
                            }
                            Vector2 middle = face + dir * (along + bridge * 0.5f);
                            whole.sections.AddRange(SectionMatcher.Unfold(half1.sections, middle, dir, axisYaw, drawer.library));

                            // Shown line: the whole rounded rectangle.
                            var full = new List<Vector2>(quarterTarget);
                            for (int n = quarterTarget.Count - 1; n >= 0; n--)
                                full.Add(quarterTarget[n] - 2f * Vector2.Dot(quarterTarget[n] - middle, dir) * dir);
                            whole.target = full;
                            Consider(whole, full, quarterWarnings, true);
                        }
                    }
                }
            }

            if (!haveClean && distance + width >= corner * 2f)
            {
                var uv = new List<Vector2> { new Vector2(-half, -half) };
                Vector2 c1 = new Vector2(-extent + corner, -half + corner), c2 = new Vector2(-extent + corner, distance + half - corner);
                for (int s = 0; s <= 16; s++)
                {
                    float t = (-90f - 90f * s / 16f) * Mathf.Deg2Rad;
                    uv.Add(c1 + corner * new Vector2(Mathf.Cos(t), Mathf.Sin(t)));
                }
                for (int s = 0; s <= 16; s++)
                {
                    float t = (180f - 90f * s / 16f) * Mathf.Deg2Rad;
                    uv.Add(c2 + corner * new Vector2(Mathf.Cos(t), Mathf.Sin(t)));
                }
                uv.Add(new Vector2(-half, distance + half));
                var rounded = new List<Vector3>();
                foreach (Vector2 p in uv) rounded.Add(World(p));
                var roundedTarget = new List<Vector2>();
                foreach (Vector3 p in LevelDrawerEditor.Resample(rounded, 1f, false)) roundedTarget.Add(new Vector2(p.x, p.z));
                var roundedWarnings = new List<string>();
                SectionDrawer.Chain first = SectionMatcher.Match(roundedTarget, axisYaw - 90f, drawer.library, sectionScale, width,
                                                                 drawer.flagDeviation, pair.left + 1, roundedWarnings);
                Consider(first, roundedTarget, roundedWarnings, true);
            }

            // 2nd try: the same with sharp corners.
            if (!haveClean)
            {
                var sharp = new List<Vector3>
                {
                    World(new Vector2(-half, -half)), World(new Vector2(-extent, -half)),
                    World(new Vector2(-extent, distance + half)), World(new Vector2(-half, distance + half)),
                };
                var sharpWarnings = new List<string>();
                SectionDrawer.Chain second = MatchLine(sharp, pair.left + 1, sharpWarnings, out List<Vector2> sharpTarget);
                Consider(second, sharpTarget, sharpWarnings, true);
            }

            // 3rd try: the averaged drawing.
            if (!haveClean)
            {
                var drawn = new List<Vector3>();
                foreach (Vector2 uv in form) drawn.Add(World(uv));
                drawn = SectionForks.LeadIn(drawn, start, startHeading, lead);
                drawn = SectionForks.LeadOut(drawn, end, endHeading, lead);
                var drawnWarnings = new List<string>();
                SectionDrawer.Chain third = MatchLine(drawn, pair.left + 1, drawnWarnings, out List<Vector2> drawnTarget);
                Consider(third, drawnTarget, drawnWarnings, false);
            }

            drawer.warnings.AddRange(keep);
            if (chain == null) { drawer.warnings.Add($"Fork: the arms are too short to build."); return; }
            chains[pair.left] = chain;
            targets[pair.left] = target;
            chains[pair.right] = SectionMatcher.Mirror(chain, face, axisYaw, pair.right + 1, drawer.library);
            targets[pair.right] = chains[pair.right].target;
        }

        void BuildLine(int i, bool nominal)
        {
            List<Vector3> line = points[i];
            if (pairRoad[i] != null && !nominal) line = AlignRoad(pairRoad[i], line);
            SectionForks.Fork sf = startFork[i], ef = endFork[i];
            if (sf != null)
            {
                Vector2 face = EndEdge(sf, nominal, out Vector2 dir);
                SectionForks.ArmStart(face, dir, startSlot[i], width, out Vector2 start, out Vector2 heading);
                line = SectionForks.LeadIn(line, start, heading, lead);
            }
            if (ef != null)
            {
                Vector2 face = StartEdge(ef, out Vector2 dir);
                SectionForks.ArmEnd(face, dir, endSlot[i], width, out Vector2 end, out Vector2 heading);
                line = SectionForks.LeadOut(line, end, heading, lead);
            }

            var sink = new List<string>();
            SectionDrawer.Chain chain = MatchLine(line, i + 1, sink, out List<Vector2> target);
            ForkPair roadAfter = pairRoad[i];
            if (chain != null && roadAfter != null && roadAfter.problem == null && !nominal && chain.sections.Count > 0)
            {
                // The sections may start a little off the line they were matched to: shift once more so the road
                // really starts on the centre line of the road before the fork.
                Vector2 face = EndEdge(roadAfter.split, false, out Vector2 dir);
                float error = Vector2.Dot(chain.sections[0].samples[0].position - face, RightOf(dir));
                if (Mathf.Abs(error) > 0.1f)
                {
                    var moved = new List<Vector3>(line.Count);
                    Vector2 right = RightOf(dir);
                    foreach (Vector3 p in line) moved.Add(p - new Vector3(right.x, 0f, right.y) * error);
                    sink = new List<string>();
                    chain = MatchLine(moved, i + 1, sink, out target);
                }
            }
            drawer.warnings.AddRange(sink);
            if (chain == null) { drawer.warnings.Add($"Spline {i + 1}: too short to build."); return; }
            targets[i] = target;
            chains[i] = chain;
        }

        // Roads first; a fork's arms once the road before it and the road after it are built.
        var done = new bool[count];
        for (int i = 0; i < count; i++) if (closed[i]) done[i] = true;
        bool Ready(int i)
        {
            ForkPair pa = pairArm[i];
            if (pa != null) return done[pa.split.through] && done[pa.merge.through];
            if (startFork[i] != null && !done[startFork[i].through]) return false;
            if (pairRoad[i] != null && !done[pairRoad[i].split.through]) return false;
            return true;
        }
        for (bool progress = true; progress;)
        {
            progress = false;
            for (int i = 0; i < count; i++)
            {
                if (done[i] || !Ready(i)) continue;
                ForkPair pa = pairArm[i];
                if (pa != null && pa.problem == null)
                {
                    BuildPair(pa);
                    done[pa.left] = done[pa.right] = true;
                }
                else
                {
                    BuildLine(i, false);
                    done[i] = true;
                }
                progress = true;
            }
        }
        for (int i = 0; i < count; i++) if (!done[i]) BuildLine(i, true); // roads that wait on each other
        foreach (ForkPair pair in pairs)
            if (pair.problem != null)
                drawer.warnings.Add($"Fork at spline {pair.split.through + 1}: arms built one by one, not symmetric - {pair.problem}.");

        drawer.chains = new List<SectionDrawer.Chain>();
        drawer.previewPaths = new List<Vector3[]>();
        drawer.previewClosed = new List<bool>();
        int skipped = 0;
        for (int i = 0; i < count; i++)
        {
            if (closed[i]) { skipped++; continue; }
            if (chains[i] == null) continue;
            drawer.chains.Add(chains[i]);
            var line = new Vector3[targets[i].Count];
            for (int k = 0; k < line.Length; k++) line[k] = new Vector3(targets[i][k].x, y, targets[i][k].y);
            drawer.previewPaths.Add(line);
            drawer.previewClosed.Add(false);
        }
        int forkCount = forks.Count;
        int symmetric = 0;
        foreach (ForkPair pair in pairs) if (pair.problem == null) symmetric++;
        if (skipped > 0) drawer.warnings.Add($"{skipped} closed loop(s) skipped - sections can't build loops. Use the Level Drawer for those.");

        drawer.previewStart = drawing.start;
        drawer.previewFinish = drawing.finish;
        drawer.previewIslands = drawing.islands;
        drawer.previewJumpPads = drawing.jumpPads;
        SceneView.RepaintAll();

        var text = new System.Text.StringBuilder(drawing.summary);
        text.Append($"\nGrid turned {offset:0.#}° to the drawing.   Forks: {forkCount} ({symmetric} symmetric: built from the road before to the road after, the right arm mirrors the left)\nRoad width {width:0.##} m, sections scaled ×{sectionScale:0.##}");
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
        summary = Preview(drawer, out LevelDrawerEditor.Drawing drawing);
        List<LevelDrawerEditor.DrawnPath> paths = ChainPaths(drawer);
        if (paths.Count == 0) return;
        if (LevelDrawerEditor.WriteRoad(drawer, road, paths, drawing.roadWidth)) summary = Preview(drawer, out _);
    }

    // The previewed chains as spline paths (knots from the sections' own tangents).
    private static List<LevelDrawerEditor.DrawnPath> ChainPaths(SectionDrawer drawer)
    {
        var paths = new List<LevelDrawerEditor.DrawnPath>();
        if (drawer.chains == null) return paths;
        foreach (SectionDrawer.Chain chain in drawer.chains)
        {
            if (chain.sections.Count == 0) continue;
            List<SectionMatcher.Knot> knots = SectionMatcher.BuildKnots(chain, drawer.roadHeight);
            var points = new List<Vector3>();
            foreach (SectionMatcher.Knot knot in knots) points.Add(knot.position);
            paths.Add(new LevelDrawerEditor.DrawnPath { points = points, closed = false, knots = knots });
        }
        return paths;
    }

    /// <summary>Draw Road + Place Objects in one go, for scripts (batch level making): replaces the road's splines and the
    /// objects placed last time WITHOUT asking. The level must be open as a prefab so the road can bake. Returns the report.</summary>
    public static string DrawAndPlace(SectionDrawer drawer)
    {
        SplineRoad road = drawer.FindRoad();
        if (road == null) return "No SplineRoad in the level.";
        string text = Preview(drawer, out LevelDrawerEditor.Drawing drawing);
        List<LevelDrawerEditor.DrawnPath> paths = ChainPaths(drawer);
        if (paths.Count == 0) return text + "\nNothing drawn.";

        while (road.Container.Splines.Count > 0) road.Container.RemoveSplineAt(0); // no "replace?" question
        foreach (GameObject old in drawer.placedObjects) if (old != null) Object.DestroyImmediate(old);
        drawer.placedObjects.Clear();

        if (!LevelDrawerEditor.WriteRoad(drawer, road, paths, drawing.roadWidth)) return text + "\nRoad not written.";
        text = Preview(drawer, out drawing);
        string placed = LevelDrawerEditor.PlaceObjects(drawer, drawing);
        if (drawer.warnings != null && drawer.warnings.Count > 0) text += "\nWARNINGS: " + string.Join(" | ", drawer.warnings);
        return text + "\n" + placed;
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
