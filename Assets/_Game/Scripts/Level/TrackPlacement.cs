using System;
using UnityEngine;

// Quarter turns relative to the road's forward direction (from the first knot towards the last).
// The value is the angle in degrees, clockwise seen from above.
// BoardStack lays its rows along its local X, so at 0° rows go across the road, at 90° along it.
public enum TrackFacing
{
    [InspectorName("0°  (rows across the road)")] Forward = 0,
    [InspectorName("90°  (rows along the road)")] Right = 90,
    [InspectorName("180°  (rows across, reversed)")] Backward = 180,
    [InspectorName("270°  (rows along, reversed)")] Left = 270,
}

// Where something sits on a road, described by the track instead of world coordinates,
// so it stays in the right spot when the spline is edited.
[Serializable]
public struct TrackPlacement
{
    public SplineRoad road;

    [Tooltip("Permanent ID of the spline (section) inside the road. Picked from the section list in the Inspector.")]
    public int sectionId;

    [Tooltip("Distance along the section from its first knot, in the road's local units (= metres at scale 1). " +
             "Clamped to the section's length; wraps around on a closed loop.")]
    [Min(0f)] public float distance;

    [Tooltip("Across the road: -1 = left edge, 0 = centre, 1 = right edge (as seen driving from the first knot).")]
    [Range(-1f, 1f)] public float side;

    [Tooltip("Quarter turn relative to the road at this point. Stays relative on curves.")]
    public TrackFacing facing;

    [Tooltip("Fine turn in degrees, added on top of Facing. Leave 0 unless you need a specific angle.")]
    public float yaw;

    [Tooltip("Lift above the road surface.")]
    public float height;

    public const float DefaultHeight = 0.1f;

    // Start values for a new placement. Unity doesn't run struct constructors when loading saved data,
    // and C# 9 (Unity 6) has no parameterless struct constructors or field initializers — so owners
    // start from this in their own field initializer: `TrackPlacement placement = TrackPlacement.Default;`
    public static TrackPlacement Default => new TrackPlacement(DefaultHeight);

    public TrackPlacement(float height) : this()
    {
        this.height = height;
    }
}
