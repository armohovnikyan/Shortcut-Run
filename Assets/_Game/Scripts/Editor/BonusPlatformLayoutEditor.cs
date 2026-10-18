using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BonusPlatformLayout))]
public class BonusPlatformLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var layout = (BonusPlatformLayout)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox($"{layout.Platforms.Count} platforms placed. After generating, re-bake the level's NavMeshSurface.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Layout")) layout.Generate();
            if (GUILayout.Button("Randomize")) layout.Randomize();
        }
    }
}
