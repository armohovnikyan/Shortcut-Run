using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

// Makes a playable level prefab out of an original section: Level_Template (runners, finish, NavMesh surface)
// around the road, islands and boards that OriginalLevelImporter builds. Saved as its own prefab with its
// road mesh and NavMesh next to it, so LevelLoader can load it like any other level.
// Runs in the open scene as a workspace (must be saved) and leaves nothing behind there.
public static class OriginalLevelPrefabBuilder
{
    private const string TemplatePath = "Assets/_Game/Prefabs/Levels/Level_Template/Level_Template.prefab";
    public const string OutputFolder = "Assets/_Game/Prefabs/Levels/Original";

    // As in Level_17: the finish platform sits this far past the road's end, facing back along it.
    private const float FinishBeyondRoadEnd = 2.83f;

    public static Level Build(string sectionPrefabPath, string levelName, string outputFolder = OutputFolder, bool mirrored = false)
    {
        GameObject imported = OriginalLevelImporter.Import(sectionPrefabPath, mirrored);
        if (imported == null) return null;

        GameObject levelGo = CreateFromTemplate(levelName);
        SplineRoad road = MoveTrackInto(levelGo.transform, imported);
        Object.DestroyImmediate(imported);
        PlaceSpawnsAndFinish(levelGo.transform, road, mirrored);

        var level = levelGo.GetComponent<Level>();
        var so = new SerializedObject(level);
        so.FindProperty("road").objectReferenceValue = road;
        so.ApplyModifiedPropertiesWithoutUndo();

        string folder = $"{outputFolder}/{levelName}";
        EnsureFolder(folder);
        string prefabPath = $"{folder}/{levelName}.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(levelGo, prefabPath, InteractionMode.AutomatedAction);

        RebakeRoadNextToPrefab(road);
        BakeNavMesh(levelGo.GetComponent<NavMeshSurface>(), $"{folder}/NavMesh-{levelName}.asset");
        PrefabUtility.ApplyPrefabInstance(levelGo, InteractionMode.AutomatedAction);

        Object.DestroyImmediate(levelGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return AssetDatabase.LoadAssetAtPath<Level>(prefabPath);
    }

    private static GameObject CreateFromTemplate(string levelName)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        var levelGo = (GameObject)PrefabUtility.InstantiatePrefab(template, EditorSceneManager.GetActiveScene());
        // Only the outer prefab: the finish platform inside stays a prefab instance.
        PrefabUtility.UnpackPrefabInstance(levelGo, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        levelGo.name = levelName;
        levelGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return levelGo;
    }

    // The template's own road and jump pads were laid out for its track, so they go; ours take their place.
    private static SplineRoad MoveTrackInto(Transform level, GameObject imported)
    {
        Transform track = level.Find("Track");
        foreach (Transform old in track.Cast<Transform>().ToList()) Object.DestroyImmediate(old.gameObject);
        Transform jumpPads = level.Find("JumpPads");
        if (jumpPads != null) Object.DestroyImmediate(jumpPads.gameObject);

        SplineRoad road = imported.GetComponentInChildren<SplineRoad>();
        road.transform.SetParent(track, true);
        imported.transform.Find("Islands")?.SetParent(track, true);

        Transform stacks = level.Find("Boards(Stacks)");
        stacks.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        level.Find("Boards(Stamps)")?.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Transform boards = imported.transform.Find("Boards");
        if (boards != null)
            foreach (Transform stack in boards.Cast<Transform>().ToList()) stack.SetParent(stacks, true);
        return road;
    }

    // The spawn box at the road's start facing along it, fitted to the road; finish just past the road's last end.
    private static void PlaceSpawnsAndFinish(Transform level, SplineRoad road, bool mirrored)
    {
        Spline first = road.Container.Splines[0];
        first.Evaluate(0f, out float3 startLocal, out float3 startTangent, out _);
        Vector3 start = road.transform.TransformPoint((Vector3)startLocal);
        Quaternion startRot = Quaternion.LookRotation(road.transform.TransformDirection((Vector3)startTangent).normalized, Vector3.up);

        var box = level.GetComponentInChildren<SpawnBox>(true);
        box.transform.SetPositionAndRotation(start, startRot);
        SpawnBoxTool.Fit(box, road);

        // The race ends on the last open section; it may be drawn either way, so take the end pointing away.
        Spline last = road.Container.Splines.Last(s => !s.Closed);
        last.Evaluate(1f, out float3 endLocal, out float3 endTangent, out _);
        last.Evaluate(0f, out float3 otherLocal, out float3 otherTangent, out _);
        Vector3 previous = road.Container.Splines.Count > 1
            ? (Vector3)road.Container.Splines[road.Container.Splines.Count - 2].EvaluatePosition(0.5f)
            : start;
        bool atOne = math.distancesq(endLocal, (float3)previous) >= math.distancesq(otherLocal, (float3)previous);
        Vector3 end = road.transform.TransformPoint(atOne ? (Vector3)endLocal : (Vector3)otherLocal);
        Vector3 dir = road.transform.TransformDirection(atOne ? (Vector3)endTangent : -(Vector3)otherTangent).normalized;

        level.Find("FinishPlatform Variant").SetPositionAndRotation(end + dir * FinishBeyondRoadEnd, Quaternion.LookRotation(-dir, Vector3.up));
    }

    // The import baked the road mesh next to the workspace scene; this level's mesh belongs next to its prefab.
    private static void RebakeRoadNextToPrefab(SplineRoad road)
    {
        var so = new SerializedObject(road);
        SerializedProperty meshProperty = so.FindProperty("bakedMesh");
        string oldMesh = AssetDatabase.GetAssetPath(meshProperty.objectReferenceValue);
        meshProperty.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        road.Bake();
        if (!string.IsNullOrEmpty(oldMesh)) AssetDatabase.DeleteAsset(oldMesh);
    }

    private static void BakeNavMesh(NavMeshSurface surface, string assetPath)
    {
        if (surface == null) return;
        surface.navMeshData = null;
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        EnsureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
    }
}
