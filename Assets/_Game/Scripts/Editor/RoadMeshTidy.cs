using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wipes a level's road meshes: every mesh file in its "<level>_RoadMeshes" folder (and the old
// "<level>_Meshes" folder), used or not, plus the mesh references on every SplineRoad in the level.
// Roads rebake into a fresh mesh on "Bake Road" or the next spline edit.
// Works on the level open in Prefab Mode (or the active scene's roads when no prefab is open).
// Flow: unsaved changes -> "save and continue?" -> confirmation listing what goes -> delete.
public static class RoadMeshTidy
{
    private const string Title = "Delete Road Meshes";

    [MenuItem("Tools/Shortcut Run/Delete Road Meshes")]
    public static void Run()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        string ownerPath = stage != null ? stage.assetPath : EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(ownerPath))
        {
            EditorUtility.DisplayDialog(Title, "Open the level prefab in Prefab Mode first.", "OK");
            return;
        }

        GameObject[] roots = stage != null
            ? new[] { stage.prefabContentsRoot }
            : EditorSceneManager.GetActiveScene().GetRootGameObjects();
        var roads = new List<SplineRoad>();
        foreach (GameObject root in roots) roads.AddRange(root.GetComponentsInChildren<SplineRoad>(true));

        if (stage == null)
            foreach (SplineRoad road in roads)
                if (PrefabUtility.IsPartOfPrefabInstance(road))
                {
                    EditorUtility.DisplayDialog(Title, $"'{road.name}' belongs to a level prefab. " +
                                                       "Open that prefab in Prefab Mode and run this there.", "OK");
                    return;
                }

        if (!SaveFirst(stage)) return;

        List<string> files = FindMeshFiles(ownerPath, out List<string> folders);
        if (files.Count == 0 && roads.Count == 0)
        {
            EditorUtility.DisplayDialog(Title, "This level has no road meshes.", "OK");
            return;
        }

        var details = new StringBuilder($"{Title} — '{Path.GetFileNameWithoutExtension(ownerPath)}':\n");
        foreach (string file in files) details.AppendLine($"  Recycle Bin: {file}");
        Debug.Log(details.ToString());

        bool confirmed = EditorUtility.DisplayDialog(Title,
            $"Level: {Path.GetFileNameWithoutExtension(ownerPath)}\n\n" +
            $"Move to the Recycle Bin: {files.Count} mesh file(s), used or not\n" +
            $"Clear the mesh on: {roads.Count} road(s)\n\n" +
            "Press \"Bake Road\" afterwards to build a fresh mesh. The file list is in the Console.",
            "Delete", "Cancel");
        if (!confirmed) return;

        foreach (SplineRoad road in roads) road.ClearBakedMesh();
        Save(stage);

        foreach (string file in files)
            if (!AssetDatabase.MoveAssetToTrash(file))
                Debug.LogWarning($"{Title}: couldn't move '{file}' to the Recycle Bin.");
        foreach (string folder in folders)
            if (AssetDatabase.FindAssets("", new[] { folder }).Length == 0)
                AssetDatabase.MoveAssetToTrash(folder);

        AssetDatabase.Refresh();
        Debug.Log($"{Title}: done.");
    }

    // The prefab/scene is saved again after the meshes are cleared, so unsaved edits must be dealt with now.
    private static bool SaveFirst(PrefabStage stage)
    {
        if (stage != null)
        {
            if (!stage.scene.isDirty) return true;
            if (!EditorUtility.DisplayDialog(Title,
                    $"'{Path.GetFileName(stage.assetPath)}' has unsaved changes.\n\nSave them and continue?",
                    "Save and Continue", "Cancel"))
                return false;
            Save(stage);
            return true;
        }
        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void Save(PrefabStage stage)
    {
        if (stage != null)
        {
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
            stage.ClearDirtiness();
        }
        else
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }

    // Every mesh file in the level's current and old-style mesh folders.
    private static List<string> FindMeshFiles(string ownerPath, out List<string> folders)
    {
        string current = SplineRoad.MeshFolderFor(ownerPath);
        string legacy = current.Substring(0, current.Length - SplineRoad.MeshFolderSuffix.Length)
                        + SplineRoad.LegacyMeshFolderSuffix;

        folders = new List<string>();
        var files = new List<string>();
        foreach (string folder in new[] { current, legacy })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            folders.Add(folder);
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!files.Contains(path)) files.Add(path);
            }
        }
        return files;
    }
}
