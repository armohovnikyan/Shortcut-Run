using UnityEditor;
using UnityEngine;

// Placement-tool settings shared by the Track Placement window and the track handles.
// Stored in EditorPrefs: per user and machine, remembered between sessions, never in the project.
public static class TrackPlacementSettings
{
    private const string Prefix = "ShortcutRun.TrackPlacement.";

    public static string PaletteFolder
    {
        get => EditorPrefs.GetString(Prefix + "PaletteFolder", "Assets/_Game/Prefabs/Board");
        set => EditorPrefs.SetString(Prefix + "PaletteFolder", value);
    }

    // Metres. 0 = no snapping along the road.
    public static float DistanceStep
    {
        get => EditorPrefs.GetFloat(Prefix + "DistanceStep", 0f);
        set => EditorPrefs.SetFloat(Prefix + "DistanceStep", Mathf.Max(0f, value));
    }

    // Evenly spaced lanes across the road. 0 = free.
    public static int Lanes
    {
        get => EditorPrefs.GetInt(Prefix + "Lanes", 0);
        set => EditorPrefs.SetInt(Prefix + "Lanes", Mathf.Max(0, value));
    }

    public static TrackFacing Facing
    {
        get => (TrackFacing)EditorPrefs.GetInt(Prefix + "Facing", 0);
        set => EditorPrefs.SetInt(Prefix + "Facing", (int)value);
    }

    public static bool ShowSectionLabels
    {
        get => EditorPrefs.GetBool(Prefix + "ShowSectionLabels", true);
        set => EditorPrefs.SetBool(Prefix + "ShowSectionLabels", value);
    }

    // Distance to the nearest step; side to the nearest lane centre.
    // Lane centres for N lanes: -1 + (2i + 1) / N  →  3 lanes = -0.67, 0, 0.67 (each lane's middle).
    public static void Snap(ref TrackPlacement placement)
    {
        float step = DistanceStep;
        if (step > 0f) placement.distance = Mathf.Round(placement.distance / step) * step;

        int lanes = Lanes;
        if (lanes > 0)
        {
            int lane = Mathf.Clamp(Mathf.FloorToInt((placement.side + 1f) * 0.5f * lanes), 0, lanes - 1);
            placement.side = -1f + (2f * lane + 1f) / lanes;
        }
    }
}
