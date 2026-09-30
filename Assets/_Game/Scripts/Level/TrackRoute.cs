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
/// Answers two questions: "how far from the finish is this point?" (race places) and
/// "which points should an NPC pass?" (checkpoints). Pure math — creates and moves nothing.
/// </summary>
public class TrackRoute
{
    private struct Leg
    {
        public int section;        // spline index in the container
        public bool isLoop;
        public bool reversed;      // open section drawn against the race direction
        public float length;       // whole spline length
        public float entry, exit;  // loop only: distances along the loop where runners come in / leave
        public float run;          // metres to get through this leg (loop = the shorter side)
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
        int bestLeg = 0;
        float bestGap = float.PositiveInfinity, bestT = 0f;

        for (int i = 0; i < legs.Count; i++)
        {
            float gap = SplineUtility.GetNearestPoint(SplineOf(legs[i]), local, out _, out float t);
            if (gap < bestGap) { bestGap = gap; bestLeg = i; bestT = t; }
        }

        Leg leg = legs[bestLeg];
        float d = SplineOf(leg).ConvertIndexUnit(bestT, PathIndexUnit.Normalized, PathIndexUnit.Distance);
        float inLeg = leg.isLoop
            ? Mathf.Min(Mathf.Repeat(leg.exit - d, leg.length), Mathf.Repeat(d - leg.exit, leg.length))
            : (leg.reversed ? d : leg.length - d);

        return inLeg + remainingAfter[bestLeg];
    }

    /// <summary>
    /// Points along the road centre, in race order, about `spacing` metres apart.
    /// takeForwardSide is asked once per loop: true = go round in the spline's direction, false = the other way.
    /// </summary>
    public Vector3[] SampleCheckpoints(float spacing, Func<bool> takeForwardSide)
    {
        var points = new List<Vector3>();
        spacing = Mathf.Max(0.5f, spacing);

        foreach (Leg leg in legs)
        {
            if (leg.isLoop)
            {
                int direction = takeForwardSide() ? 1 : -1;
                float side = SideLength(leg, direction);
                AddSamples(points, side, spacing, s => Mathf.Repeat(leg.entry + direction * s, leg.length), leg);
            }
            else
            {
                AddSamples(points, leg.length, spacing, s => leg.reversed ? leg.length - s : s, leg);
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

        for (int n = 0; n < usable.Count; n++)
        {
            Spline spline = splines[usable[n]];
            Spline next = n + 1 < usable.Count ? splines[usable[n + 1]] : null;
            var leg = new Leg { section = usable[n], length = spline.GetLength(), isLoop = spline.Closed };

            if (leg.isLoop)
            {
                leg.entry = handover.HasValue ? NearestDistance(spline, handover.Value) : 0f;
                if (next != null)
                {
                    leg.exit = NearestDistance(spline, EndNearest(next, spline));
                }
                else
                {
                    leg.exit = leg.entry;
                    Debug.LogWarning($"{road.name}: the last section is a loop, so it has no exit — " +
                                     "the route treats it as one full lap. End the track with an open section.", road);
                }
                leg.run = Mathf.Min(SideLength(leg, 1), SideLength(leg, -1));
                handover = spline.EvaluatePosition(ToT(spline, leg.exit));
            }
            else
            {
                float3 start = spline.EvaluatePosition(0f), end = spline.EvaluatePosition(1f);
                if (handover.HasValue)
                    leg.reversed = math.distancesq(end, handover.Value) < math.distancesq(start, handover.Value);
                else if (next != null)
                    leg.reversed = DistanceTo(next, start) < DistanceTo(next, end); // start touches the next leg = drawn backwards
                leg.run = leg.length;
                handover = leg.reversed ? start : end;
            }
            legs.Add(leg);
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
    private void AddSamples(List<Vector3> points, float legRun, float spacing, Func<float, float> toDistance, Leg leg)
    {
        Spline spline = SplineOf(leg);
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
