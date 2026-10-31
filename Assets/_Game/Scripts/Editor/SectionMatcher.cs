using System.Collections.Generic;
using UnityEngine;

// Builds one drawn line out of library sections. Two steps:
//  1. Clean line: the drawing's heading is snapped to the 45° grid (the grid is turned to the drawing's own, see
//     GridOffset), runs of one heading become straight lines, neighbouring lines meet at corners. Wobble — tiny runs
//     and short jogs that return to the same heading — is dropped. That polygon is what the road is matched to.
//  2. Search: a beam search grows the road one whole section at a time. Every partial road tries every library
//     section; a try costs the squared distance of its centre line to the clean line, a heading penalty (a section
//     must run the way the line runs), a little per section and per turn (so long straights beat many short ones and
//     no turn is made for nothing). Only the best few partial roads survive each round.
// Sections that stray too far, or a drawing the library ran out of sections for, are reported — nothing is hidden.
public static class SectionMatcher
{
    public struct Knot
    {
        public Vector3 position, tangentIn, tangentOut; // tangents relative to the position
    }

    private const int BeamWidth = 40;
    private const float Hysteresis = 7f;
    private const float MinRun = 4f;
    private const float MaxJog = 12f;
    private const float ArcHandle = 0.26521f; // 4/3 * tan(45°/4): Bezier handle of a 45° arc, in radii

    private class Node
    {
        public Vector2 position;
        public float yaw;
        public float cost;
        public int index;
        public Node parent;
        public SectionDef def;
    }

    // ---------- 1. The clean line ----------

    /// <summary>The drawing's own 45° grid, in degrees (-45..45): headings cluster around multiples of 45° from it.
    /// Taken over all the drawn lines together, so a stem and its arms share one grid.</summary>
    public static float GridOffset(IEnumerable<List<Vector3>> paths)
    {
        float sin = 0f, cos = 0f;
        foreach (List<Vector3> path in paths)
        {
            List<Vector3> line = LevelDrawerEditor.Resample(path, 1f, false);
            for (int i = 0; i < line.Count; i++)
            {
                float theta = Heading(line, i) * 4f * Mathf.Deg2Rad;
                sin += Mathf.Sin(theta);
                cos += Mathf.Cos(theta);
            }
        }
        return Mathf.Atan2(sin, cos) * Mathf.Rad2Deg / 4f;
    }

    private struct Run { public int q, first, last; }

    /// <summary>The clean line for one drawn path, every 1 m. Null when the path is too short.</summary>
    public static List<Vector2> BuildTarget(List<Vector3> path, float offset, out float startYaw)
    {
        startYaw = offset;
        List<Vector3> line = LevelDrawerEditor.Resample(path, 1f, false);
        int n = line.Count;
        if (n < 4) return null;

        var q = new int[n];
        int current = Mathf.RoundToInt(Mathf.DeltaAngle(offset, Heading(line, 0)) / 45f);
        for (int i = 0; i < n; i++)
        {
            float diff = Mathf.DeltaAngle(offset + current * 45f, Heading(line, i));
            if (Mathf.Abs(diff) > 22.5f + Hysteresis) current += Mathf.RoundToInt(diff / 45f);
            q[i] = current;
        }

        var runs = new List<Run>();
        int first = 0;
        for (int i = 1; i <= n; i++)
            if (i == n || q[i] != q[first])
            {
                runs.Add(new Run { q = q[first], first = first, last = i - 1 });
                first = i;
            }

        // Wobble: tiny runs, and short jogs that come back to the heading they left.
        for (bool again = true; again;)
        {
            again = false;
            for (int i = 1; i < runs.Count - 1; i++)
            {
                int length = runs[i].last - runs[i].first + 1;
                bool jog = length < MaxJog && runs[i - 1].q == runs[i + 1].q;
                if (length >= MinRun && !jog) continue;
                Run before = runs[i - 1], after = runs[i + 1];
                runs.RemoveAt(i);
                if (before.q == after.q)
                {
                    runs[i - 1] = new Run { q = before.q, first = before.first, last = after.last };
                    runs.RemoveAt(i);
                }
                again = true;
                break;
            }
        }

        var lines = new List<(Vector2 normal, float offset)>();
        foreach (Run run in runs)
        {
            Vector2 normal = SectionDef.Right(offset + run.q * 45f);
            int quarter = (run.last - run.first) / 4;
            int from = run.first + quarter, to = run.last - quarter;
            if (to < from) { from = run.first; to = run.last; }
            float sum = 0f;
            for (int i = from; i <= to; i++) sum += Vector2.Dot(normal, Flat(line[i]));
            lines.Add((normal, sum / (to - from + 1)));
        }

        var vertices = new List<Vector2> { Drop(Flat(line[0]), lines[0]) };
        for (int i = 1; i < runs.Count; i++)
        {
            if (!Cross(lines[i - 1], lines[i], out Vector2 meet))
                meet = (Flat(line[runs[i - 1].last]) + Flat(line[runs[i].first])) * 0.5f;
            vertices.Add(meet);
        }
        vertices.Add(Drop(Flat(line[n - 1]), lines[lines.Count - 1]));

        var target = new List<Vector2> { vertices[0] };
        for (int i = 1; i < vertices.Count; i++)
        {
            Vector2 a = vertices[i - 1], b = vertices[i];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int s = 1; s <= steps; s++) target.Add(Vector2.Lerp(a, b, s / (float)steps));
        }
        startYaw = offset + runs[0].q * 45f;
        return target;
    }

    private static float Heading(List<Vector3> line, int i)
    {
        Vector3 d = line[Mathf.Min(i + 2, line.Count - 1)] - line[Mathf.Max(i - 2, 0)];
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    private static bool Cross((Vector2 normal, float offset) a, (Vector2 normal, float offset) b, out Vector2 point)
    {
        float det = a.normal.x * b.normal.y - a.normal.y * b.normal.x;
        point = default;
        if (Mathf.Abs(det) < 0.3f) return false; // parallel-ish: a U-turn or no turn at all
        point = new Vector2((a.offset * b.normal.y - b.offset * a.normal.y) / det,
                            (a.normal.x * b.offset - b.normal.x * a.offset) / det);
        return true;
    }

    private static Vector2 Drop(Vector2 p, (Vector2 normal, float offset) line) =>
        p - line.normal * (Vector2.Dot(line.normal, p) - line.offset);

    private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    // ---------- 2. Library sections along it ----------

    public static SectionDrawer.Chain Match(List<Vector2> target, float startYaw, IList<SectionDef> library,
                                            float scale, float roadWidth, float flagDeviation, int number, List<string> warnings)
    {
        int n = target.Count;
        var chain = new SectionDrawer.Chain { number = number, target = target, uncovered = new List<Vector2>() };

        var defs = new List<SectionDef>();
        var seen = new HashSet<string>();
        foreach (SectionDef def in library)
            if (def != null && def.IsValid && Mathf.Abs(def.scale - scale) < 0.05f && seen.Add(def.pieces.Trim())) defs.Add(def);
        if (defs.Count == 0)
        {
            warnings.Add($"Spline {number}: the library has no usable section of scale {scale} — press Fill Sample Library.");
            chain.uncovered = new List<Vector2>(target);
            return chain;
        }

        var turns = new Dictionary<SectionDef, int>();
        float shortest = float.MaxValue;
        foreach (SectionDef def in defs)
        {
            int count = 0;
            foreach (SectionDef.PieceInfo piece in def.ParsePieces()) if (piece.IsArc) count++;
            turns[def] = count;
            shortest = Mathf.Min(shortest, def.Length);
        }

        var tangent = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 d = target[Mathf.Min(i + 3, n - 1)] - target[Mathf.Max(i - 3, 0)];
            tangent[i] = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        float k = roadWidth * roadWidth * 0.25f;     // deviating half a road width costs as much as 1 m of not moving on
        float stopDistance = roadWidth * 0.5f;
        int nearEnd = n - 1 - Mathf.CeilToInt(roadWidth * 1.5f);
        Vector2 endPoint = target[n - 1];

        var beam = new List<Node> { new Node { position = target[0], yaw = startYaw } };
        Node best = null, partial = null;
        float bestScore = float.MaxValue;
        int rounds = Mathf.CeilToInt(n / (shortest * 0.5f)) + 20;

        for (int round = 0; round < rounds && beam.Count > 0; round++)
        {
            var next = new Dictionary<long, Node>();
            foreach (Node node in beam)
                foreach (SectionDef def in defs)
                {
                    Node child = Extend(node, def, turns[def], target, tangent, k, roadWidth);
                    if (child == null) continue;
                    if (partial == null || child.index > partial.index || (child.index == partial.index && child.cost < partial.cost))
                        partial = child;

                    float gap = Vector2.Distance(child.position, endPoint);
                    if ((gap <= stopDistance && child.index >= nearEnd) || child.index >= n - 2)
                    {
                        float score = child.cost + k * gap;
                        if (score < bestScore) { bestScore = score; best = child; }
                        continue;
                    }

                    long key = Key(child);
                    if (!next.TryGetValue(key, out Node other) || Rank(other, n, k) > Rank(child, n, k)) next[key] = child;
                }

            beam = new List<Node>(next.Values);
            if (beam.Count > BeamWidth)
            {
                beam.Sort((a, b) => Rank(a, n, k).CompareTo(Rank(b, n, k)));
                beam.RemoveRange(BeamWidth, beam.Count - BeamWidth);
            }
        }

        chain.complete = best != null;
        Node last = best ?? partial;
        if (last != null)
        {
            var nodes = new List<Node>();
            for (Node node = last; node.parent != null; node = node.parent) nodes.Add(node);
            nodes.Reverse();
            foreach (Node node in nodes) chain.sections.Add(Place(node));
        }

        float flag = flagDeviation > 0f ? flagDeviation : roadWidth * 0.6f;
        Measure(chain, target, flag);

        int covered = last == null ? 0 : last.index;
        if (!chain.complete)
        {
            for (int i = Mathf.Max(0, covered); i < n; i++) chain.uncovered.Add(target[i]);
            warnings.Add($"Spline {number}: the library ran out of sections after section {chain.sections.Count} — " +
                         $"{n - 1 - covered:0} m of the drawing is not covered (shown in red). No library section fits the drawing there: " +
                         "add a section that bends that way, or redraw it straighter.");
        }
        else
        {
            chain.endGap = Vector2.Distance(last.position, endPoint);
            if (chain.endGap > roadWidth * 0.6f)
                warnings.Add($"Spline {number}: the road ends {chain.endGap:0.#} m short of / past the drawing's end — no section length lands closer.");
        }
        for (int i = 0; i < chain.sections.Count; i++)
            if (chain.sections[i].flagged)
                warnings.Add($"Spline {number}, section {i + 1} \"{chain.sections[i].def.name}\": up to {chain.sections[i].maxDeviation:0.#} m " +
                             "from the drawing (shown in red) — the library has nothing closer for this part.");
        return chain;
    }

    private static float Rank(Node node, int n, float k) => node.cost + k * (n - 1 - node.index);

    private static long Key(Node node)
    {
        long x = Mathf.RoundToInt(node.position.x / 2f), z = Mathf.RoundToInt(node.position.y / 2f);
        long yaw = Mathf.RoundToInt(Mathf.Repeat(node.yaw, 360f) / 15f);
        return (x * 73856093L) ^ (z * 19349663L) ^ (yaw * 83492791L);
    }

    // One more whole section on a partial road: the new node, or null when it strays too far / goes nowhere.
    private static Node Extend(Node from, SectionDef def, int turnPieces, List<Vector2> target, float[] tangent, float k, float roadWidth)
    {
        int n = target.Count;
        IReadOnlyList<SectionDef.Sample> samples = def.Samples;
        Vector2 forward = SectionDef.Forward(from.yaw), right = SectionDef.Right(from.yaw);

        float cost = from.cost + k * 4f + k * 2f * turnPieces;
        float limit = roadWidth * 2.5f;
        int index = from.index;
        Vector2 position = from.position;
        float yaw = from.yaw;
        Vector2 previous = from.position;

        for (int s = 1; s < samples.Count; s++)
        {
            position = from.position + samples[s].position.x * right + samples[s].position.y * forward;
            yaw = from.yaw + samples[s].yaw;
            float step = Vector2.Distance(position, previous);
            previous = position;

            int lo = Mathf.Max(0, index - 3), hi = Mathf.Min(n - 1, index + 10);
            float bestSq = float.MaxValue;
            int bestIndex = lo;
            for (int i = lo; i <= hi; i++)
            {
                float sq = (target[i] - position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; bestIndex = i; }
            }
            if (bestSq > limit * limit) return null;

            float off = Mathf.Abs(Mathf.DeltaAngle(yaw, tangent[bestIndex])) / 45f;
            cost += (bestSq + k * 2f * off * off) * step;
            index = Mathf.Max(index, bestIndex);
        }

        if (index < from.index + 0.3f * def.Length && index < n - 3) return null; // has to get somewhere
        return new Node { position = position, yaw = yaw, cost = cost, index = index, parent = from, def = def };
    }

    private static SectionDrawer.PlacedSection Place(Node node)
    {
        Node from = node.parent;
        var placed = new SectionDrawer.PlacedSection { def = node.def, position = from.position, yaw = from.yaw };
        Vector2 forward = SectionDef.Forward(from.yaw), right = SectionDef.Right(from.yaw);
        foreach (SectionDef.Sample local in node.def.Samples)
            placed.samples.Add(new SectionDef.Sample
            {
                position = from.position + local.position.x * right + local.position.y * forward,
                yaw = from.yaw + local.yaw,
            });
        return placed;
    }

    private static void Measure(SectionDrawer.Chain chain, List<Vector2> target, float flag)
    {
        float sum = 0f;
        int count = 0;
        foreach (SectionDrawer.PlacedSection placed in chain.sections)
        {
            foreach (SectionDef.Sample sample in placed.samples)
            {
                float best = float.MaxValue;
                for (int i = 1; i < target.Count; i++) best = Mathf.Min(best, SegmentSq(sample.position, target[i - 1], target[i]));
                float d = Mathf.Sqrt(best);
                placed.maxDeviation = Mathf.Max(placed.maxDeviation, d);
                sum += d;
                count++;
            }
            placed.flagged = placed.maxDeviation > flag;
            chain.maxError = Mathf.Max(chain.maxError, placed.maxDeviation);
        }
        chain.meanError = count > 0 ? sum / count : 0f;
    }

    private static float SegmentSq(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq < 1e-8f) return (p - a).sqrMagnitude;
        return (p - (a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq))).sqrMagnitude;
    }

    // ---------- Chain -> knots ----------

    /// <summary>One Bezier knot per piece joint: exact straights and arcs, tangent-continuous. Drawer-local, at height y.</summary>
    public static List<Knot> BuildKnots(SectionDrawer.Chain chain, float y)
    {
        var starts = new List<Vector2>();
        var yaws = new List<float>();
        var handles = new List<float>(); // Bezier handle length of each piece
        Vector2 position = default;
        float yaw = 0f;

        foreach (SectionDrawer.PlacedSection placed in chain.sections)
        {
            position = placed.position;
            yaw = placed.yaw;
            foreach (SectionDef.PieceInfo piece in placed.def.ParsePieces())
            {
                starts.Add(position);
                yaws.Add(yaw);
                if (piece.IsArc)
                {
                    handles.Add(piece.radius * ArcHandle);
                    Vector2 center = position + piece.sign * piece.radius * SectionDef.Right(yaw);
                    yaw += piece.sign * SectionDef.TurnAngle;
                    position = center - piece.sign * piece.radius * SectionDef.Right(yaw);
                }
                else
                {
                    handles.Add(piece.length / 3f);
                    position += SectionDef.Forward(yaw) * piece.length;
                }
            }
        }

        var knots = new List<Knot>();
        Vector3 At(Vector2 p) => new Vector3(p.x, y, p.y);
        Vector3 Vec(Vector2 v) => new Vector3(v.x, 0f, v.y);
        for (int i = 0; i <= starts.Count; i++)
        {
            var knot = new Knot { position = At(i < starts.Count ? starts[i] : position) };
            float heading = i < starts.Count ? yaws[i] : yaw;
            if (i > 0) knot.tangentIn = Vec(-SectionDef.Forward(heading) * handles[i - 1]);
            if (i < starts.Count) knot.tangentOut = Vec(SectionDef.Forward(heading) * handles[i]);
            knots.Add(knot);
        }
        return knots;
    }
}
