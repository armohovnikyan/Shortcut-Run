using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Root of a level prefab — the "arena". Holds what the run needs from the level (spawns, NPC setup,
/// finish, stand points, bonus platforms) and answers track questions through TrackRoute.
/// Owns no race logic: the run manager asks, the level answers.
/// </summary>
public class Level : MonoBehaviour
{
    [Header("Track")]
    [SerializeField] private SplineRoad road;
    [Tooltip("Distance between NPC checkpoints along the road, in metres.")]
    [SerializeField, Min(1f)] private float checkpointSpacing = 8f;

    [Header("Runners")]
    [Tooltip("The starting area. It holds the spawn points (player behind, NPCs ahead) and how many NPCs race.")]
    [SerializeField] private SpawnBox spawnBox;
    [Tooltip("NPC prefabs to pick from. Each spawned NPC takes a random one.")]
    [SerializeField] private NPC[] npcPrefabs;

    [Header("Finish")]
    [SerializeField] private FinishLine finishLine;
    [Tooltip("Where finished runners stand, in finishing order (1st place = element 0).")]
    [SerializeField] private Transform[] standPoints;
    [Tooltip("Where the finish camera sits, position AND rotation (the finish platform's CameraPos). Same view " +
             "whichever stand point the player gets. Empty = the camera just stops where it stopped following.")]
    [SerializeField] private Transform finishViewPoint;
    [Tooltip("The bonus multiplier platforms after the finish; they rise when the player comes 1st. " +
             "Empty = this level has no bonus: 1st place just finishes at x1.")]
    [SerializeField] private BonusPlatformLayout bonusPlatforms;

    private TrackRoute route;

    public TrackRoute Route => route ??= new TrackRoute(road);
    public SpawnBox SpawnBox => spawnBox;
    public NPC[] NpcPrefabs => npcPrefabs;

    /// <summary>Where the runners of one run start: the player first, then the NPCs. Rolled again on every call
    /// (NPC count between the box's Min and Max, points shaken by the zones' noise). Not valid = nothing to spawn the player at.</summary>
    public struct SpawnPlan
    {
        public bool valid;
        public Pose player;
        public List<Pose> npcs;
    }

    public SpawnPlan CreateSpawnPlan()
    {
        if (spawnBox == null) return default;
        var plan = new SpawnPlan { npcs = new List<Pose>() };
        plan.valid = spawnBox.Roll(out plan.player, plan.npcs);
        return plan;
    }

    public FinishLine FinishLine => finishLine;
    public Transform[] StandPoints => standPoints;
    public Transform FinishViewPoint => finishViewPoint;
    public BonusPlatformLayout BonusPlatforms => bonusPlatforms;

    /// <summary>Every jump pad picks one of its own spots at random. Called on each Prepare, so a replay
    /// (after a fall, or a wiped save) re-rolls them even when it's the same level instance.</summary>
    public void PlaceJumpPads()
    {
        foreach (JumpPadSpots pad in GetComponentsInChildren<JumpPadSpots>(true)) pad.PlaceRandom();
    }

    /// <summary>Metres left to the finish along the track. Lower = further ahead in the race.</summary>
    public float GetRemainingDistance(Vector3 worldPosition) => Route.GetRemainingDistance(worldPosition);

    /// <summary>A fresh checkpoint list for one NPC. Each loop's side is picked at random, so NPCs spread out.
    /// The finish line's centre is added last: the road can end before the finish trigger, and an NPC
    /// that only walks to the road's end never crosses it.</summary>
    public Vector3[] BuildNpcCheckpoints()
    {
        Vector3[] points = Route.SampleCheckpoints(checkpointSpacing, () => Random.value < 0.5f);
        if (finishLine == null) return points;

        System.Array.Resize(ref points, points.Length + 1);
        points[points.Length - 1] = finishLine.GetComponent<Collider>().bounds.center;
        return points;
    }

    // Route preview: yellow = loops taken one way, cyan = the other way. Select the level to see it.
    private void OnDrawGizmosSelected()
    {
        if (road == null) return;
        var preview = new TrackRoute(road);
        DrawPoints(preview.SampleCheckpoints(checkpointSpacing, () => true), Color.yellow);
        DrawPoints(preview.SampleCheckpoints(checkpointSpacing, () => false), Color.cyan);
    }

    private static void DrawPoints(Vector3[] points, Color color)
    {
        Gizmos.color = color;
        for (int i = 0; i < points.Length; i++)
        {
            Gizmos.DrawSphere(points[i], 0.4f);
            if (i > 0) Gizmos.DrawLine(points[i - 1], points[i]);
        }
    }
}
