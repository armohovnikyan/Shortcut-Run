using UnityEngine;

public enum NavigationState { FollowingRoute, Shortcutting, Finished }

/// <summary>
/// Pure state and math for checkpoint progress and the shortcut decision.
/// Never touches NavMeshAgent or transform — NPC reads State/ShortcutTarget
/// each frame and acts on them. Checkpoints are NOT the path the agent walks;
/// they're only used to judge progress and spot shortcut opportunities.
/// </summary>
public class WaypointNavigator
{
    private readonly Vector3[] _checkpoints;
    private readonly float _checkpointReachedSqrDistance;
    private readonly float _shortcutReachedSqrDistance;

    private int _currentIndex;

    public NavigationState State { get; private set; } = NavigationState.FollowingRoute;
    public Vector3 ShortcutTarget => _checkpoints[_currentIndex];

    public WaypointNavigator(Vector3[] checkpoints, int startIndex = 0,
        float checkpointReachedSqrDistance = 81f, float shortcutReachedSqrDistance = 64f)
    {
        _checkpoints = checkpoints;
        _currentIndex = Mathf.Clamp(startIndex, 0, checkpoints.Length - 1);
        _checkpointReachedSqrDistance = checkpointReachedSqrDistance;
        _shortcutReachedSqrDistance = shortcutReachedSqrDistance;
    }

    /// <summary>Call every frame while the race is running. May change State.</summary>
    public void Tick(Vector3 currentPosition, int boardsHeld)
    {
        if (State == NavigationState.Finished) return;

        if (State == NavigationState.Shortcutting)
        {
            if (SqrDistanceXZ(currentPosition, _checkpoints[_currentIndex]) < _shortcutReachedSqrDistance)
                State = NavigationState.FollowingRoute;
            return;
        }

        if (_currentIndex >= _checkpoints.Length - 1)
        {
            State = NavigationState.Finished;
            return;
        }

        if (SqrDistanceXZ(currentPosition, _checkpoints[_currentIndex]) >= _checkpointReachedSqrDistance)
            return;

        _currentIndex++;
        if (_currentIndex >= _checkpoints.Length - 1) return; // Finished picked up next Tick

        int bestIndex = FindBestShortcutIndex(currentPosition, boardsHeld);
        if (bestIndex > _currentIndex)
        {
            _currentIndex = bestIndex;
            State = NavigationState.Shortcutting;
        }
    }

    private int FindBestShortcutIndex(Vector3 currentPosition, int boardsHeld)
    {
        int bestIndex = _currentIndex;
        int startIndex = _currentIndex + 2;
        if (startIndex > _checkpoints.Length - 3) return bestIndex;

        for (int i = startIndex; i < _checkpoints.Length - 3; i++)
        {
            float dist = Vector3.Distance(currentPosition, _checkpoints[i]);
            if (dist > boardsHeld * 2f) continue;
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