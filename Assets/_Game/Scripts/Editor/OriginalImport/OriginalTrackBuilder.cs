using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

// Turns the original game's modular road pieces (straights, 45° turns) into SplineRoad sections.
// Pieces whose ends meet are joined into chains; one chain = one open spline. Two chains that run between
// the same two junctions are a fork around something, which SplineRoad/TrackRoute express as one closed
// spline (a loop) — so such pairs are merged into a loop. Sizes are the original's, unscaled.
// Pure math: builds Spline objects, touches no scene objects.
public static class OriginalTrackBuilder
{
    // Measured from the original meshes (Straight / Turn_45 / Turn_45_short) before the piece's own scale.
    // Most sections place pieces at scale 1.5, a few early test ones at 1.
    private const float MeshWidth = 6.113f;
    private const float StraightLength = 10f;
    private const float TurnRadius = 17f;
    private const float ShortTurnRadius = 7f;
    private const float TurnAngle = 45f;
    // Ends closer than this are the same junction.
    private const float JoinTolerance = 0.5f;
    // A side branch's end may sit this far outside its host road's edge and still count as attached.
    private const float SideAttachSlack = 0.3f;

    public struct Piece
    {
        public string type;     // "path_Straight", "path_Turn_45_L", "path_Turn_45_Short_R", ...
        public Vector3 position; // the piece's pivot = one of its two ends
        public float yaw;
        public float scale;      // the piece's uniform world scale
        public bool mirrored;    // the level is flipped left-right: a turn then bends towards +X instead of -X
    }

    public static float RoadWidth(float pieceScale) => MeshWidth * pieceScale;

    // One piece as a curve from P0 to P1, with the road direction at each end. Straight or one arc.
    private struct Segment
    {
        public Vector3 p0, p1;
        public Vector3 dir0, dir1;
        public float handle0, handle1; // Bezier handle lengths that reproduce the piece's shape
        public bool straight;
        public bool splitRemainder;    // part of a straight cut at a side fork — dropped if it leads nowhere

        public float Length => Vector3.Distance(p0, p1);

        public Segment Reversed() => new Segment
        {
            p0 = p1, p1 = p0, dir0 = -dir1, dir1 = -dir0,
            handle0 = handle1, handle1 = handle0, straight = straight, splitRemainder = splitRemainder,
        };

        public static Segment Line(Vector3 from, Vector3 to, bool splitRemainder = false)
        {
            Vector3 dir = (to - from).normalized;
            float handle = Vector3.Distance(from, to) / 3f;
            return new Segment
            {
                p0 = from, p1 = to, dir0 = dir, dir1 = dir,
                handle0 = handle, handle1 = handle, straight = true, splitRemainder = splitRemainder,
            };
        }
    }

    // Raised side paths (…_HighGround) are left out for now.
    public static bool IsRoadPiece(string type) =>
        (type.StartsWith("path_Straight") || type.StartsWith("path_Turn_45")) && !type.Contains("HighGround");

    /// <summary>Splines in race order, starting from the chain nearest raceStart.</summary>
    public static List<Spline> Build(IReadOnlyList<Piece> pieces, Vector3 raceStart, Vector3 raceEnd)
    {
        var segments = new List<Segment>();
        foreach (Piece piece in pieces) segments.Add(ToSegment(piece));
        float halfWidth = pieces.Count > 0 ? RoadWidth(pieces[0].scale) * 0.5f : 0f;

        AttachSideBranches(segments, halfWidth, raceStart, raceEnd);
        DropDeadEndRemainders(segments, halfWidth, raceStart, raceEnd);

        // Junctions: piece ends that touch. Each segment is an edge between two junctions.
        var junctions = new List<Vector3>();
        var edgeEnds = new List<(int a, int b)>();
        foreach (Segment s in segments) edgeEnds.Add((JunctionOf(s.p0, junctions), JunctionOf(s.p1, junctions)));

        var edgesAt = new List<List<int>>();
        for (int j = 0; j < junctions.Count; j++) edgesAt.Add(new List<int>());
        for (int e = 0; e < edgeEnds.Count; e++)
        {
            edgesAt[edgeEnds[e].a].Add(e);
            edgesAt[edgeEnds[e].b].Add(e);
        }

        List<Chain> chains = FindChains(segments, edgeEnds, edgesAt);
        return OrderIntoSplines(chains, junctions, raceStart);
    }

    // ---------- Pieces ----------

    private static Segment ToSegment(Piece piece)
    {
        Quaternion heading = Quaternion.Euler(0f, piece.yaw, 0f);
        Vector3 forward = heading * Vector3.forward;

        if (piece.type.StartsWith("path_Straight"))
            return Segment.Line(piece.position, piece.position + forward * StraightLength * piece.scale);

        // Every turn mesh bends towards its own -X, L and R alike (they differ only in texture).
        float radius = (piece.type.Contains("Short") ? ShortTurnRadius : TurnRadius) * piece.scale;
        float angle = TurnAngle * Mathf.Deg2Rad;
        float bend = piece.mirrored ? 1f : -1f; // -1 = towards -X (left), +1 = a mirrored turn bends right
        var localEnd = new Vector3(bend * radius * (1f - Mathf.Cos(angle)), 0f, radius * Mathf.Sin(angle));
        // Bezier handle for a circular arc: 4/3 * tan(angle / 4) * radius.
        float handle = 4f / 3f * Mathf.Tan(angle / 4f) * radius;

        return new Segment
        {
            p0 = piece.position, p1 = piece.position + heading * localEnd,
            dir0 = forward, dir1 = Quaternion.Euler(0f, piece.yaw + bend * TurnAngle, 0f) * Vector3.forward,
            handle0 = handle, handle1 = handle, straight = false,
        };
    }

    // ---------- Side forks ----------

    // In the original a fork doesn't start at a piece's end: the two branches leave from the SIDES of a straight
    // (their ends sit on its edges), and come back onto the sides of a later straight. To make that a proper
    // junction, the host straight is cut where the branch meets it and a short stub joins its centre line to
    // the branch's end. The stub lies on top of the host road, so the road looks the same.
    private static void AttachSideBranches(List<Segment> segments, float halfWidth, Vector3 raceStart, Vector3 raceEnd)
    {
        List<Vector3> loose = LooseEnds(segments, raceStart, raceEnd);
        var cuts = new Dictionary<int, List<float>>(); // host straight -> distances along it to cut at
        var stubs = new List<(int host, float along, Vector3 end)>();

        foreach (Vector3 end in loose)
        {
            int bestHost = -1;
            float bestAlong = 0f, bestSide = float.MaxValue;
            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                if (!s.straight || Mathf.Abs(end.y - s.p0.y) > JoinTolerance) continue;
                Vector3 offset = end - s.p0;
                float along = Vector3.Dot(offset, s.dir0);
                float side = (offset - s.dir0 * along).magnitude;
                if (along <= JoinTolerance || along >= s.Length - JoinTolerance) continue;
                if (side < JoinTolerance || side > halfWidth + SideAttachSlack || side >= bestSide) continue;
                bestHost = i; bestAlong = along; bestSide = side;
            }
            if (bestHost < 0) continue;

            if (!cuts.TryGetValue(bestHost, out List<float> list)) cuts[bestHost] = list = new List<float>();
            if (!list.Exists(a => Mathf.Abs(a - bestAlong) < JoinTolerance)) list.Add(bestAlong);
            stubs.Add((bestHost, bestAlong, end));
        }

        // Stubs first (they need the hosts as they were), then replace each host by its cut pieces.
        foreach ((int host, float along, Vector3 end) in stubs)
        {
            Segment s = segments[host];
            float snapped = cuts[host].Find(a => Mathf.Abs(a - along) < JoinTolerance);
            segments.Add(Segment.Line(s.p0 + s.dir0 * snapped, end));
        }
        foreach (int host in cuts.Keys.OrderByDescending(i => i))
        {
            Segment s = segments[host];
            segments.RemoveAt(host);
            Vector3 from = s.p0;
            foreach (float along in cuts[host].OrderBy(a => a))
            {
                Vector3 at = s.p0 + s.dir0 * along;
                segments.Add(Segment.Line(from, at, splitRemainder: true));
                from = at;
            }
            segments.Add(Segment.Line(from, s.p1, splitRemainder: true));
        }
    }

    // A cut-off bit of straight past a fork that leads nowhere (the main road ended there and the race
    // goes on through the branches). It's covered by the stubs' road anyway, and as a dead end it would
    // break the race order — so it goes.
    private static void DropDeadEndRemainders(List<Segment> segments, float halfWidth, Vector3 raceStart, Vector3 raceEnd)
    {
        List<Vector3> loose = LooseEnds(segments, raceStart, raceEnd);
        segments.RemoveAll(s => s.splitRemainder && s.Length <= halfWidth + SideAttachSlack &&
                                loose.Exists(p => Near(p, s.p0) || Near(p, s.p1)));
    }

    // Piece ends that touch no other piece and aren't where the race starts or ends.
    private static List<Vector3> LooseEnds(List<Segment> segments, Vector3 raceStart, Vector3 raceEnd)
    {
        var ends = new List<Vector3>();
        foreach (Segment s in segments) { ends.Add(s.p0); ends.Add(s.p1); }

        var loose = new List<Vector3>();
        foreach (Vector3 p in ends)
        {
            int touching = ends.Count(q => Near(p, q));
            bool raceEndpoint = Near(p, raceStart) || Vector3.Distance(p, raceEnd) < 5f;
            if (touching == 1 && !raceEndpoint) loose.Add(p);
        }
        return loose;
    }

    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < JoinTolerance * JoinTolerance;

    private static int JunctionOf(Vector3 point, List<Vector3> junctions)
    {
        for (int i = 0; i < junctions.Count; i++)
            if ((junctions[i] - point).sqrMagnitude < JoinTolerance * JoinTolerance) return i;
        junctions.Add(point);
        return junctions.Count - 1;
    }

    // ---------- Chains ----------

    // Segments in driving order from junction `from` to junction `to`.
    private class Chain
    {
        public int from, to;
        public readonly List<Segment> segments = new List<Segment>();
        public bool used;
    }

    // A chain runs through junctions where exactly two pieces meet and stops where the road ends or forks.
    private static List<Chain> FindChains(List<Segment> segments, List<(int a, int b)> edgeEnds, List<List<int>> edgesAt)
    {
        var chains = new List<Chain>();
        var taken = new bool[segments.Count];

        for (int j = 0; j < edgesAt.Count; j++)
        {
            if (edgesAt[j].Count == 2) continue; // passes straight through — not a chain end
            foreach (int first in edgesAt[j])
                if (!taken[first]) chains.Add(Walk(j, first));
        }
        // Whatever is left is a ring with no ends at all.
        for (int e = 0; e < segments.Count; e++)
            if (!taken[e]) chains.Add(Walk(edgeEnds[e].a, e));
        return chains;

        Chain Walk(int startJunction, int edge)
        {
            var chain = new Chain { from = startJunction };
            int at = startJunction;
            while (true)
            {
                taken[edge] = true;
                bool forward = edgeEnds[edge].a == at;
                chain.segments.Add(forward ? segments[edge] : segments[edge].Reversed());
                at = forward ? edgeEnds[edge].b : edgeEnds[edge].a;

                if (edgesAt[at].Count != 2 || at == startJunction) break;
                int next = edgesAt[at][0] == edge ? edgesAt[at][1] : edgesAt[at][0];
                if (taken[next]) break;
                edge = next;
            }
            chain.to = at;
            return chain;
        }
    }

    // Breadth-first from the start, so sections come out in race order. A pair of chains between the
    // same two junctions becomes one closed loop; every other chain stays open, drawn away from the start.
    private static List<Spline> OrderIntoSplines(List<Chain> chains, List<Vector3> junctions, Vector3 raceStart)
    {
        var splines = new List<Spline>();
        int startJunction = 0;
        for (int j = 1; j < junctions.Count; j++)
            if ((junctions[j] - raceStart).sqrMagnitude < (junctions[startJunction] - raceStart).sqrMagnitude) startJunction = j;

        var queue = new Queue<int>();
        var visited = new HashSet<int> { startJunction };
        queue.Enqueue(startJunction);

        while (queue.Count > 0)
        {
            int at = queue.Dequeue();
            foreach (Chain chain in chains)
            {
                if (chain.used || (chain.from != at && chain.to != at)) continue;
                chain.used = true;
                List<Segment> path = Oriented(chain, at);
                int other = chain.from == at ? chain.to : chain.from;

                Chain twin = chains.Find(c => !c.used && c != chain && SameEnds(c, chain));
                if (twin != null)
                {
                    twin.used = true;
                    List<Segment> back = Oriented(twin, other); // other -> at, closes the loop
                    path.AddRange(back);
                    splines.Add(ToSpline(path, closed: true));
                }
                else splines.Add(ToSpline(path, closed: false));

                if (visited.Add(other)) queue.Enqueue(other);
            }
        }

        // Chains not connected to the start (e.g. raised side paths) still go in, after the race order.
        foreach (Chain chain in chains)
            if (!chain.used) splines.Add(ToSpline(chain.segments, closed: chain.from == chain.to && chain.segments.Count > 1));
        return splines;
    }

    private static bool SameEnds(Chain a, Chain b) =>
        (a.from == b.from && a.to == b.to) || (a.from == b.to && a.to == b.from);

    private static List<Segment> Oriented(Chain chain, int fromJunction)
    {
        var path = new List<Segment>(chain.segments);
        if (chain.from == fromJunction) return path;
        path.Reverse();
        for (int i = 0; i < path.Count; i++) path[i] = path[i].Reversed();
        return path;
    }

    // ---------- Spline ----------

    // One knot where each piece ends; runs of straights become one straight, so no needless knots.
    // Handles are set per side (Broken mode) because a straight and an arc need different lengths.
    private static Spline ToSpline(List<Segment> path, bool closed)
    {
        List<Segment> merged = MergeStraights(path);
        var spline = new Spline { Closed = closed };

        Segment first = merged[0];
        Segment last = merged[merged.Count - 1];
        float firstIn = closed ? last.handle1 : first.handle0;
        spline.Add(Knot(first.p0, first.dir0, firstIn, first.handle0), TangentMode.Broken);

        for (int i = 0; i < merged.Count; i++)
        {
            bool isLast = i == merged.Count - 1;
            if (isLast && closed) break; // the loop ends where it started — that knot already exists
            float outHandle = isLast ? merged[i].handle1 : merged[i + 1].handle0;
            spline.Add(Knot(merged[i].p1, merged[i].dir1, merged[i].handle1, outHandle), TangentMode.Broken);
        }
        return spline;
    }

    private static List<Segment> MergeStraights(List<Segment> path)
    {
        var merged = new List<Segment>();
        foreach (Segment s in path)
        {
            if (merged.Count > 0)
            {
                Segment prev = merged[merged.Count - 1];
                if (prev.straight && s.straight && Vector3.Dot(prev.dir1, s.dir0) > 0.999f)
                {
                    float length = Vector3.Distance(prev.p0, s.p1);
                    prev.p1 = s.p1;
                    prev.handle0 = prev.handle1 = length / 3f;
                    merged[merged.Count - 1] = prev;
                    continue;
                }
            }
            merged.Add(s);
        }
        return merged;
    }

    // Tangents are in the knot's own space: along its local Z, which the rotation points down the road.
    private static BezierKnot Knot(Vector3 position, Vector3 direction, float handleIn, float handleOut) =>
        new BezierKnot(
            position,
            new float3(0f, 0f, -handleIn),
            new float3(0f, 0f, handleOut),
            Quaternion.LookRotation(direction, Vector3.up));
}
