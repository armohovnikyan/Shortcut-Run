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
    private readonly float _minShortcutSaving;
    private readonly float[] _routeDistance; // metres along the route from checkpoint 0 to each checkpoint

    private int _currentIndex;

    public NavigationState State { get; private set; } = NavigationState.FollowingRoute;
    public int CurrentIndex => _currentIndex;
    public Vector3 CurrentCheckpoint => _checkpoints[_currentIndex];

    public WaypointNavigator(Vector3[] checkpoints, int startIndex = 0,
        float checkpointReachedSqrDistance = 81f, float shortcutReachedSqrDistance = 2.25f, float minShortcutSaving = 5f)
    {
        _checkpoints = checkpoints;
        _currentIndex = Mathf.Clamp(startIndex, 0, checkpoints.Length - 1);
        _checkpointReachedSqrDistance = checkpointReachedSqrDistance;
        _shortcutReachedSqrDistance = shortcutReachedSqrDistance;
        _minShortcutSaving = minShortcutSaving;

        _routeDistance = new float[checkpoints.Length];
        for (int i = 1; i < checkpoints.Length; i++)
            _routeDistance[i] = _routeDistance[i - 1] + Vector3.Distance(checkpoints[i - 1], checkpoints[i]);
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

        // Checked every frame, not only when a checkpoint is reached: the chance to cut across a loop
        // opens and closes as the NPC runs and as its board count changes.
        int bestIndex = FindBestShortcutIndex(currentPosition, bridgeReach);
        if (bestIndex > _currentIndex)
        {
            _currentIndex = bestIndex;
            State = NavigationState.Shortcutting;
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
    /// The NPC is standing on real road again after crossing a gap — go back to following the route
    /// from here. Normally ends a shortcut long before the checkpoint itself is reached.
    /// </summary>
    public void EndShortcut()
    {
        if (State == NavigationState.Shortcutting)
            State = NavigationState.FollowingRoute;
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

    // Farthest checkpoint the boards in hand can reach in a straight line, if going straight saves at
    // least _minShortcutSaving metres over following the road. Without the saving rule a straight road
    // would count as a "shortcut" too. The last checkpoints (road end, finish) are always walked.
    private int FindBestShortcutIndex(Vector3 currentPosition, float bridgeReach)
    {
        int bestIndex = _currentIndex;
        if (bridgeReach <= 0f) return bestIndex;

        float toCurrent = Vector3.Distance(currentPosition, CurrentCheckpoint);
        for (int i = _currentIndex + 1; i < _checkpoints.Length - 3; i++)
        {
            float straight = Vector3.Distance(currentPosition, _checkpoints[i]);
            if (straight > bridgeReach) continue;

            float alongRoute = toCurrent + _routeDistance[i] - _routeDistance[_currentIndex];
            if (alongRoute - straight >= _minShortcutSaving) bestIndex = i;
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
