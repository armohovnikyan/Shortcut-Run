using System;
using UnityEngine;

/// <summary>
/// Raycasts only: what is under the feet, and is there road ahead to climb onto.
/// Knows nothing about states, boards in hand or animations.
/// </summary>
[Serializable]
public class GroundProbe
{
    [Space]
    [Tooltip("Road, jump pads and placed boards. NOT the collectable board layer.")]
    [SerializeField] private LayerMask roadLayer;
    [Tooltip("Ray starts this high above the feet, so ground slightly above the feet is still found.")]
    [SerializeField] private float rayOriginHeight = 0.5f;
    [Tooltip("How far below the feet still counts as standing on something.")]
    [SerializeField] private float groundCheckDistance = 1.2f;
    [Tooltip("How far ahead to look for road to climb onto when a jump ends over nothing.")]
    [SerializeField] private float grabRoadDistance = 8f;
    [Tooltip("Highest road edge the runner can still climb onto.")]
    [SerializeField] private float maxClimbHeight = 2f;

    public bool TryGetGround(Vector3 feet, out RaycastHit hit)
    {
        return Physics.Raycast(
            feet + Vector3.up * rayOriginHeight, Vector3.down, out hit,
            rayOriginHeight + groundCheckDistance, roadLayer, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Finds the top surface of the road edge in front. forward must be flat and normalized.</summary>
    public bool TryFindRoadAhead(Vector3 feet, Vector3 forward, out Vector3 grabPoint)
    {
        grabPoint = default;

        // Just below the feet, so it hits the side wall of a road at our height or higher.
        Vector3 origin = feet + Vector3.down * 0.1f;
        if (!Physics.Raycast(origin, forward, out RaycastHit wall, grabRoadDistance, roadLayer, QueryTriggerInteraction.Ignore))
            return false;

        // Step a little past the wall and look down for its top surface.
        Vector3 above = wall.point + forward * 0.3f + Vector3.up * maxClimbHeight;
        if (!Physics.Raycast(above, Vector3.down, out RaycastHit top, maxClimbHeight + 0.5f, roadLayer, QueryTriggerInteraction.Ignore))
            return false;

        grabPoint = top.point;
        return true;
    }
}
