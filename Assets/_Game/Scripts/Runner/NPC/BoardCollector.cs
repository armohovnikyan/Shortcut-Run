using System;
using UnityEngine;

/// <summary>
/// Pure logic of one NPC's board collecting (no agent, no transform). NPC calls Tick every frame and walks
/// to Target when it is not null. Armed once the NPC held ArmBoards; collects while at/below CollectAtOrBelow
/// until it is back at ArmBoards; priority while at/below PriorityAtOrBelow (same refill span).
/// Only boards ahead on the route are picked, so the detour never pulls the NPC backwards.
/// </summary>
public class BoardCollector
{
    private readonly NpcBoardCollectingSO settings;
    private readonly CollectableBoard[] boards;
    private readonly Vector3[] checkpoints;

    private bool armed;
    private float scanTimer;
    private float targetTimeLeft;

    public bool Refilling { get; private set; }
    public bool Priority { get; private set; }
    public CollectableBoard Target { get; private set; }

    /// <summary>In priority mode with a board to go for: the NPC should not start a shortcut now.</summary>
    public bool SuppressShortcuts => Priority && Target != null;

    public BoardCollector(NpcBoardCollectingSO settings, CollectableBoard[] boards, Vector3[] checkpoints)
    {
        this.settings = settings;
        this.boards = boards;
        this.checkpoints = checkpoints;
    }

    /// <param name="routeIndex">Checkpoint the NPC is heading to.</param>
    /// <param name="followingRoute">False while shortcutting — no detours then.</param>
    /// <param name="isReachable">NavMesh check for a candidate position.</param>
    public void Tick(Vector3 position, int boardCount, int routeIndex, bool followingRoute, float deltaTime,
                     Func<Vector3, bool> isReachable)
    {
        UpdateStock(boardCount);

        if (!Refilling || !followingRoute)
        {
            Target = null;
            return;
        }

        if (Target != null)
        {
            targetTimeLeft -= deltaTime;
            if (!Target.IsAvailable || targetTimeLeft <= 0f) Target = null;
        }
        if (Target != null) return;

        scanTimer -= deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = settings.ScanInterval;

        Target = FindBoard(position, routeIndex, isReachable);
        targetTimeLeft = settings.GiveUpAfter;
    }

    private void UpdateStock(int count)
    {
        if (!armed && count >= settings.ArmBoards) armed = true;
        if (!armed) return;

        if (!Refilling && count <= settings.CollectAtOrBelow)
            Refilling = true;
        else if (Refilling && count >= settings.ArmBoards)
        {
            Refilling = false;
            Priority = false;
        }

        if (Refilling && count <= settings.PriorityAtOrBelow) Priority = true;
    }

    // Nearest available board that lies ahead, close to the next few checkpoints, on this level of the track.
    private CollectableBoard FindBoard(Vector3 position, int routeIndex, Func<Vector3, bool> isReachable)
    {
        Vector3 forward = checkpoints[Mathf.Clamp(routeIndex, 0, checkpoints.Length - 1)] - position;
        forward.y = 0f;
        float maxSqr = settings.MaxDetour * settings.MaxDetour;
        int lastIndex = Mathf.Min(checkpoints.Length - 1, routeIndex + settings.LookAheadCheckpoints);

        CollectableBoard best = null;
        float bestSqr = float.MaxValue;
        foreach (CollectableBoard board in boards)
        {
            if (board == null || !board.IsAvailable) continue;

            Vector3 boardPos = board.transform.position;
            if (Mathf.Abs(boardPos.y - position.y) > settings.MaxHeightDifference) continue;

            Vector3 offset = boardPos - position;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr > maxSqr || sqr >= bestSqr) continue;
            if (Vector3.Dot(offset, forward) <= 0f) continue; // behind

            if (!IsOnForwardRoute(boardPos, routeIndex, lastIndex, maxSqr)) continue;
            if (!isReachable(boardPos)) continue;

            best = board;
            bestSqr = sqr;
        }
        return best;
    }

    // Its nearest checkpoint must be inside the look-ahead window and not far off the route.
    private bool IsOnForwardRoute(Vector3 boardPos, int fromIndex, int toIndex, float maxSqr)
    {
        int nearest = 0;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            float sqr = (checkpoints[i] - boardPos).sqrMagnitude;
            if (sqr < nearestSqr) { nearestSqr = sqr; nearest = i; }
        }
        return nearest >= fromIndex && nearest <= toIndex && nearestSqr <= maxSqr * 4f;
    }
}
