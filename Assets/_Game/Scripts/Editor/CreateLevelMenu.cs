using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

// Project window: right-click > Create > Shortcut Run > Level.
// Asks for a name in place (like a new folder), then makes "<name>/<name>.prefab", a copy of
// Level_Template with its road mesh cleared — the road bakes its own mesh into
// "<name>/<name>_RoadMeshes" on the first "Bake Road".
public static class CreateLevelMenu
{
    private const string TemplateName = "Level_Template";

    [MenuItem("Assets/Create/Shortcut Run/Level", false, 0)]
    private static void CreateLevel()
    {
        if (FindTemplate() == null)
        {
            EditorUtility.DisplayDialog("Create Level", $"Couldn't find the '{TemplateName}' prefab.", "OK");
            return;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{SelectedFolder()}/Level_New");
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<CreateLevelAction>(),
            path, EditorGUIUtility.IconContent("Folder Icon").image as Texture2D, null);
    }

    private class CreateLevelAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string parent = Path.GetDirectoryName(pathName).Replace('\\', '/');
            string name = Path.GetFileName(pathName);
            if (AssetDatabase.IsValidFolder(pathName))
            {
                EditorUtility.DisplayDialog("Create Level", $"'{pathName}' already exists.", "OK");
                return;
            }

            AssetDatabase.CreateFolder(parent, name);
            string prefabPath = $"{pathName}/{name}.prefab";
            if (!AssetDatabase.CopyAsset(FindTemplate(), prefabPath))
            {
                Debug.LogError($"Create Level: couldn't copy {TemplateName} to '{prefabPath}'.");
                return;
            }

            // The template's road may still point at a mesh — the new level must bake its own.
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (SplineRoad road in root.GetComponentsInChildren<SplineRoad>(true)) road.ClearBakedMesh();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ProjectWindowUtil.ShowCreatedAsset(prefab);
            Debug.Log($"Create Level: made '{prefabPath}' from {TemplateName}.", prefab);
        }
    }

    private static string FindTemplate()
    {
        foreach (string guid in AssetDatabase.FindAssets($"{TemplateName} t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == TemplateName) return path;
        }
        return null;
    }

    // The folder that's selected in the Project window, or the folder of the selected asset.
    private static string SelectedFolder()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path)) return "Assets";
        return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
    }
}
