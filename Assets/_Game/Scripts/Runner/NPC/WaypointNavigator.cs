using UnityEngine;

public enum NavigationState { FollowingRoute, Shortcutting, Finished }

/// <summary>
/// Pure state and math for checkpoint progress and the shortcut decision.
/// Never touches NavMeshAgent or transform — NPC reads State / CurrentCheckpoint each frame and acts.
/// FollowingRoute: the agent walks to CurrentCheckpoint (always one just ahead, never the far end,
/// so it can't pick "the other way round" a loop). Shortcutting: straight line to CurrentCheckpoint.
/// </summary>
public class WaypointNavigator
{
    private readonly Vector3[] _checkpoints;
    private readonly float _checkpointReachedSqrDistance;
    private readonly float _shortcutReachedSqrDistance;

    private int _currentIndex;

    public NavigationState State { get; private set; } = NavigationState.FollowingRoute;
    public int CurrentIndex => _currentIndex;
    public Vector3 CurrentCheckpoint => _checkpoints[_currentIndex];

    public WaypointNavigator(Vector3[] checkpoints, int startIndex = 0,
        float checkpointReachedSqrDistance = 81f, float shortcutReachedSqrDistance = 64f)
    {
        _checkpoints = checkpoints;
        _currentIndex = Mathf.Clamp(startIndex, 0, checkpoints.Length - 1);
        _checkpointReachedSqrDistance = checkpointReachedSqrDistance;
        _shortcutReachedSqrDistance = shortcutReachedSqrDistance;
    }

    /// <summary>
    /// Call every frame while the race is running. May change State.
    /// bridgeReach = metres the boards in hand can cover.
    /// </summary>
    public void Tick(Vector3 currentPosition, float bridgeReach)
    {
        if (State == NavigationState.Finished) return;

        if (State == NavigationState.Shortcutting)
        {
            if (SqrDistanceXZ(currentPosition, CurrentCheckpoint) < _shortcutReachedSqrDistance)
                State = NavigationState.FollowingRoute;
            return;
        }

        if (SqrDistanceXZ(currentPosition, CurrentCheckpoint) >= _checkpointReachedSqrDistance)
            return;

        if (_currentIndex >= _checkpoints.Length - 1)
        {
            State = NavigationState.Finished;
            return;
        }

        _currentIndex++;

        int bestIndex = FindBestShortcutIndex(currentPosition, bridgeReach);
        if (bestIndex > _currentIndex)
        {
            _currentIndex = bestIndex;
            State = NavigationState.Shortcutting;
        }
    }

    /// <summary>
    /// The NavMesh has no path to the current checkpoint (a gap between road sections) —
    /// cross it in a straight line instead, bridging or jumping like any shortcut.
    /// </summary>
    public void ForceShortcut()
    {
        if (State == NavigationState.FollowingRoute)
            State = NavigationState.Shortcutting;
    }

    /// <summary>
    /// First checkpoint still ahead of position: the nearest one, or the one after it if position
    /// is already past it. Lets a runner that starts mid-track join the route instead of running back.
    /// </summary>
    public static int NextIndexFrom(Vector3[] checkpoints, Vector3 position)
    {
        int nearest = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            float sqr = (checkpoints[i] - position).sqrMagnitude; // 3D: a loop passing above/below isn't "near"
            if (sqr < bestSqr) { bestSqr = sqr; nearest = i; }
        }

        if (nearest >= checkpoints.Length - 1) return nearest;

        Vector3 along = checkpoints[nearest + 1] - checkpoints[nearest];
        Vector3 fromNearest = position - checkpoints[nearest];
        return Vector3.Dot(fromNearest, along) > 0f ? nearest + 1 : nearest;
    }

    private int FindBestShortcutIndex(Vector3 currentPosition, float bridgeReach)
    {
        int bestIndex = _currentIndex;
        int startIndex = _currentIndex + 2;
        if (startIndex > _checkpoints.Length - 3) return bestIndex;

        for (int i = startIndex; i < _checkpoints.Length - 3; i++)
        {
            float dist = Vector3.Distance(currentPosition, _checkpoints[i]);
            if (dist > bridgeReach) continue;
            if (i > bestIndex) bestIndex = i;
        }

        return bestIndex;
    }

    private static float SqrDistanceXZ(Vector3 a, Vector3 b)
    {
        Vector3 dir = a - b;
        dir.y = 0f;
        return dir.sqrMagnitude;
    }
}
