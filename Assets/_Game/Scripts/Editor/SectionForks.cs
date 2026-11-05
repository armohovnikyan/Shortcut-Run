using System.Collections.Generic;
using UnityEngine;

// Forks for the Section Drawer. A fork is one road that ends where several roads start (a split), or several roads
// that end where one road starts (a merge) — drawn with gaps between them, the dots only say which line starts and
// which ends. The real points are calculated, and the junction is a T: the arms leave the single road's end square
// to it (left arm to the left, right arm to the right), flush with the road's end edge — an arm's outer edge lines up
// with the end of the road, its centre starts on the road's side half a road width back. A merge is the same turned
// around: the arms arrive square to the road and end on its sides, level with its start edge. Nothing overlaps.
public static class SectionForks
{
    public class Fork
    {
        public bool split;                 // true: `through` ends and the arms start. false: the arms end and `through` starts.
        public int through;                // index of the single road
        public Vector2 position;           // its drawn end (split) / start (merge)
        public Vector2 direction;          // the way the race runs there
        public readonly List<int> arms = new List<int>();     // path indices, left to right
        public readonly List<float> slots = new List<float>(); // -1 = leftmost ... +1 = rightmost
    }

    private struct End
    {
        public int path;
        public bool atStart;
        public Vector2 position;
        public Vector2 outward; // unit direction pointing out of the path at this end
    }

    public static List<Fork> Find(List<List<Vector3>> paths, List<bool> closed, float joinDistance)
    {
        var ends = new List<End>();
        for (int i = 0; i < paths.Count; i++)
        {
            List<Vector3> p = paths[i];
            if (closed[i] || p.Count < 2) continue;
            int k = Mathf.Min(6, p.Count - 1);
            ends.Add(new End { path = i, atStart = true, position = Flat(p[0]), outward = (Flat(p[0]) - Flat(p[k])).normalized });
            ends.Add(new End { path = i, atStart = false, position = Flat(p[p.Count - 1]), outward = (Flat(p[p.Count - 1]) - Flat(p[p.Count - 1 - k])).normalized });
        }

        var forks = new List<Fork>();
        var used = new HashSet<int>();
        for (int t = 0; t < ends.Count; t++)
        {
            if (used.Contains(t)) continue;
            End through = ends[t];
            var armEnds = new List<int>();
            bool unique = true;
            for (int o = 0; o < ends.Count; o++)
            {
                if (o == t || ends[o].path == through.path) continue;
                float d = Vector2.Distance(ends[o].position, through.position);
                if (d > joinDistance || d < 0.01f) continue;
                if (ends[o].atStart == through.atStart) unique = false;
                else armEnds.Add(o);
            }
            if (!unique || armEnds.Count < 2) continue;

            used.Add(t);
            var fork = new Fork
            {
                split = !through.atStart,
                through = through.path,
                position = through.position,
                direction = through.atStart ? -through.outward : through.outward,
            };
            Vector2 right = Right(fork.direction);
            armEnds.Sort((a, b) => Vector2.Dot(ends[a].position - through.position, right)
                                   .CompareTo(Vector2.Dot(ends[b].position - through.position, right)));
            for (int i = 0; i < armEnds.Count; i++)
            {
                used.Add(armEnds[i]);
                fork.arms.Add(ends[armEnds[i]].path);
                fork.slots.Add(armEnds.Count == 1 ? 0f : -1f + 2f * i / (armEnds.Count - 1));
            }
            forks.Add(fork);
        }
        return forks;
    }

    /// <summary>Where an arm starts and which way it heads, for a split. `face` is the middle of the single road's end
    /// edge, `direction` the way it runs. Left / right arms start on the road's side half a width back from the end and
    /// head sideways; an arm in the middle continues straight on from the end.</summary>
    public static void ArmStart(Vector2 face, Vector2 direction, float slot, float roadWidth, out Vector2 start, out Vector2 heading)
    {
        float side = Side(slot);
        if (side == 0f) { start = face; heading = direction; return; }
        start = face + Right(direction) * (side * roadWidth * 0.5f) - direction * (roadWidth * 0.5f);
        heading = Right(direction) * side;
    }

    /// <summary>Where an arm ends and which way it heads there, for a merge. `face` is the middle of the single road's
    /// start edge. Left / right arms end on the road's side half a width past its start, heading at it.</summary>
    public static void ArmEnd(Vector2 face, Vector2 direction, float slot, float roadWidth, out Vector2 end, out Vector2 heading)
    {
        float side = Side(slot);
        if (side == 0f) { end = face; heading = direction; return; }
        end = face + Right(direction) * (side * roadWidth * 0.5f) + direction * (roadWidth * 0.5f);
        heading = -Right(direction) * side;
    }

    /// <summary>The arm's line from its start point: `lead` metres along its heading, then the drawn line from where it
    /// is `lead` along that heading (the drawn part behind that is dropped).</summary>
    public static List<Vector3> LeadIn(List<Vector3> points, Vector2 start, Vector2 heading, float lead)
    {
        float y = points[0].y;
        int first = 0;
        while (first < points.Count - 2 && Vector2.Dot(Flat(points[first]) - start, heading) < lead * 1.5f) first++;
        var result = new List<Vector3> { At(start, y), At(start + heading * lead, y) };
        for (int i = first; i < points.Count; i++) result.Add(points[i]);
        return result;
    }

    /// <summary>The arm's line towards its end point: the drawn line up to where it is `lead` short of the end along the
    /// heading, then `lead` metres straight onto the end point.</summary>
    public static List<Vector3> LeadOut(List<Vector3> points, Vector2 end, Vector2 heading, float lead)
    {
        float y = points[0].y;
        int last = points.Count - 1;
        while (last > 1 && Vector2.Dot(end - Flat(points[last]), heading) < lead * 1.5f) last--;
        var result = new List<Vector3>();
        for (int i = 0; i <= last; i++) result.Add(points[i]);
        result.Add(At(end - heading * lead, y));
        result.Add(At(end, y));
        return result;
    }

    private static float Side(float slot) => slot < -0.5f ? -1f : slot > 0.5f ? 1f : 0f;
    private static Vector2 Right(Vector2 direction) => new Vector2(direction.y, -direction.x);
    private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    private static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);
}
