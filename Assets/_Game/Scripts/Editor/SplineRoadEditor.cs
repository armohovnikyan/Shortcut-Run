using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineRoad)), CanEditMultipleObjects]
public class SplineRoadEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "bakedMesh");
        serializedObject.ApplyModifiedProperties();

        // Shown, not editable: the road manages this asset itself.
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bakedMesh"));

        EditorGUILayout.Space();
        DrawSectionWidths();

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake Road"))
            foreach (Object road in targets)
                ((SplineRoad)road).Bake();

        // Opens dialogs — run after this Inspector finishes drawing, or Unity reports a broken GUI layout.
        if (GUILayout.Button(new GUIContent("Tidy Road Meshes…",
                "Removes road meshes nothing uses any more and moves misplaced ones to their level's folder. " +
                "Asks before changing anything.")))
            EditorApplication.delayCall += RoadMeshTidy.Run;
    }

    // Widths live inside each spline (embedded data), which the SplineContainer Inspector doesn't show —
    // so they're edited here, one row per spline, in the same order as the container's spline list.
    private void DrawSectionWidths()
    {
        EditorGUILayout.LabelField("Section Widths", EditorStyles.boldLabel);

        if (targets.Length > 1)
        {
            EditorGUILayout.HelpBox("Select a single road to edit section widths.", MessageType.None);
            return;
        }

        var road = (SplineRoad)target;
        if (road.SectionCount == 0)
        {
            EditorGUILayout.HelpBox("The SplineContainer has no splines yet.", MessageType.Info);
            return;
        }

        for (int i = 0; i < road.SectionCount; i++)
        {
            EditorGUI.BeginChangeCheck();
            float width = EditorGUILayout.FloatField($"Spline {i}", road.GetWidth(i));
            if (EditorGUI.EndChangeCheck()) road.SetWidth(i, width);
        }
    }
}
