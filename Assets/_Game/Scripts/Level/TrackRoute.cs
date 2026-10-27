using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// The race order through a SplineRoad: one leg per section, in the container's spline order.
/// - Open section: run end to end. Drawn backwards? Detected from which end touches the previous leg.
/// - Closed section (loop) = a FORK, not a lap: runners enter where the previous leg meets it, leave where
///   the next leg meets it, and may go either way round (or bridge across the middle).
/// - Arm fork = consecutive open sections that all START where the previous leg ended (a fork built from
///   separate arm splines, e.g. by the LevelDrawer). Runners take any one arm; the next leg follows on
///   from the arms' far ends. A normal next section starts at the previous one's END, so it never matches.
/// Answers two questions: "how far from the finish is this point?" (race places) and
/// "which points should an NPC pass?" (checkpoints). Pure math — creates and moves nothing.
/// </summary>
public class TrackRoute
{
    private struct Arm
    {
        public int section;
        public bool reversed;
        public float length;
    }

    private struct Leg
    {
        public int section;        // spline index in the container (loop / open leg)
        public bool isLoop;
        public bool reversed;      // open section drawn against the race direction
        public float length;       // whole spline length
        public float entry, exit;  // loop only: distances along the loop where runners come in / leave
        public Arm[] arms;         // arm fork only (then section/reversed/length are unused)
        public float run;          // metres to get through this leg (loop / arm fork = the shortest way)
    }

    private readonly SplineRoad road;
    private readonly List<Leg> legs = new List<Leg>();
    private float[] remainingAfter; // route metres left after leg i

    public float TotalLength { get; private set; }
    public bool IsValid => legs.Count > 0;

    public TrackRoute(SplineRoad road)
    {
        this.road = road;
        if (road != null) Build();
    }

    // ---------- Queries ----------

    /// <summary>Metres left to the finish along the route (shortest way through loops). Lower = further ahead.</summary>
    public float GetRemainingDistance(Vector3 worldPosition)
    {
        if (!IsValid) return float.PositiveInfinity;

        float3 local = road.transform.InverseTransformPoint(worldPosition);
        float best = float.PositiveInfinity, bestGap = float.PositiveInfinity;

        for (int i = 0; i < legs.Count; i++)
        {
            Leg leg = legs[i];
            if (leg.arms != null)
            {
                foreach (Arm arm in leg.arms)
                {
                    Spline spline = road.Container.Splines[arm.section];
                    float gap = SplineUtility.GetNearestPoint(spline, local, out _, out float t);
                    if (gap >= bestGap) continue;
                    float d = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Distance);
                    bestGap = gap;
                    best = (arm.reversed ? d : arm.length - d) + remainingAfter[i];
                }
                continue;
            }

            float legGap = SplineUtility.GetNearestPoint(SplineOf(leg), local, out _, out float legT);
            if (legGap >= bestGap) continue;
            float along = SplineOf(leg).ConvertIndexUnit(legT, PathIndexUnit.Normalized, PathIndexUnit.Distance);
            float inLeg = leg.isLoop
                ? Mathf.Min(Mathf.Repeat(leg.exit - along, leg.length), Mathf.Repeat(along - leg.exit, leg.length))
                : (leg.reversed ? along : leg.length - along);
            bestGap = legGap;
            best = inLeg + remainingAfter[i];
        }
        return best;
    }

    /// <summary>
    /// Points along the road centre, in race order, about `spacing` metres apart.
    /// takeForwardSide is asked once per fork: for a loop true = go round in the spline's direction,
    /// false = the other way; for an arm fork true = the first arm, false = another one.
    /// </summary>
    public Vector3[] SampleCheckpoints(float spacing, Func<bool> takeForwardSide)
    {
        var points = new List<Vector3>();
        spacing = Mathf.Max(0.5f, spacing);

        foreach (Leg leg in legs)
        {
            if (leg.arms != null)
            {
                // false picks among the other arms at random — with the usual two arms that's simply the second.
                Arm arm = takeForwardSide() ? leg.arms[0] : leg.arms[UnityEngine.Random.Range(1, leg.arms.Length)];
                AddSamples(points, arm.length, spacing, s => arm.reversed ? arm.length - s : s, arm.section);
            }
            else if (leg.isLoop)
            {
                int direction = takeForwardSide() ? 1 : -1;
                float side = SideLength(leg, direction);
                AddSamples(points, side, spacing, s => Mathf.Repeat(leg.entry + direction * s, leg.length), leg.section);
            }
            else
            {
                AddSamples(points, leg.length, spacing, s => leg.reversed ? leg.length - s : s, leg.section);
            }
        }
        return points.ToArray();
    }

    // ---------- Building ----------

    private void Build()
    {
        IReadOnlyList<Spline> splines = road.Container.Splines;
        var usable = new List<int>();
        for (int i = 0; i < splines.Count; i++)
            if (splines[i].Count >= 2) usable.Add(i);

        float3? handover = null; // where the previous leg ends, in road-local space
        float handoverWidth = 0f; // the previous leg's road width

        for (int n = 0; n < usable.Count; n++)
        {
            Spline spline = splines[usable[n]];

            int armCount = handover.HasValue ? CountArms(usable, n, handover.Value, handoverWidth) : 1;
            if (armCount >= 2)
            {
                var leg = new Leg { arms = new Arm[armCount], run = float.PositiveInfinity };
                float3 exitSum = float3.zero;
                for (int a = 0; a < armCount; a++)
                {
                    Spline armSpline = splines[usable[n + a]];
                    float3 start = armSpline.EvaluatePosition(0f), end = armSpline.EvaluatePosition(1f);
                    bool reversed = math.distancesq(end, handover.Value) < math.distancesq(start, handover.Value);
                    float length = armSpline.GetLength();
                    leg.arms[a] = new Arm { section = usable[n + a], reversed = reversed, length = length };
                    leg.run = Mathf.Min(leg.run, length);
                    exitSum += reversed ? start : end;
                }
                handover = exitSum / armCount; // the arms meet again here (or nearly — they end on the next road's sides)
                handoverWidth = road.GetWidth(usable[n]);
                legs.Add(leg);
                n += armCount - 1;
                continue;
            }

            Spline next = n + 1 < usable.Count ? splines[usable[n + 1]] : null;
            var single = new Leg { section = usable[n], length = spline.GetLength(), isLoop = spline.Closed };

            if (single.isLoop)
            {
                single.entry = handover.HasValue ? NearestDistance(spline, handover.Value) : 0f;
                if (next != null)
                {
                    single.exit = NearestDistance(spline, EndNearest(next, spline));
                }
                else
                {
                    single.exit = single.entry;
                    Debug.LogWarning($"{road.name}: the last section is a loop, so it has no exit — " +
                                     "the route treats it as one full lap. End the track with an open section.", road);
                }
                single.run = Mathf.Min(SideLength(single, 1), SideLength(single, -1));
                handover = spline.EvaluatePosition(ToT(spline, single.exit));
            }
            else
            {
                float3 start = spline.EvaluatePosition(0f), end = spline.EvaluatePosition(1f);
                if (handover.HasValue)
                    single.reversed = math.distancesq(end, handover.Value) < math.distancesq(start, handover.Value);
                else if (next != null)
                    single.reversed = DistanceTo(next, start) < DistanceTo(next, end); // start touches the next leg = drawn backwards
                single.run = single.length;
                handover = single.reversed ? start : end;
            }
            handoverWidth = road.GetWidth(usable[n]);
            legs.Add(single);
        }

        remainingAfter = new float[legs.Count];
        float sum = 0f;
        for (int i = legs.Count - 1; i >= 0; i--)
        {
            remainingAfter[i] = sum;
            sum += legs[i].run;
        }
        TotalLength = sum;
    }

    // How many open sections from usable[n] on all have an end at the handover point = arms of one fork.
    // "At" = within about a road width: drawn arms end on the incoming road's sides, not on its centre line.
    private int CountArms(List<int> usable, int n, float3 handover, float handoverWidth)
    {
        IReadOnlyList<Spline> splines = road.Container.Splines;
        int count = 0;
        while (n + count < usable.Count)
        {
            Spline spline = splines[usable[n + count]];
            if (spline.Closed) break;
            float reach = Mathf.Max(handoverWidth, road.GetWidth(usable[n + count])) * 1.5f;
            float3 start = spline.EvaluatePosition(0f), end = spline.EvaluatePosition(1f);
            float nearest = Mathf.Sqrt(Mathf.Min(math.distancesq(start, handover), math.distancesq(end, handover)));
            float farthest = Mathf.Sqrt(Mathf.Max(math.distancesq(start, handover), math.distancesq(end, handover)));
            if (nearest > reach || farthest <= reach) break; // a stub with both ends at the junction isn't an arm
            count++;
        }
        return count;
    }

    // ---------- Helpers ----------

    private Spline SplineOf(Leg leg) => road.Container.Splines[leg.section];

    // Length of one side of a loop, from entry to exit. Entry == exit means a full lap.
    private static float SideLength(Leg leg, int direction)
    {
        float side = direction > 0
            ? Mathf.Repeat(leg.exit - leg.entry, leg.length)
            : Mathf.Repeat(leg.entry - leg.exit, leg.length);
        return side < 0.01f ? leg.length : side;
    }

    // s = metres travelled into the leg -> distance along the spline. Always includes the leg's last point.
    private void AddSamples(List<Vector3> points, float legRun, float spacing, Func<float, float> toDistance, int section)
    {
        Spline spline = road.Container.Splines[section];
        for (float s = 0f; s < legRun; s += spacing)
            AddPoint(points, WorldPoint(spline, toDistance(s)), spacing);
        AddPoint(points, WorldPoint(spline, toDistance(legRun)), spacing);
    }

    // Skips a point sitting on top of the previous one (where two legs meet).
    private static void AddPoint(List<Vector3> points, Vector3 point, float spacing)
    {
        if (points.Count > 0 && (points[points.Count - 1] - point).sqrMagnitude < spacing * spacing * 0.25f) return;
        points.Add(point);
    }

    private Vector3 WorldPoint(Spline spline, float distance)
    {
        Vector3 local = (Vector3)spline.EvaluatePosition(ToT(spline, distance));
        return road.transform.TransformPoint(local);
    }

    private static float ToT(Spline spline, float distance) =>
        spline.ConvertIndexUnit(distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);

    private static float NearestDistance(Spline spline, float3 point)
    {
        SplineUtility.GetNearestPoint(spline, point, out _, out float t);
        return spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Distance);
    }

    private static float DistanceTo(Spline spline, float3 point) =>
        SplineUtility.GetNearestPoint(spline, point, out _, out _);

    // The end of `section` that touches `other` — where a loop hands over to the next section.
    private static float3 EndNearest(Spline section, Spline other)
    {
        float3 start = section.EvaluatePosition(0f), end = section.EvaluatePosition(1f);
        return DistanceTo(other, start) <= DistanceTo(other, end) ? start : end;
    }
}
