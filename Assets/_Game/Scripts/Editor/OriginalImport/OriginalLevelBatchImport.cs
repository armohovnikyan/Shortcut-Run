using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One click: builds a level prefab for every connected-road original in the pool list, into
// Prefabs/Levels/Original/Pool/. Each one goes through the same OriginalLevelPrefabBuilder as a single import,
// then gets a quick sanity check (route valid, finish placed, road mesh baked). Failures don't stop the batch;
// the report at the end lists what worked and what didn't. The open scene must be saved (it is the workspace).
public static class OriginalLevelBatchImport
{
    public const string PoolFolder = OriginalLevelPrefabBuilder.OutputFolder + "/Pool";
    private const string SourceFolderPref = "ShortcutRun.OriginalBatch.SourceFolder";
    private const string DefaultSourceFolder = @"C:\AssetRipper\temp\AssetRipper_export_20261005_154538\ExportedProject\Assets\GameObject";

    // Source prefab name (without .prefab) -> level name. Connected-road levels only (see Docs/SectionLibrary.md).
    private static readonly string[] PoolSources =
    {
        "Final_1", "Final_1_Short", "Final_3", "Final_3_Short", "Final_4", "Final_4_Short",
        "Final_6", "Final_6_Short", "Final_8", "Final_9", "Final_10", "Final_11", "Final_12", "Final_13", "Final_14",
        "Test_8_Alex", "Test_9_Alex", "Test_10_Alex", "Test_10_Alex_Short", "Test_11_Alex", "Test_12_Alex",
        "Test_14_Alex_Short", "Tuto_test",
    };

    [MenuItem("Window/Shortcut Run/Import Original Pool Levels")]
    private static void Run()
    {
        string folder = EditorPrefs.GetString(SourceFolderPref, DefaultSourceFolder);
        if (!Directory.Exists(folder))
        {
            folder = EditorUtility.OpenFolderPanel("Original section prefabs (…/ExportedProject/Assets/GameObject)", "", "");
            if (string.IsNullOrEmpty(folder)) return;
        }
        EditorPrefs.SetString(SourceFolderPref, folder);
        Debug.Log(Import(folder, PoolSources));
    }

    [MenuItem("Window/Shortcut Run/Import Original Pool Levels (Mirrored)")]
    private static void RunMirrored()
    {
        string folder = EditorPrefs.GetString(SourceFolderPref, DefaultSourceFolder);
        if (!Directory.Exists(folder)) { Debug.LogError("Run the normal pool import once first to set the source folder."); return; }
        Debug.Log(Import(folder, PoolSources, mirrored: true));
    }

    /// <summary>Imports the named source prefabs from `sourceFolder`. Returns a text report.
    /// mirrored = left-right twins, named Pool_X_Mirrored.</summary>
    public static string Import(string sourceFolder, IReadOnlyList<string> sources, bool mirrored = false)
    {
        if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            return "Import failed: save the open scene first — it is used as the workspace.";

        var report = new StringBuilder();
        int ok = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            string source = sources[i];
            string levelName = "Pool_" + source.Replace("_Alex", "") + (mirrored ? "_Mirrored" : "");
            string path = Path.Combine(sourceFolder, source + ".prefab");
            EditorUtility.DisplayProgressBar("Importing original levels", levelName, i / (float)sources.Count);
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("source prefab not found");
                Level level = OriginalLevelPrefabBuilder.Build(path, levelName, PoolFolder, mirrored);
                if (level == null) throw new Exception("builder returned nothing (see console)");
                report.AppendLine($"OK   {levelName}: {Check(level)}");
                ok++;
            }
            catch (Exception e)
            {
                report.AppendLine($"FAIL {levelName}: {e.Message}");
                Debug.LogException(e);
            }
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        return $"Original pool import: {ok}/{sources.Count} built into {PoolFolder}\n{report}";
    }

    // Quick look at the saved prefab: road, route, finish. Returns a one-line summary (problems are marked "!!").
    private static string Check(Level level)
    {
        var so = new SerializedObject(level);
        var road = so.FindProperty("road").objectReferenceValue as SplineRoad;
        var finish = so.FindProperty("finishLine").objectReferenceValue;
        if (road == null) return "!! no road";

        var route = new TrackRoute(road);
        var notes = new List<string>
        {
            $"{road.SectionCount} splines",
            route.IsValid ? $"route {route.TotalLength:0} m" : "!! route invalid",
            finish != null ? "finish ok" : "!! no finish line",
            road.GetComponent<MeshFilter>().sharedMesh != null ? "mesh ok" : "!! no road mesh",
            $"{level.GetComponentsInChildren<BoardStack>(true).Length} stacks",
            $"{level.GetComponentsInChildren<TrackPlatform>(true).Length} platforms",
        };
        return string.Join(", ", notes);
    }
}
