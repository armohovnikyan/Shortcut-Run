using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Everything that builds a SpawnBox: fitting it to a level's start road and laying out its spawn points.
public static class SpawnBoxTool
{
    private const float Margin = 0.8f;

    /// <summary>Sizes both zones to the road at the level's start (as wide as it allows, no deeper than its first
    /// section) and lays the points out again.</summary>
    public static void Fit(SpawnBox box, SplineRoad road)
    {
        float roadWidth = 9f, firstLength = 30f;
        if (road != null && road.SectionCount > 0)
        {
            roadWidth = road.GetWidth(0);
            firstLength = road.Container.Splines[0].GetLength();
        }
        float width = Mathf.Clamp(roadWidth - 2f, 3f, 8f);
        box.playerZone.width = box.npcZone.width = width;
        box.npcZone.depth = Mathf.Clamp(firstLength - 4f - box.playerZone.depth - box.zoneGap, 6f, 12f);
        GeneratePoints(box);
        EditorUtility.SetDirty(box);
    }

    /// <summary>Replaces the box's points: the player's in the middle of the player zone, NPCs at random spots in the NPC
    /// zone at least Min Spacing apart (eased when it doesn't fit). Capacity = Max (or Min) NPCs. Every press is a new layout.</summary>
    public static void GeneratePoints(SpawnBox box)
    {
        if (box.playerPoint != null) Object.DestroyImmediate(box.playerPoint.gameObject);
        foreach (Transform old in box.npcPoints) if (old != null) Object.DestroyImmediate(old.gameObject);
        box.npcPoints.Clear();

        box.playerPoint = NewPoint(box, "PlayerPoint", new Vector3(0f, 0f, box.playerZone.depth * 0.5f));

        // Random-looking but never crowded: points are thrown at random into the zone, each kept at least `spacing` from the
        // others; when the zone is too full for that, the spacing is eased a little and it starts over.
        int count = Mathf.Max(box.npcCountMin, box.npcCountMax, 1);
        float halfWidth = Mathf.Max(0f, box.npcZone.width * 0.5f - Margin);
        float zMin = box.NpcZoneStart + Margin, zMax = box.NpcZoneStart + Mathf.Max(Margin, box.npcZone.depth - Margin);
        var random = new System.Random();
        var spots = new List<Vector2>();
        float spacing = box.minSpacing;
        for (int round = 0; round < 40; round++, spacing *= 0.93f)
        {
            spots.Clear();
            for (int tries = 0; tries < count * 80 && spots.Count < count; tries++)
            {
                var candidate = new Vector2(Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
                                            Mathf.Lerp(zMin, zMax, (float)random.NextDouble()));
                if (spots.TrueForAll(s => (s - candidate).sqrMagnitude >= spacing * spacing)) spots.Add(candidate);
            }
            if (spots.Count >= count) break;
        }
        spots.Sort((a, b) => b.y.CompareTo(a.y)); // front of the zone first, so point 1 is the front-most
        for (int i = 0; i < spots.Count; i++)
            box.npcPoints.Add(NewPoint(box, $"NpcPoint ({i + 1})", new Vector3(spots[i].x, 0f, spots[i].y)));
    }

    private static Transform NewPoint(SpawnBox box, string pointName, Vector3 local)
    {
        var go = new GameObject(pointName);
        go.transform.SetParent(box.transform, false);
        go.transform.localPosition = local;
        return go.transform;
    }
}
