using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Housekeeping for the mesh files SplineRoad bakes into "<owner>_Meshes" folders:
//  - a mesh no prefab or scene uses any more (road deleted)            -> Recycle Bin
//  - a mesh used by exactly one prefab/scene but in another's folder     -> moved to "<that owner>_Meshes"
//    (e.g. after renaming the level prefab). Moving keeps the asset's ID, so references survive.
//  - a mesh used by several prefabs/scenes                               -> left alone (no single right folder)
//  - "_Meshes" folders left empty                                        -> Recycle Bin
// Only mesh assets inside "_Meshes" folders are ever touched. Reads the SAVED files, so it insists on
// saving first — an unsaved fresh bake would otherwise look unused.
public static class RoadMeshTidy
{
    private struct Move
    {
        public string from, toFolder;
    }

    [MenuItem("Tools/Shortcut Run/Tidy Road Meshes")]
    public static void Run()
    {
        if (!EnsureEverythingSaved()) return;

        List<string> meshes = FindRoadMeshes();
        Dictionary<string, List<string>> owners = FindOwners(meshes);

        var unused = new List<string>();
        var moves = new List<Move>();
        var shared = new List<string>();

        foreach (string mesh in meshes)
        {
            List<string> users = owners[mesh];
            if (users.Count == 0) unused.Add(mesh);
            else if (users.Count > 1) shared.Add(mesh);
            else
            {
                string target = SplineRoad.MeshFolderFor(users[0]);
                if (FolderOf(mesh) != target) moves.Add(new Move { from = mesh, toFolder = target });
            }
        }

        List<string> emptyFolders = FindEmptyMeshFolders(ignoring: unused, movingOut: moves);

        if (unused.Count == 0 && moves.Count == 0 && emptyFolders.Count == 0)
        {
            EditorUtility.DisplayDialog("Tidy Road Meshes", "Nothing to tidy — every road mesh is used and in " +
                                                            "its level's folder.", "OK");
            return;
        }

        LogPlan(unused, moves, shared, emptyFolders);

        bool confirmed = EditorUtility.DisplayDialog("Tidy Road Meshes",
            $"Move to the Recycle Bin: {unused.Count} unused mesh(es)\n" +
            $"Move to their level's folder: {moves.Count} mesh(es)\n" +
            $"Remove empty folders: {emptyFolders.Count}\n" +
            (shared.Count > 0 ? $"Used by several levels, left alone: {shared.Count}\n" : "") +
            "\nThe full list is in the Console.",
            "Tidy", "Cancel");
        if (!confirmed) return;

        Execute(unused, moves);

        // Recomputed after the moves: folders that just became empty are included.
        foreach (string folder in FindEmptyMeshFolders(ignoring: new List<string>(), movingOut: new List<Move>()))
            AssetDatabase.MoveAssetToTrash(folder);

        AssetDatabase.Refresh();
        Debug.Log("Tidy Road Meshes: done.");
    }

    // The tool reads what's saved on disk. Anything unsaved could reference a mesh the saved files don't.
    private static bool EnsureEverythingSaved()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty)
        {
            EditorUtility.DisplayDialog("Tidy Road Meshes",
                $"'{Path.GetFileName(stage.assetPath)}' is open with unsaved changes.\n\n" +
                "Save it (Ctrl+S), then run Tidy Road Meshes again.", "OK");
            return false;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (!SceneManager.GetSceneAt(i).isDirty) continue;
            EditorUtility.DisplayDialog("Tidy Road Meshes",
                "Open scenes still have unsaved changes. Save them, then run Tidy Road Meshes again.", "OK");
            return false;
        }
        return true;
    }

    // Mesh assets directly inside a folder whose name ends in "_Meshes".
    private static List<string> FindRoadMeshes()
    {
        var meshes = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".asset") && FolderOf(path).EndsWith(SplineRoad.MeshFolderSuffix) && !meshes.Contains(path))
                meshes.Add(path);
        }
        return meshes;
    }

    // mesh path -> prefabs/scenes that reference it directly (a SplineRoad's MeshFilter/MeshCollider/bakedMesh).
    private static Dictionary<string, List<string>> FindOwners(List<string> meshes)
    {
        var owners = new Dictionary<string, List<string>>();
        foreach (string mesh in meshes) owners[mesh] = new List<string>();

        var ownerGuids = new List<string>();
        ownerGuids.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }));
        ownerGuids.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }));

        foreach (string guid in ownerGuids)
        {
            string ownerPath = AssetDatabase.GUIDToAssetPath(guid);
            foreach (string dependency in AssetDatabase.GetDependencies(ownerPath, false))
                if (owners.TryGetValue(dependency, out List<string> users) && !users.Contains(ownerPath))
                    users.Add(ownerPath);
        }
        return owners;
    }

    // "_Meshes" folders that hold nothing, or will hold nothing once the planned removals/moves are done.
    private static List<string> FindEmptyMeshFolders(List<string> ignoring, List<Move> movingOut)
    {
        var leaving = new HashSet<string>(ignoring);
        foreach (Move move in movingOut) leaving.Add(move.from);
        var incoming = new HashSet<string>();
        foreach (Move move in movingOut) incoming.Add(move.toFolder);

        var empty = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || !path.EndsWith(SplineRoad.MeshFolderSuffix)) continue;
            if (!AssetDatabase.IsValidFolder(path) || incoming.Contains(path)) continue;

            bool staysOccupied = false;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { path }))
                if (!leaving.Contains(AssetDatabase.GUIDToAssetPath(guid))) { staysOccupied = true; break; }
            if (!staysOccupied) empty.Add(path);
        }
        return empty;
    }

    private static void Execute(List<string> unused, List<Move> moves)
    {
        foreach (string mesh in unused)
            if (!AssetDatabase.MoveAssetToTrash(mesh))
                Debug.LogWarning($"Tidy Road Meshes: couldn't move '{mesh}' to the Recycle Bin.");

        foreach (Move move in moves)
        {
            if (!AssetDatabase.IsValidFolder(move.toFolder))
                AssetDatabase.CreateFolder(FolderOf(move.toFolder), Path.GetFileName(move.toFolder));

            string destination = AssetDatabase.GenerateUniqueAssetPath($"{move.toFolder}/{Path.GetFileName(move.from)}");
            string error = AssetDatabase.MoveAsset(move.from, destination);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"Tidy Road Meshes: couldn't move '{move.from}': {error}");
                continue;
            }

            // A clash may have given the file a new name; the mesh inside must match it or Unity warns.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(destination);
            string newName = Path.GetFileNameWithoutExtension(destination);
            if (mesh != null && mesh.name != newName)
            {
                mesh.name = newName;
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssetIfDirty(mesh);
            }
        }
    }

    private static void LogPlan(List<string> unused, List<Move> moves, List<string> shared, List<string> emptyFolders)
    {
        var log = new StringBuilder("Tidy Road Meshes — plan:\n");
        foreach (string mesh in unused) log.AppendLine($"  Recycle Bin (unused): {mesh}");
        foreach (Move move in moves) log.AppendLine($"  Move: {move.from}  ->  {move.toFolder}/");
        foreach (string folder in emptyFolders) log.AppendLine($"  Remove empty folder: {folder}");
        foreach (string mesh in shared) log.AppendLine($"  Left alone (used by several levels): {mesh}");
        Debug.Log(log.ToString());
    }

    private static string FolderOf(string assetPath) => Path.GetDirectoryName(assetPath).Replace('\\', '/');
}
