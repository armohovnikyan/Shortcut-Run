using UnityEditor;
using UnityEngine;

// Inspector for SpawnBox: "Generate Points" lays out the player and NPC points inside the two zones (one Undo step).
[CustomEditor(typeof(SpawnBox))]
public class SpawnBoxEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var box = (SpawnBox)target;
        EditorGUILayout.Space();

        if (box.playerPoint == null)
            EditorGUILayout.HelpBox("No player point yet — press Generate Points.", MessageType.Warning);
        else if (box.npcPoints.Count < Mathf.Max(box.npcCountMin, box.npcCountMax))
            EditorGUILayout.HelpBox($"{box.npcPoints.Count} NPC point(s) for up to {Mathf.Max(box.npcCountMin, box.npcCountMax)} NPCs — " +
                                    "fewer NPCs will spawn. Press Generate Points to refill.", MessageType.Warning);

        if (GUILayout.Button(new GUIContent("Generate Points",
                "Replaces the player and NPC points with a fresh layout inside the zones. Hand-moved points are lost.")))
        {
            Undo.SetCurrentGroupName("Generate Spawn Points");
            int group = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(box.gameObject, "Generate Spawn Points");
            SpawnBoxTool.GeneratePoints(box);
            EditorUtility.SetDirty(box);
            Undo.CollapseUndoOperations(group);
        }
    }
}
