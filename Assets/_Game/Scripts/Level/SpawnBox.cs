using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The starting area of a level, in two zones along the road: the NPC zone in front, the player zone behind it,
/// so the player always starts last with every NPC ahead in view. The box starts at its own position (rear edge,
/// centred sideways) and runs forward (+Z of this object); every runner faces that way.
/// The box owns the spawn points: one player point and a row of NPC points (child objects, shown as dots in the
/// Scene view; "Generate Points" in the Inspector lays them out, then move them by hand if you like).
/// Each run takes a random number of NPCs (Min..Max) from the NPC points and shakes every point by its zone's noise.
/// </summary>
public class SpawnBox : MonoBehaviour
{
    [System.Serializable]
    public class Zone
    {
        [Min(1f)] public float width = 6f;
        [Min(1f)] public float depth = 10f;
        [Tooltip("Spawn points of this zone are lifted by this much on the box's up axis.")]
        public float yOffset;
        [Tooltip("Every run each point is moved at random by up to this many metres (flat), so starts are never identical.")]
        [Min(0f)] public float noise;
    }

    [Header("Zones")]
    public Zone playerZone = new Zone { width = 6f, depth = 4f, noise = 0.3f };
    public Zone npcZone = new Zone { width = 6f, depth = 12f, noise = 0.8f };
    [Tooltip("Empty space between the player zone and the NPC zone, in metres.")]
    [Min(0f)] public float zoneGap = 2f;

    [Header("NPCs")]
    [Tooltip("How many NPCs race: a random number from Min to Max each run. Max 0 (or below Min) = always exactly Min. " +
             "Capped by the number of NPC points.")]
    [Min(0)] public int npcCountMin = 4;
    [Min(0)] public int npcCountMax;
    [Tooltip("Generate Points keeps at least this distance between neighbouring NPC points.")]
    [Min(0.5f)] public float minSpacing = 2.2f;

    [Header("Points")]
    public Transform playerPoint;
    public List<Transform> npcPoints = new List<Transform>();

    /// <summary>Where the NPC zone starts along the box's forward axis.</summary>
    public float NpcZoneStart => playerZone.depth + zoneGap;
    public float TotalDepth => NpcZoneStart + npcZone.depth;
    /// <summary>The most NPCs one run can take: Max (or Min), as far as there are points for.</summary>
    public int NpcCapacity => Mathf.Min(npcPoints.Count, Mathf.Max(npcCountMin, npcCountMax));
    public bool IsValid => playerPoint != null;

    /// <summary>Rolls one run's starts: the player's pose, and the NPCs' poses appended to `npcs` (shuffled picks,
    /// each shaken by the NPC zone's noise). False = no player point to start from.</summary>
    public bool Roll(out Pose player, List<Pose> npcs)
    {
        player = default;
        if (playerPoint == null) return false;
        player = Shaken(playerPoint, playerZone);

        int wanted = npcCountMax > npcCountMin ? Random.Range(npcCountMin, npcCountMax + 1) : npcCountMin;
        var pool = new List<Transform>(npcPoints);
        pool.RemoveAll(p => p == null);
        for (int i = 0; i < wanted && pool.Count > 0; i++)
        {
            int pick = Random.Range(0, pool.Count);
            npcs.Add(Shaken(pool[pick], npcZone));
            pool.RemoveAt(pick);
        }
        return true;
    }

    private Pose Shaken(Transform point, Zone zone)
    {
        Vector2 shake = Random.insideUnitCircle * zone.noise;
        Vector3 position = point.position + transform.right * shake.x + transform.forward * shake.y + transform.up * zone.yOffset;
        return new Pose(position, point.rotation);
    }

    // Selected: both zones (green = player, yellow = NPCs) with their points as dots and the noise as a ring.
    private void OnDrawGizmosSelected()
    {
        DrawZone(0f, playerZone, Color.green);
        DrawZone(NpcZoneStart, npcZone, Color.yellow);
        DrawPoint(playerPoint, playerZone, Color.green);
        foreach (Transform point in npcPoints) DrawPoint(point, npcZone, Color.yellow);
    }

    private void DrawZone(float start, Zone zone, Color color)
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(color.r, color.g, color.b, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, zone.yOffset + 0.05f, start + zone.depth * 0.5f), new Vector3(zone.width, 0.1f, zone.depth));
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawPoint(Transform point, Zone zone, Color color)
    {
        if (point == null) return;
        Vector3 center = point.position + transform.up * (zone.yOffset + 0.5f);
        Gizmos.color = color;
        Gizmos.DrawSphere(center, 0.35f);
        if (zone.noise > 0f)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.35f);
            Gizmos.DrawWireSphere(center, zone.noise);
        }
    }
}
