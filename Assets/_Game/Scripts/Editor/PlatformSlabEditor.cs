using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlatformSlab)), CanEditMultipleObjects]
public class PlatformSlabEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        bool anyOutdated = false, anyAsset = false;
        foreach (Object slab in targets)
        {
            if (EditorUtility.IsPersistent(slab)) anyAsset = true;
            else if (!((PlatformSlab)slab).IsBakeCurrent) anyOutdated = true;
        }

        if (anyAsset)
        {
            // Selected in the Project window: baking there would skip Undo and the prefab's own scene.
            EditorGUILayout.HelpBox("Open the prefab (double-click it) to bake.", MessageType.Info);
            return;
        }

        if (anyOutdated)
            EditorGUILayout.HelpBox("The mesh doesn't match these settings yet — press Bake Mesh.", MessageType.Warning);

        if (GUILayout.Button("Bake Mesh"))
            foreach (Object slab in targets)
                ((PlatformSlab)slab).Bake();
    }
}
