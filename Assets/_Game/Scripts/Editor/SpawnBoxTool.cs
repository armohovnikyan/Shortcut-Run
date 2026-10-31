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

    /// <summary>Replaces the box's points: the player's in the middle of the player zone, NPCs in staggered rows
    /// filling the NPC zone from its front edge back. Capacity = Max (or Min) NPCs.</summary>
    public static void GeneratePoints(SpawnBox box)
    {
        if (box.playerPoint != null) Object.DestroyImmediate(box.playerPoint.gameObject);
        foreach (Transform old in box.npcPoints) if (old != null) Object.DestroyImmediate(old.gameObject);
        box.npcPoints.Clear();

        box.playerPoint = NewPoint(box, "PlayerPoint", new Vector3(0f, 0f, box.playerZone.depth * 0.5f));

        int count = Mathf.Max(box.npcCountMin, box.npcCountMax, 1);
        float usable = Mathf.Max(0f, box.npcZone.width - 2f * Margin);
        int columns = Mathf.Max(1, Mathf.FloorToInt(usable / box.minSpacing) + 1);
        float step = columns > 1 ? usable / (columns - 1) : 0f;
        int rows = Mathf.CeilToInt(count / (float)columns) + 1; // staggered rows hold fewer
        float rowStep = Mathf.Min(box.minSpacing, Mathf.Max(0.5f, (box.npcZone.depth - 2f * Margin) / rows));
        float z = box.NpcZoneStart + box.npcZone.depth - Margin;

        int made = 0;
        for (int row = 0; made < count; row++, z -= rowStep)
        {
            bool shifted = row % 2 == 1 && columns > 1;
            int inRow = shifted ? columns - 1 : columns;
            for (int c = 0; c < inRow && made < count; c++, made++)
            {
                float x = -usable * 0.5f + c * step + (shifted ? step * 0.5f : 0f);
                if (columns == 1) x = 0f;
                box.npcPoints.Add(NewPoint(box, $"NpcPoint ({made + 1})", new Vector3(x, 0f, Mathf.Max(z, box.NpcZoneStart))));
            }
        }
    }

    private static Transform NewPoint(SpawnBox box, string pointName, Vector3 local)
    {
        var go = new GameObject(pointName);
        go.transform.SetParent(box.transform, false);
        go.transform.localPosition = local;
        return go.transform;
    }
}
