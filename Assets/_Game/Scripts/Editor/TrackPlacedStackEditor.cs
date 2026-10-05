using UnityEditor;

[CustomEditor(typeof(TrackPlacedStack)), CanEditMultipleObjects]
public class TrackPlacedStackEditor : TrackPlacedEditor
{
    public override void OnInspectorGUI()
    {
        if (DrawPlatformSection()) return; // on a platform: no road fields

        serializedObject.Update();
        DrawPlacementFields();
        serializedObject.ApplyModifiedProperties();
        DrawStatusAndSnap();
    }
}
