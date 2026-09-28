using UnityEditor;

[CustomEditor(typeof(TrackPlacedStack)), CanEditMultipleObjects]
public class TrackPlacedStackEditor : TrackPlacedEditor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPlacementFields();
        serializedObject.ApplyModifiedProperties();
        DrawStatusAndSnap();
    }
}
