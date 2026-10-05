using UnityEngine;

/// <summary>
/// Root of a level prefab — the "arena". Holds what the run needs from the level (spawns, NPC setup,
/// finish, stand points, bonus anchor) and answers track questions through TrackRoute.
/// Owns no race logic: the run manager asks, the level answers.
/// </summary>
public class Level : MonoBehaviour
{
    [Header("Track")]
    [SerializeField] private SplineRoad road;
    [Tooltip("Distance between NPC checkpoints along the road, in metres.")]
    [SerializeField, Min(1f)] private float checkpointSpacing = 8f;

    [Header("Runners")]
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform[] npcSpawns;
    [Tooltip("NPC prefabs to pick from. Each spawned NPC takes a random one.")]
    [SerializeField] private NPC[] npcPrefabs;
    [Tooltip("How many NPCs race on this level. Capped by the number of NPC spawns.")]
    [SerializeField, Min(0)] private int npcCount = 4;

    [Header("Finish")]
    [SerializeField] private FinishLine finishLine;
    [Tooltip("Where finished runners stand, in finishing order (1st place = element 0).")]
    [SerializeField] private Transform[] standPoints;
    [Tooltip("Centre of the finish platform, its blue arrow pointing where the camera looks from. The finish camera " +
             "frames this point whichever stand point the player gets. Empty = the 1st place stand point.")]
    [SerializeField] private Transform finishViewPoint;
    [Tooltip("Spawns the bonus multiplier platforms after the finish when the player comes 1st. " +
             "Empty = this level has no bonus: 1st place just finishes at x1.")]
    [SerializeField] private BonusPlatformSpawner bonusPlatforms;

    private TrackRoute route;

    public TrackRoute Route => route ??= new TrackRoute(road);
    public Transform PlayerSpawn => playerSpawn;
    public Transform[] NpcSpawns => npcSpawns;
    public NPC[] NpcPrefabs => npcPrefabs;
    public int NpcCount => npcSpawns == null ? 0 : Mathf.Min(npcCount, npcSpawns.Length);
    public FinishLine FinishLine => finishLine;
    public Transform[] StandPoints => standPoints;
    public Transform FinishViewPoint => finishViewPoint != null ? finishViewPoint
        : standPoints != null && standPoints.Length > 0 ? standPoints[0] : finishLine.transform;
    public BonusPlatformSpawner BonusPlatforms => bonusPlatforms;

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
