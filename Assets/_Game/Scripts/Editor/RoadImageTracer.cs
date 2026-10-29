using System.Collections.Generic;
using UnityEngine;

// Turns a road drawing into ordered centre lines, in pixel coordinates (x right, y up).
//   black/dark pixels = road (any stroke thickness), red dot = finish platform.
//   brown dot = a helper platform (never road), yellow dot = a jump pad (sits on the road, counts as road).
//   green dot on the road = a spline starts here, blue dot = a spline ends here. At a junction put each
//   dot on its own spline's side (blue on the road coming in, green on the one going out); a blue+green
//   pair in the middle of a line cuts it into two splines. The green dot on a free line end = race start.
// Steps: thin the strokes to 1-pixel lines -> split them into edges at junctions and dots -> drop the
// little spurs thinning leaves at corners -> order and orient the edges along the race.
// Two edges joining the same two junctions (an oval) become one closed path = a loop/fork for TrackRoute.
// Separate strokes (gaps, jumps) become separate paths, each one continuing from the nearest end.
public static class RoadImageTracer
{
    public class Path
    {
        public List<Vector2> Points = new List<Vector2>();
        public bool Closed; // first point == junction where runners enter; the last point isn't repeated
        public bool Marked; // direction came from green/blue dots (not guessed)
    }

    public class Result
    {
        public readonly List<Path> Paths = new List<Path>();
        public Vector2? Start;           // the green-dotted line end the race starts at, when there is one
        public Vector2? Finish;          // red dot (where the finish platform goes)
        public float StrokeThickness;    // average stroke width in pixels
        public float JoinDistance;       // pixels: line ends closer than this to a junction belong to it (forks)
        public readonly List<Vector2> Platforms = new List<Vector2>(); // brown dot centres
        public readonly List<Vector2> JumpPads = new List<Vector2>();  // yellow dot centres
    }

    private class Edge
    {
        public int A, B;                 // node ids; A == B for a loop
        public List<Vector2> Points;     // from node A to node B, both node centres included
        public bool Removed;

        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Points.Count; i++) length += Vector2.Distance(Points[i - 1], Points[i]);
                return length;
            }
        }
    }

    // Neighbour offsets in ring order (N, NE, E, SE, S, SW, W, NW).
    private static readonly int[] RingX = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] RingY = { 1, 1, 0, -1, -1, -1, 0, 1 };

    private const byte None = 0, Green = 1, Blue = 2, Red = 3, Brown = 4, Yellow = 5;

    // Marker colour of every pixel (padded grid), plus how far from a line end a dot still counts for it.
    private class Markers
    {
        public byte[] Grid;
        public int W;
        public float Reach;

        public byte At(Vector2 p)
        {
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            if (Mathf.Abs(p.x - x) > 0.01f || Mathf.Abs(p.y - y) > 0.01f) return None; // a node centre, not a pixel
            int i = (y + 1) * W + x + 1;
            return i >= 0 && i < Grid.Length ? Grid[i] : None;
        }

        // A green/blue pixel within `radius` pixels of p (rounded), or None. Green wins a tie.
        public byte Near(Vector2 p, int radius)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            byte found = None;
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int i = (y + 1) * W + x + 1;
                    if (i < 0 || i >= Grid.Length) continue;
                    if (Grid[i] == Green) return Green;
                    if (Grid[i] == Blue) found = Blue;
                }
            return found;
        }
    }

    private struct Blob
    {
        public byte Colour;
        public Vector2 Centre;
        public float Radius;
    }

    // joinDistance: pixels; 0 = automatic (8 stroke widths).
    public static Result Trace(Color32[] pixels, int width, int height, float darkThreshold, float joinDistance = 0f)
    {
        var result = new Result();

        // One empty pixel of padding all round, so neighbour lookups never leave the grid.
        int w = width + 2, h = height + 2;
        var grid = new bool[w * h];
        var marks = new Markers { Grid = new byte[w * h], W = w };
        int roadCount = 0;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 c = pixels[y * width + x];
                if (c.a < 128) continue;
                byte mark = MarkOf(c);
                int i = (y + 1) * w + x + 1;
                marks.Grid[i] = mark;
                // A platform stands next to the road, so a brown dot is never road — even a dark brown one.
                if (mark == Brown) continue;
                float luminance = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                // Other dots count as road, so a dot drawn on the line doesn't cut the stroke.
                if (mark != None || luminance < darkThreshold) { grid[i] = true; roadCount++; }
            }

        if (roadCount == 0) return result;
        List<Blob> blobs = FindBlobs(marks.Grid, w);
        foreach (Blob blob in blobs)
        {
            if (blob.Colour == Red) result.Finish = blob.Centre;
            // A couple of stray pixels (anti-aliasing at a stroke's edge) aren't a dot.
            if (blob.Colour == Brown && blob.Radius >= 1f) result.Platforms.Add(blob.Centre);
            if (blob.Colour == Yellow && blob.Radius >= 1f) result.JumpPads.Add(blob.Centre);
        }

        Thin(grid, w, h);
        RemoveStairCorners(grid, w, h);

        int skeletonCount = 0;
        foreach (bool on in grid) if (on) skeletonCount++;
        result.StrokeThickness = roadCount / (float)Mathf.Max(1, skeletonCount);
        marks.Reach = Mathf.Max(6f, result.StrokeThickness * 3f);

        List<Vector2> nodes = BuildGraph(grid, w, h, out List<Edge> edges);
        result.JoinDistance = joinDistance > 0f ? joinDistance : result.StrokeThickness * 8f;
        float spur = Mathf.Max(4f, result.StrokeThickness * 2.5f);
        var protectedNodes = new HashSet<int>();
        CutAtMarkers(nodes, edges, blobs, marks.Reach, protectedNodes);
        foreach (Blob blob in blobs)
            if (blob.Colour == Green || blob.Colour == Red) protectedNodes.Add(NearestNode(nodes, edges, blob.Centre, true));
        Simplify(nodes, edges, spur, protectedNodes);
        Order(nodes, edges, marks, result);
        return result;
    }

    // Paint-style colours, with room for slightly different shades. Order matters: yellow before green
    // (both high G), and red is a strong red only, so a reddish brown counts as brown.
    private static byte MarkOf(Color32 c)
    {
        if (c.r > 200 && c.g > 180 && c.b < 110) return Yellow;
        if (c.g > 150 && c.r < 110 && c.b < 110) return Green;
        if (c.b > 130 && c.r < 110 && c.g < 110) return Blue;
        if (c.r > 150 && c.g < 110 && c.b < 110 && c.r - c.g > 100) return Red;
        if (c.r > 90 && c.g >= 40 && c.r - c.g >= 35 && c.g - c.b >= 15) return Brown;
        return None;
    }

    // Touching pixels of one marker colour = one dot.
    private static List<Blob> FindBlobs(byte[] marks, int w)
    {
        var blobs = new List<Blob>();
        var seen = new bool[marks.Length];
        var stack = new Stack<int>();
        for (int i = 0; i < marks.Length; i++)
        {
            if (marks[i] == None || seen[i]) continue;
            byte colour = marks[i];
            Vector2 sum = Vector2.zero;
            int count = 0;
            seen[i] = true;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                sum += PixelPos(p, w);
                count++;
                foreach (int n in Neighbours(p, w))
                    if (n >= 0 && n < marks.Length && !seen[n] && marks[n] == colour) { seen[n] = true; stack.Push(n); }
            }
            blobs.Add(new Blob { Colour = colour, Centre = sum / count, Radius = Mathf.Sqrt(count / Mathf.PI) });
        }
        return blobs;
    }

    // A green/blue dot in the middle of a line (not at a junction or line end) splits it into two splines there.
    private static void CutAtMarkers(List<Vector2> nodes, List<Edge> edges, List<Blob> blobs, float reach, HashSet<int> protectedNodes)
    {
        foreach (Blob blob in blobs)
        {
            if (blob.Colour != Green && blob.Colour != Blue) continue;
            Edge best = null;
            int bestIndex = -1;
            float bestDistance = blob.Radius + 2f;
            foreach (Edge e in edges)
                for (int i = 1; i < e.Points.Count - 1; i++)
                {
                    float d = Vector2.Distance(e.Points[i], blob.Centre);
                    if (d < bestDistance) { bestDistance = d; best = e; bestIndex = i; }
                }
            if (best == null) continue;
            if (ArcLength(best.Points, 0, bestIndex) < reach || ArcLength(best.Points, bestIndex, best.Points.Count - 1) < reach)
                continue; // at a junction or line end — that's where it marks a direction, not a cut

            int node = nodes.Count;
            nodes.Add(best.Points[bestIndex]);
            protectedNodes.Add(node);
            edges.Add(new Edge { A = node, B = best.B, Points = best.Points.GetRange(bestIndex, best.Points.Count - bestIndex) });
            best.Points = best.Points.GetRange(0, bestIndex + 1);
            best.B = node;
        }
    }

    private static float ArcLength(List<Vector2> points, int from, int to)
    {
        float length = 0f;
        for (int i = from + 1; i <= to; i++) length += Vector2.Distance(points[i - 1], points[i]);
        return length;
    }

    // The dot an edge runs through as it leaves one of its ends: the LAST green/blue pixel within reach.
    // "Last" because at a junction the line runs through the neighbour's dot first (blue on the incoming
    // road, then green on the loop's arm) — the far one is this edge's own.
    private static byte EndMark(Edge e, bool atA, Markers marks)
    {
        int count = e.Points.Count;
        // Thinning pulls a line end back inside its stroke, so the end may stop just short of a dot drawn there.
        byte found = marks.Near(e.Points[atA ? 0 : count - 1], 2);
        if (found != Green && found != Blue) found = None;
        float travelled = 0f;
        for (int k = 1; k < count; k++)
        {
            Vector2 previous = e.Points[atA ? k - 1 : count - k];
            Vector2 point = e.Points[atA ? k : count - 1 - k];
            travelled += Vector2.Distance(previous, point);
            if (travelled > marks.Reach) break;
            byte mark = marks.At(point);
            if (mark == Green || mark == Blue) found = mark;
        }
        return found;
    }

    // ---------- Thinning ----------

    // Zhang-Suen: peels stroke edges away until only 1-pixel centre lines are left.
    private static void Thin(bool[] g, int w, int h)
    {
        var remove = new List<int>();
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int pass = 0; pass < 2; pass++)
            {
                remove.Clear();
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                    {
                        int i = y * w + x;
                        if (!g[i]) continue;
                        bool p2 = g[i + w], p3 = g[i + w + 1], p4 = g[i + 1], p5 = g[i - w + 1];
                        bool p6 = g[i - w], p7 = g[i - w - 1], p8 = g[i - 1], p9 = g[i + w - 1];
                        bool[] ring = { p2, p3, p4, p5, p6, p7, p8, p9 };

                        int count = 0, transitions = 0;
                        for (int k = 0; k < 8; k++)
                        {
                            if (ring[k]) count++;
                            if (!ring[k] && ring[(k + 1) % 8]) transitions++;
                        }
                        if (count < 2 || count > 6 || transitions != 1) continue;
                        if (pass == 0 && ((p2 && p4 && p6) || (p4 && p6 && p8))) continue;
                        if (pass == 1 && ((p2 && p4 && p8) || (p2 && p6 && p8))) continue;
                        remove.Add(i);
                    }
                foreach (int i in remove) g[i] = false;
                if (remove.Count > 0) changed = true;
            }
        }
    }

    // Thinning leaves L-shaped corners (a pixel touching both N and E, which also touch each other).
    // Those look like 3-way junctions, so remove any pixel whose neighbours stay connected without it.
    private static void RemoveStairCorners(bool[] g, int w, int h)
    {
        var ring = new List<int>(8);
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                if (!g[y * w + x]) continue;
                ring.Clear();
                for (int k = 0; k < 8; k++)
                    if (g[(y + RingY[k]) * w + x + RingX[k]]) ring.Add(k);
                if (ring.Count < 2) continue;
                if (RingConnected(ring)) g[y * w + x] = false;
            }
    }

    private static bool RingConnected(List<int> ring)
    {
        var reached = new bool[ring.Count];
        var stack = new Stack<int>();
        stack.Push(0);
        reached[0] = true;
        int count = 1;
        while (stack.Count > 0)
        {
            int a = ring[stack.Pop()];
            for (int j = 0; j < ring.Count; j++)
            {
                if (reached[j]) continue;
                int b = ring[j];
                if (Mathf.Abs(RingX[a] - RingX[b]) <= 1 && Mathf.Abs(RingY[a] - RingY[b]) <= 1)
                {
                    reached[j] = true;
                    count++;
                    stack.Push(j);
                }
            }
        }
        return count == ring.Count;
    }

    // ---------- Graph ----------

    // Nodes = line ends and junctions (touching junction pixels form one node). Edges = the pixel runs between.
    private static List<Vector2> BuildGraph(bool[] g, int w, int h, out List<Edge> edges)
    {
        edges = new List<Edge>();
        var nodePositions = new List<Vector2>();
        var nodeOf = new int[g.Length];
        for (int i = 0; i < nodeOf.Length; i++) nodeOf[i] = -1;

        var degree = new int[g.Length];
        for (int i = 0; i < g.Length; i++)
        {
            if (!g[i]) continue;
            foreach (int n in Neighbours(i, w)) if (g[n]) degree[i]++;
        }

        // Flood-fill touching non-line pixels (degree != 2) into nodes.
        for (int i = 0; i < g.Length; i++)
        {
            if (!g[i] || degree[i] == 2 || degree[i] == 0 || nodeOf[i] >= 0) continue;
            int id = nodePositions.Count;
            Vector2 sum = Vector2.zero;
            int count = 0;
            var stack = new Stack<int>();
            stack.Push(i);
            nodeOf[i] = id;
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                sum += PixelPos(p, w);
                count++;
                foreach (int n in Neighbours(p, w))
                    if (g[n] && degree[n] != 2 && nodeOf[n] < 0) { nodeOf[n] = id; stack.Push(n); }
            }
            nodePositions.Add(sum / count);
        }

        var visited = new bool[g.Length];
        var directPairs = new HashSet<long>();

        for (int s = 0; s < g.Length; s++)
        {
            if (nodeOf[s] < 0) continue;
            int a = nodeOf[s];
            foreach (int first in Neighbours(s, w))
            {
                if (!g[first] || nodeOf[first] == a) continue;

                if (nodeOf[first] >= 0) // two nodes touching directly
                {
                    long key = Mathf.Min(s, first) * (long)g.Length + Mathf.Max(s, first);
                    if (directPairs.Add(key))
                        edges.Add(new Edge { A = a, B = nodeOf[first], Points = new List<Vector2> { nodePositions[a], nodePositions[nodeOf[first]] } });
                    continue;
                }
                if (visited[first]) continue;

                var points = new List<Vector2> { nodePositions[a] };
                int prev = s, cur = first, end = -1, steps = 0;
                while (true)
                {
                    visited[cur] = true;
                    points.Add(PixelPos(cur, w));
                    steps++;
                    int next = -1;
                    foreach (int n in Neighbours(cur, w))
                    {
                        if (!g[n] || n == prev) continue;
                        // Right next to the start node, its other pixels aren't the far end.
                        if (nodeOf[n] >= 0 && !(nodeOf[n] == a && steps < 3)) { end = nodeOf[n]; break; }
                        if (nodeOf[n] < 0 && !visited[n]) next = n;
                    }
                    if (end >= 0 || next < 0) break;
                    prev = cur;
                    cur = next;
                }
                if (end < 0) continue; // ran into an already-walked run — only possible on broken input
                points.Add(nodePositions[end]);
                edges.Add(new Edge { A = a, B = end, Points = points });
            }
        }

        // Rings with no ends or junctions at all (a lone drawn circle).
        for (int i = 0; i < g.Length; i++)
        {
            if (!g[i] || visited[i] || nodeOf[i] >= 0 || degree[i] != 2) continue;
            int id = nodePositions.Count;
            nodePositions.Add(PixelPos(i, w));
            var points = new List<Vector2> { PixelPos(i, w) };
            int prev = -1, cur = i;
            visited[i] = true;
            while (true)
            {
                int next = -1;
                foreach (int n in Neighbours(cur, w))
                    if (g[n] && n != prev && !visited[n]) { next = n; break; }
                if (next < 0) break;
                visited[next] = true;
                points.Add(PixelPos(next, w));
                prev = cur;
                cur = next;
            }
            points.Add(PixelPos(i, w));
            edges.Add(new Edge { A = id, B = id, Points = points });
        }
        return nodePositions;
    }

    // Removes thinning spurs and noise, then joins edges through nodes that are no longer junctions.
    private static void Simplify(List<Vector2> nodes, List<Edge> edges, float spur, HashSet<int> protectedNodes)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            int[] degree = Degrees(nodes.Count, edges);

            foreach (Edge e in edges)
            {
                if (e.Removed) continue;
                float length = e.Length;
                bool aEnd = degree[e.A] == 1, bEnd = degree[e.B] == 1;
                bool spurA = aEnd && degree[e.B] >= 3 && !protectedNodes.Contains(e.A);
                bool spurB = bEnd && degree[e.A] >= 3 && !protectedNodes.Contains(e.B);
                bool tinyLoop = e.A == e.B && length < spur * 2f && degree[e.A] > 2;
                bool speck = aEnd && bEnd && length < spur * 2f;
                if (((spurA || spurB) && length < spur) || tinyLoop || speck)
                {
                    e.Removed = true;
                    changed = true;
                    degree = Degrees(nodes.Count, edges);
                }
            }

            // A node with exactly two different edges is just a bend now — join them.
            for (int node = 0; node < nodes.Count && !changed; node++)
            {
                if (degree[node] != 2 || protectedNodes.Contains(node)) continue; // protected = cut at a dot
                Edge first = null, second = null;
                foreach (Edge e in edges)
                {
                    if (e.Removed || (e.A != node && e.B != node)) continue;
                    if (first == null) first = e; else second = e;
                }
                if (first == null || second == null) continue; // a loop through this node — leave it
                List<Vector2> a = first.B == node ? first.Points : Reversed(first.Points);
                List<Vector2> b = second.A == node ? second.Points : Reversed(second.Points);
                var joined = new List<Vector2>(a);
                joined.AddRange(b.GetRange(1, b.Count - 1));
                int start = first.B == node ? first.A : first.B;
                int end = second.A == node ? second.B : second.A;
                first.Removed = second.Removed = true;
                edges.Add(new Edge { A = start, B = end, Points = joined });
                changed = true;
            }
        }
        edges.RemoveAll(e => e.Removed);
    }

    // ---------- Race order ----------

    // Green/blue dots decide each spline's direction and where the race starts; without dots,
    // splines run away from the start (lowest line end) and strokes follow on from the nearest end.
    private static void Order(List<Vector2> nodes, List<Edge> edges, Markers marks, Result result)
    {
        if (edges.Count == 0) return;
        int[] degree = Degrees(nodes.Count, edges);
        var done = new HashSet<Edge>();
        // Ends of the open splines placed so far, in order. A fork drawn with gaps hangs several strokes
        // off one end, so the next stroke is looked for at these "sockets", oldest first.
        var sockets = new List<Vector2>();

        int current = GreenEnd(nodes, edges, degree, marks, done, null);
        if (current >= 0) result.Start = nodes[current];
        else current = LowestEnd(nodes, edges, degree);

        while (done.Count < edges.Count)
        {
            Dictionary<int, float> distance = Distances(current, edges);
            var group = new List<Edge>();
            foreach (Edge e in edges)
                if (!done.Contains(e) && distance.ContainsKey(e.A)) group.Add(e);

            group.Sort((x, y) =>
            {
                int c = Near(x, distance).CompareTo(Near(y, distance));
                return c != 0 ? c : Far(x, distance).CompareTo(Far(y, distance));
            });

            foreach (Edge e in group)
            {
                if (done.Contains(e)) continue;
                done.Add(e);

                // Two edges between the same two junctions = an oval/square: one closed loop from the entry junction.
                Edge twin = null;
                if (e.A != e.B)
                    foreach (Edge other in group)
                        if (!done.Contains(other) && ((other.A == e.A && other.B == e.B) || (other.A == e.B && other.B == e.A)))
                        { twin = other; break; }

                int byMarks = DirectionFromMarks(e, marks);
                if (byMarks == 0 && twin != null) byMarks = DirectionFromMarks(twin, marks) * (twin.A == e.A ? 1 : -1);
                int entry = byMarks > 0 ? e.A
                          : byMarks < 0 ? e.B
                          : distance[e.A] <= distance[e.B] ? e.A : e.B;

                List<Vector2> forward = e.A == entry ? e.Points : Reversed(e.Points);
                var path = new Path { Points = new List<Vector2>(forward), Marked = byMarks != 0 };

                if (twin != null)
                {
                    done.Add(twin);
                    List<Vector2> back = twin.B == entry ? twin.Points : Reversed(twin.Points);
                    path.Points.AddRange(back.GetRange(1, back.Count - 2)); // skip the shared junction points
                    path.Closed = true;
                }
                else if (e.A == e.B)
                {
                    path.Points.RemoveAt(path.Points.Count - 1);
                    path.Closed = true;
                }
                result.Paths.Add(path);
                if (!path.Closed) sockets.Add(path.Points[path.Points.Count - 1]);
            }

            // Continue from the far end of this stroke to the next stroke: a green-dotted line end
            // when there is one, otherwise the nearest end (across a gap).
            int exit = current;
            foreach (KeyValuePair<int, float> pair in distance)
                if (pair.Value > distance[exit]) exit = pair.Key;

            int nextStart = -1;
            foreach (Vector2 socket in sockets)
            {
                nextStart = GreenEnd(nodes, edges, degree, marks, done, socket, result.JoinDistance);
                if (nextStart >= 0) break;
            }
            if (nextStart < 0) nextStart = GreenEnd(nodes, edges, degree, marks, done, nodes[exit]);
            if (nextStart < 0)
            {
                float best = float.PositiveInfinity;
                foreach (Edge e in edges)
                {
                    if (done.Contains(e)) continue;
                    foreach (int node in new[] { e.A, e.B })
                    {
                        float d = Vector2.Distance(nodes[node], nodes[exit]) - (degree[node] == 1 ? 0.5f : 0f);
                        if (d < best) { best = d; nextStart = node; }
                    }
                }
            }
            if (nextStart < 0) break;
            current = nextStart;
        }
    }

    // +1 = the dots say A -> B, -1 = B -> A, 0 = no dots (or they contradict each other).
    private static int DirectionFromMarks(Edge e, Markers marks)
    {
        byte a = EndMark(e, true, marks), b = EndMark(e, false, marks);
        bool forward = a == Green || b == Blue;
        bool backward = b == Green || a == Blue;
        return forward == backward ? 0 : forward ? 1 : -1;
    }

    // A line end (not a junction) whose edge starts on a green dot, among strokes not done yet.
    // Nearest to `near` when given, otherwise the lowest one in the picture. -1 = none.
    private static int GreenEnd(List<Vector2> nodes, List<Edge> edges, int[] degree, Markers marks,
                                HashSet<Edge> done, Vector2? near, float within = float.PositiveInfinity)
    {
        int best = -1;
        float bestScore = float.PositiveInfinity;
        foreach (Edge e in edges)
        {
            if (done.Contains(e)) continue;
            for (int side = 0; side < 2; side++)
            {
                int node = side == 0 ? e.A : e.B;
                if (degree[node] != 1 || EndMark(e, side == 0, marks) != Green) continue;
                float score = near.HasValue ? Vector2.Distance(nodes[node], near.Value) : nodes[node].y;
                if (near.HasValue && score > within) continue;
                if (score < bestScore) { bestScore = score; best = node; }
            }
        }
        return best;
    }

    private static float Near(Edge e, Dictionary<int, float> d) => Mathf.Min(d[e.A], d[e.B]);
    private static float Far(Edge e, Dictionary<int, float> d) => Mathf.Max(d[e.A], d[e.B]);

    // Shortest distance from the start node to every node it connects to (Dijkstra; graphs are tiny).
    private static Dictionary<int, float> Distances(int start, List<Edge> edges)
    {
        var distance = new Dictionary<int, float> { [start] = 0f };
        var open = new List<int> { start };
        while (open.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
                if (distance[open[i]] < distance[open[bestIndex]]) bestIndex = i;
            int node = open[bestIndex];
            open.RemoveAt(bestIndex);

            foreach (Edge e in edges)
            {
                if (e.A != node && e.B != node) continue;
                int other = e.A == node ? e.B : e.A;
                float d = distance[node] + e.Length;
                if (!distance.TryGetValue(other, out float known) || d < known)
                {
                    distance[other] = d;
                    if (!open.Contains(other)) open.Add(other);
                }
            }
        }
        return distance;
    }

    private static int NearestNode(List<Vector2> nodes, List<Edge> edges, Vector2 point, bool preferEnds)
    {
        int[] degree = Degrees(nodes.Count, edges);
        int best = -1;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (degree[i] == 0) continue;
            float d = Vector2.Distance(nodes[i], point);
            if (preferEnds && degree[i] != 1) d += 3f; // a line end beats a junction at about the same spot
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    // No start marker: the line end lowest in the picture (the race runs up the image).
    private static int LowestEnd(List<Vector2> nodes, List<Edge> edges, int[] degree)
    {
        int best = -1;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (degree[i] == 0) continue;
            bool better = best < 0
                          || (degree[i] == 1 && degree[best] != 1)
                          || ((degree[i] == 1) == (degree[best] == 1) && nodes[i].y < nodes[best].y);
            if (better) best = i;
        }
        return best >= 0 ? best : edges[0].A;
    }

    private static int[] Degrees(int nodeCount, List<Edge> edges)
    {
        var degree = new int[nodeCount];
        foreach (Edge e in edges)
        {
            if (e.Removed) continue;
            degree[e.A]++;
            degree[e.B]++;
        }
        return degree;
    }

    private static List<Vector2> Reversed(List<Vector2> points)
    {
        var copy = new List<Vector2>(points);
        copy.Reverse();
        return copy;
    }

    // Padded index -> original pixel coordinates.
    private static Vector2 PixelPos(int index, int w) => new Vector2(index % w - 1, index / w - 1);

    private static IEnumerable<int> Neighbours(int index, int w)
    {
        for (int k = 0; k < 8; k++) yield return index + RingY[k] * w + RingX[k];
    }
}
