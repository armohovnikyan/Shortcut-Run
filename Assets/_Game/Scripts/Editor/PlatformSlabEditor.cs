using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// "Bake Mesh" = the moment the preview becomes a file. In Prefab Mode the prefab is saved right after,
// so the mesh each platform used before can be deleted when no saved prefab/scene (and nothing loaded)
// uses it any more — tweaking a platform leaves one mesh behind, not one per try.
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
            EditorGUILayout.HelpBox("The yellow see-through shape in the Scene view is the preview. " +
                                    "Press Bake Mesh to make it the platform's mesh.", MessageType.Warning);

        if (GUILayout.Button("Bake Mesh")) BakeAll();
    }

    private void BakeAll()
    {
        var replaced = new HashSet<string>();
        foreach (Object target in targets)
        {
            var slab = (PlatformSlab)target;
            Mesh old = slab.GetComponent<MeshFilter>().sharedMesh;
            slab.Bake();
            Mesh baked = slab.GetComponent<MeshFilter>().sharedMesh;
            if (old != null && old != baked) replaced.Add(AssetDatabase.GetAssetPath(old));
        }

        // The saved prefab still points at the old mesh until it's saved, which would keep it "in use".
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty)
        {
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
            stage.ClearDirtiness();
        }
        PlatformMeshCleanup.DeleteUnused(replaced);
    }
}

// Deletes platform meshes (only files in PlatformSlab.MeshFolder) that no saved prefab or scene uses
// and no object in an open scene / Prefab Mode uses.
public static class PlatformMeshCleanup
{
    [MenuItem("Tools/Shortcut Run/Delete Unused Platform Meshes")]
    private static void DeleteAllUnused()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty)
        {
            EditorUtility.DisplayDialog("Delete Unused Platform Meshes",
                $"Save '{Path.GetFileName(stage.assetPath)}' first (Ctrl+S), then run this again.", "OK");
            return;
        }

        var all = new List<string>();
        if (AssetDatabase.IsValidFolder(PlatformSlab.MeshFolder))
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { PlatformSlab.MeshFolder }))
                all.Add(AssetDatabase.GUIDToAssetPath(guid));

        HashSet<string> used = FindUsed(all);
        var unused = all.FindAll(path => !used.Contains(path));
        if (unused.Count == 0)
        {
            EditorUtility.DisplayDialog("Delete Unused Platform Meshes", "Every platform mesh is in use.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Delete Unused Platform Meshes",
                $"Move {unused.Count} of {all.Count} platform mesh(es) to the Recycle Bin? Nothing uses them.\n\n" +
                "The list is in the Console.", "Delete", "Cancel"))
            return;

        Debug.Log("Delete Unused Platform Meshes:\n  " + string.Join("\n  ", unused));
        foreach (string path in unused) AssetDatabase.MoveAssetToTrash(path);
    }

    public static void DeleteUnused(ICollection<string> candidates)
    {
        var inFolder = new List<string>();
        foreach (string path in candidates)
            if (!string.IsNullOrEmpty(path) && path.StartsWith(PlatformSlab.MeshFolder + "/")) inFolder.Add(path);
        if (inFolder.Count == 0) return;

        HashSet<string> used = FindUsed(inFolder);
        foreach (string path in inFolder)
        {
            if (used.Contains(path)) continue;
            AssetDatabase.MoveAssetToTrash(path);
            Debug.Log($"Platform mesh no longer used, moved to the Recycle Bin: {path}");
        }
    }

    // The candidates something still uses: saved prefabs/scenes on disk, plus every loaded object
    // (open scenes and Prefab Mode, which may hold unsaved changes).
    private static HashSet<string> FindUsed(List<string> candidates)
    {
        var wanted = new HashSet<string>(candidates);
        var used = new HashSet<string>();

        var owners = new List<string>();
        owners.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }));
        owners.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }));
        foreach (string guid in owners)
            foreach (string dependency in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid), false))
                if (wanted.Contains(dependency)) used.Add(dependency);

        var loaded = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isLoaded) loaded.AddRange(SceneManager.GetSceneAt(i).GetRootGameObjects());
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) loaded.Add(stage.prefabContentsRoot);

        foreach (GameObject root in loaded)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) Mark(filter.sharedMesh);
            foreach (MeshCollider meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
                if (meshCollider.sharedMesh != null) Mark(meshCollider.sharedMesh);
        }
        return used;

        void Mark(Mesh mesh)
        {
            string path = AssetDatabase.GetAssetPath(mesh);
            if (wanted.Contains(path)) used.Add(path);
        }
    }
}
