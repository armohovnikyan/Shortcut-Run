using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Inspector for SectionChainPreview: "Fill Sample Library" adds a starter set of sections,
// "Roll" generates with the current seed, "New Seed" rolls a random one, "Stress Test" runs many seeds
// and reports how often a track was found and how much backtracking it took.
[CustomEditor(typeof(SectionChainPreview))]
public class SectionChainPreviewEditor : Editor
{
    private string stressReport;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var preview = (SectionChainPreview)target;

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fill Sample Library"))
            {
                Undo.RecordObject(preview, "Fill Sample Library");
                preview.library = SampleLibrary();
                preview.Roll();
                EditorUtility.SetDirty(preview);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Roll")) { Undo.RecordObject(preview, "Roll"); preview.Roll(); SceneView.RepaintAll(); }
            if (GUILayout.Button("New Seed")) { Undo.RecordObject(preview, "New Seed"); preview.RollNewSeed(); SceneView.RepaintAll(); }
            if (GUILayout.Button("Stress Test (500 seeds)")) stressReport = StressTest(preview);
        }

        var result = preview.result;
        if (result != null)
        {
            string text = result.success
                ? $"Track found: {result.sections.Count} sections, {result.steps} steps, {result.backtracks} backtracks."
                : $"FAILED: {result.message}";
            EditorGUILayout.HelpBox(text, result.success ? MessageType.Info : MessageType.Warning);
        }
        if (stressReport != null) EditorGUILayout.HelpBox(stressReport, MessageType.None);
    }

    private static string StressTest(SectionChainPreview preview)
    {
        int ok = 0, backtracks = 0, worstSteps = 0;
        var settings = JsonUtility.FromJson<SectionChainGenerator.Settings>(JsonUtility.ToJson(preview.settings));
        for (int seed = 1; seed <= 500; seed++)
        {
            settings.seed = seed;
            var r = SectionChainGenerator.Generate(preview.library, settings);
            if (r.success) ok++;
            backtracks += r.backtracks;
            worstSteps = Mathf.Max(worstSteps, r.steps);
        }
        return $"Stress test: {ok}/500 tracks found, {backtracks / 500f:0.0} backtracks on average, worst case {worstSteps} steps.";
    }

    private static SectionDef Make(string name, string pieces, int difficulty, float weight,
        SectionTags tags = SectionTags.None, int minLevel = 1, int maxUses = 0) => new SectionDef
    {
        name = name, pieces = pieces, difficulty = difficulty, weight = weight,
        tags = tags, minLevel = minLevel, maxUses = maxUses,
    };

    // 45° = one letter. Four turns the same way = half circle, so e.g. "L L" is a 90° corner.
    private static List<SectionDef> SampleLibrary() => new List<SectionDef>
    {
        Make("Start",               "S",                0, 1, SectionTags.Start),
        Make("Finish",              "S",                0, 1, SectionTags.Finish),
        Make("Straight",            "S",                0, 4),
        Make("Long",                "S S",              0, 2),
        Make("Gentle Left - Large", "S L L S",          0, 2),
        Make("Gentle Right - Large","S R R S",          0, 2),
        Make("Gentle Right - Small","S r r S",          0, 3), 
        Make("Gentle Right - Small","S l l S",          0, 3),
        Make("Wide Left",           "L L L",            1, 1f, minLevel: 2),
        Make("Wide Right",          "R R R",            1, 1f, minLevel: 2),
        Make("S-Bend",              "S L R S",          1, 3),
        Make("S-Bend Back",         "S R L S",          1, 3),
        Make("Chicane",             "R L L R",          2, 2.5f, minLevel: 3),
        Make("Zigzag",              "S l l r r l l S",  2, 1, minLevel: 4),
        Make("Hairpin Left",        "S L L L L S",      3, 2.7f, minLevel: 5, maxUses: 1),
        Make("Hairpin Right",       "S R R R R S",      3, 1.7f, minLevel: 5, maxUses: 1),
        Make("Round-Half-Left",     "R L L L",          5, 1.7f, minLevel: 7, maxUses: 2),
        Make("Round-Half-Right",    "L R R R",          5, 1.7f, minLevel: 7, maxUses: 2),
        Make("Round-Left",          "R L L L L",        5, 0.7f, minLevel: 7, maxUses: 1),
        Make("Round-Right",         "L R R R R",        5, 0.7f, minLevel: 7, maxUses: 1),
    };
}
