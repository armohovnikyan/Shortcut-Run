using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

[CustomEditor(typeof(TrackPlacedStamp)), CanEditMultipleObjects]
public class TrackPlacedStampEditor : TrackPlacedEditor
{
    private SerializedProperty stamp, boardPrefab;

    protected override void OnEnable()
    {
        base.OnEnable();
        stamp = serializedObject.FindProperty("stamp");
        boardPrefab = serializedObject.FindProperty("boardPrefab");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(stamp);
        EditorGUILayout.PropertyField(boardPrefab);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anchor", EditorStyles.boldLabel);

        if (OnPlatformCount() > 0)
        {
            serializedObject.ApplyModifiedProperties();
            DrawPlatformSection();
            if (targets.Length == 1) DrawPatternInfo((TrackPlacedStamp)target);
            return;
        }

        DrawPlacementFields();
        serializedObject.ApplyModifiedProperties();

        if (targets.Length == 1) DrawPatternInfo((TrackPlacedStamp)target);
        DrawStatusAndSnap();
    }

    private static void DrawPatternInfo(TrackPlacedStamp placed)
    {
        BoardStampSO asset = placed.Stamp;
        if (asset == null)
        {
            EditorGUILayout.HelpBox("Assign a Board Stamp asset.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox($"{asset.Entries.Count} stacks, {asset.BoardCount} boards.", MessageType.None);
        if (placed.Platform != null)
        {
            if (IsPartlyOffPlatform(placed))
                EditorGUILayout.HelpBox("Part of the pattern hangs over the platform's edge. Move or turn the stamp.",
                    MessageType.Warning);
        }
        else if (IsPartlyOffRoad(placed, asset))
            EditorGUILayout.HelpBox("Part of the pattern is off the road or past the section's end " +
                                    "(stacks there get squeezed to the end). Move or turn the stamp.",
                MessageType.Warning);
    }

    // A stack is "on" the platform when a ray straight down through it hits the platform's collider.
    private static bool IsPartlyOffPlatform(TrackPlacedStamp placed)
    {
        Collider surface = placed.Platform.Surface;
        if (surface == null) return false;

        foreach (Transform child in placed.transform)
        {
            if (!child.TryGetComponent(out BoardStack _)) continue;
            var down = new Ray(child.position + Vector3.up * 5f, Vector3.down);
            if (!surface.Raycast(down, out _, 10f)) return true;
        }
        return false;
    }

    // Side beyond ±1 = off the road edge; distance outside the section = clamped onto its end.
    private static bool IsPartlyOffRoad(TrackPlacedStamp placed, BoardStampSO asset)
    {
        SplineRoad road = placed.Placement.road;
        if (road == null) return false;
        int index = road.FindSectionIndex(placed.Placement.sectionId);
        if (index < 0) return false;

        Spline spline = road.Container.Splines[index];
        float length = spline.GetLength();

        foreach (BoardStampSO.Entry entry in asset.Entries)
        {
            if (!placed.TryGetEntryPlacement(entry, out TrackPlacement p)) continue;
            if (Mathf.Abs(p.side) > 1f) return true;
            if (!spline.Closed && (p.distance < 0f || p.distance > length)) return true;
        }
        return false;
    }
}
