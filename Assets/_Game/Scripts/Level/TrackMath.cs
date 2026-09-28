using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

// Converts between TrackPlacement (section, distance, side) and world position/rotation.
// Pure math over a SplineRoad — no scene objects are created or moved here.
public static class TrackMath
{
    // Road-relative placement -> world pose. False if the road or its section is missing.
    public static bool TryEvaluate(in TrackPlacement placement, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        SplineRoad road = placement.road;
        if (road == null) return false;
        int index = road.FindSectionIndex(placement.sectionId);
        if (index < 0) return false;
        Spline spline = road.Container.Splines[index];
        if (spline.Count < 2) return false;

        float length = spline.GetLength();
        float distance = spline.Closed
            ? Mathf.Repeat(placement.distance, length)
            : Mathf.Clamp(placement.distance, 0f, length);
        // Distance -> normalized t through the spline's own length table, so equal distances
        // are equal metres even where knots are unevenly spaced.
        float t = spline.ConvertIndexUnit(distance, PathIndexUnit.Distance, PathIndexUnit.Normalized);

        GetFrame(spline, t, out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right);
        Vector3 local = center
                        + right * (placement.side * road.GetWidth(index) * 0.5f)
                        + up * placement.height;

        Transform roadTransform = road.transform;
        position = roadTransform.TransformPoint(local);
        // Turn around the road's own up axis, so Facing stays relative to the road on curves and slopes.
        float turn = (float)placement.facing + placement.yaw;
        rotation = roadTransform.rotation * Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, turn, 0f);
        return true;
    }

    // World point -> nearest spot on any section of the road. Fills road, section, distance and side;
    // leaves yaw and height as they were. False if the road has no usable section.
    public static bool TryProject(SplineRoad road, Vector3 worldPoint, ref TrackPlacement placement)
    {
        if (road == null) return false;

        float3 local = road.transform.InverseTransformPoint(worldPoint);
        int bestIndex = -1;
        float bestT = 0f, bestDistance = float.PositiveInfinity;

        for (int i = 0; i < road.SectionCount; i++)
        {
            Spline spline = road.Container.Splines[i];
            if (spline.Count < 2) continue;

            float distance = SplineUtility.GetNearestPoint(spline, local, out _, out float t);
            if (distance < bestDistance) { bestDistance = distance; bestIndex = i; bestT = t; }
        }
        if (bestIndex < 0) return false;

        Spline best = road.Container.Splines[bestIndex];
        GetFrame(best, bestT, out Vector3 center, out _, out _, out Vector3 right);
        float halfWidth = road.GetWidth(bestIndex) * 0.5f;

        placement.road = road;
        placement.sectionId = road.GetSectionId(bestIndex);
        placement.distance = best.ConvertIndexUnit(bestT, PathIndexUnit.Normalized, PathIndexUnit.Distance);
        placement.side = Mathf.Clamp(Vector3.Dot((Vector3)local - center, right) / halfWidth, -1f, 1f);
        return true;
    }

    // Mouse ray -> spot on the road surface under it. Finds the section whose centre line passes
    // closest to the ray, hits that section's surface plane, then projects the hit like TryProject.
    // offRoad = how far outside the road's edge the hit landed (0 = on the road).
    // Spline math, not physics: physics raycasts are unreliable in Prefab Mode (separate physics scene).
    public static bool TryProjectRay(SplineRoad road, Ray worldRay, ref TrackPlacement placement, out float offRoad)
    {
        offRoad = float.PositiveInfinity;
        if (road == null) return false;

        Transform roadTransform = road.transform;
        var localRay = new Ray(roadTransform.InverseTransformPoint(worldRay.origin),
                               roadTransform.InverseTransformDirection(worldRay.direction));
        int bestIndex = -1;
        float bestT = 0f, bestDistance = float.PositiveInfinity;

        for (int i = 0; i < road.SectionCount; i++)
        {
            Spline spline = road.Container.Splines[i];
            if (spline.Count < 2) continue;

            float distance = SplineUtility.GetNearestPoint(spline, localRay, out _, out float t);
            if (distance < bestDistance) { bestDistance = distance; bestIndex = i; bestT = t; }
        }
        if (bestIndex < 0) return false;

        GetFrame(road.Container.Splines[bestIndex], bestT, out Vector3 center, out _, out Vector3 up, out Vector3 right);
        if (!new Plane(up, center).Raycast(localRay, out float enter)) return false;

        Vector3 hit = localRay.GetPoint(enter);
        offRoad = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(hit - center, right)) - road.GetWidth(bestIndex) * 0.5f);
        return TryProject(road, roadTransform.TransformPoint(hit), ref placement);
    }

    // The road something new most likely belongs to: one of its parents, else the first road in its scene/prefab.
    public static SplineRoad FindRoad(GameObject placed)
    {
        SplineRoad road = placed.GetComponentInParent<SplineRoad>();
        if (road != null) return road;

        foreach (GameObject root in placed.scene.GetRootGameObjects())
        {
            road = root.GetComponentInChildren<SplineRoad>(true);
            if (road != null) return road;
        }
        return null;
    }

    // Same frame SplineRoad builds the mesh with, so placements line up with the baked road exactly.
    private static void GetFrame(Spline spline, float t,
        out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right)
    {
        spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 upward);
        center = pos;
        forward = ((Vector3)tangent).normalized;
        if (forward == Vector3.zero) forward = Vector3.forward;
        up = ((Vector3)upward).normalized;
        if (up == Vector3.zero) up = Vector3.up;
        right = Vector3.Cross(up, forward).normalized;
    }
}
