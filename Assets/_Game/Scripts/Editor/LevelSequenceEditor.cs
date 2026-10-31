using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Inspector for LevelSequenceSO: the two lists, helper buttons that fill them from the imported original levels,
// and a preview of which level a player with a given salt gets at level 1, 2, 3 … (the pool part is shuffled
// by the salt, so "Random salt" shows how different players' pools differ).
[CustomEditor(typeof(LevelSequenceSO))]
public class LevelSequenceEditor : Editor
{
    private const string OriginalFolder = "Assets/_Game/Prefabs/Levels/Original";
    private const string PoolFolder = OriginalFolder + "/Pool";

    private int previewSalt = 12345;
    private int previewCount = 40;
    private Vector2 scroll;

    public override void OnInspectorGUI()
    {
        var sequence = (LevelSequenceSO)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fixedLevels"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pool"), true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fill from imported originals", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fixed = Level_Original_01..10")) SetList("fixedLevels", LoadLevels(OriginalFolder, "Level_Original_", 1, 10));
            if (GUILayout.Button("Pool = everything in Pool/")) SetPool(LoadLevels(PoolFolder, "Pool_", 0, int.MaxValue));
        }
        EditorGUILayout.HelpBox("Pool/ also holds the two narrow scale-1 levels (Pool_Test_9, Pool_Test_11); remove them " +
                                "from the list if they should not appear. A Pool_X_Mirrored prefab is attached to Pool_X " +
                                "as its Mirrored twin.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            previewSalt = EditorGUILayout.IntField("Salt", previewSalt);
            if (GUILayout.Button("Random salt", GUILayout.Width(90))) previewSalt = Random.Range(1, int.MaxValue);
        }
        previewCount = Mathf.Clamp(EditorGUILayout.IntField("Levels to show", previewCount), 1, 300);

        int fixedCount = sequence.FixedLevels?.Length ?? 0;
        var text = new StringBuilder();
        string previous = null;
        int repeats = 0;
        for (int i = 0; i < previewCount; i++)
        {
            Level level = sequence.Pick(i, previewSalt);
            string levelName = level != null ? level.name : "(none)";
            if (levelName == previous) repeats++;
            previous = levelName;
            text.AppendLine($"{i + 1,3}  {(i < fixedCount ? "fixed" : "pool ")}  {levelName}");
        }
        if (repeats > 0) EditorGUILayout.HelpBox($"{repeats} level(s) repeat the one before them.", MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(320));
        EditorGUILayout.SelectableLabel(text.ToString(), EditorStyles.textArea, GUILayout.MinHeight(previewCount * 15f));
        EditorGUILayout.EndScrollView();
    }

    private void SetList(string property, List<Level> levels)
    {
        serializedObject.Update();
        SerializedProperty list = serializedObject.FindProperty(property);
        list.arraySize = levels.Count;
        for (int i = 0; i < levels.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        serializedObject.ApplyModifiedProperties();
    }

    // One entry per level; a "<name>_Mirrored" prefab becomes that entry's mirrored twin instead of an entry of its own.
    private void SetPool(List<Level> levels)
    {
        List<Level> originals = levels.Where(l => !l.name.EndsWith("_Mirrored")).ToList();
        serializedObject.Update();
        SerializedProperty list = serializedObject.FindProperty("pool");
        list.arraySize = originals.Count;
        for (int i = 0; i < originals.Count; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("level").objectReferenceValue = originals[i];
            entry.FindPropertyRelative("mirrored").objectReferenceValue =
                levels.Find(l => l.name == originals[i].name + "_Mirrored");
        }
        serializedObject.ApplyModifiedProperties();
    }

    // Level prefabs named `prefix` + number inside one folder (subfolders are the level's own, one prefab each).
    private static List<Level> LoadLevels(string folder, string prefix, int from, int to)
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(prefix))
            .Select(path => (path, level: AssetDatabase.LoadAssetAtPath<Level>(path)))
            .Where(item => item.level != null && InRange(item.path, prefix, from, to))
            .OrderBy(item => NaturalKey(item.path, prefix))
            .Select(item => item.level)
            .ToList();
    }

    private static bool InRange(string path, string prefix, int from, int to)
    {
        if (to == int.MaxValue) return true;
        string name = System.IO.Path.GetFileNameWithoutExtension(path).Substring(prefix.Length);
        return int.TryParse(name, out int number) && number >= from && number <= to;
    }

    // Pool_Final_2 before Pool_Final_10.
    private static string NaturalKey(string path, string prefix)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(path).Replace("_Mirrored", "");
        return System.Text.RegularExpressions.Regex.Replace(name, @"\d+", m => m.Value.PadLeft(4, '0'));
    }
}
