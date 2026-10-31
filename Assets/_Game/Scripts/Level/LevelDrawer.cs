using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Editor tool that lives in a level prefab: reads a road drawing (PNG) and writes it into the level's
// SplineRoad as splines. Black = road, green dot = a spline starts, blue dot = a spline ends,
// red dot = finish, brown dot = island, yellow dot = jump pad. The image's up = this object's
// forward (+Z); the start sits on this object's position.
// Everything is flat at one height and one width — ramps, widths and island sizes are edited by hand afterwards.
// The road is traced as free curves. SectionDrawer (a LevelDrawer too) builds it from library sections instead.
// The work is done by LevelDrawerEditor ("Preview" / "Draw Road" / "Place Objects"); nothing here runs in the game.
public class LevelDrawer : MonoBehaviour
{
    public enum ScaleMode { MetersPerPixel, FitLength, Auto }

    [Header("Input")]
    [Tooltip("The road drawing. Black = road, green = spline start, blue = spline end, red = finish, " +
             "brown = island, yellow = jump pad. Any stroke thickness.")]
    public Texture2D roadImage;
    [Tooltip("Road to draw into. Empty = the first SplineRoad in this level.")]
    public SplineRoad road;
    [Tooltip("Pixels darker than this count as road (0 = only pure black, 1 = everything).")]
    [Range(0.05f, 0.95f)] public float darkThreshold = 0.5f;

    [Header("Scale")]
    [Tooltip("Auto: the drawn road width is stretched to the road's default width, whatever size the image is. " +
             "MetersPerPixel: set it by hand (the Inspector then shows the road width it gives and warns when it is off).")]
    public ScaleMode scaleMode = ScaleMode.Auto;
    [Tooltip("World metres per image pixel.")]
    [Min(0.001f)] public float metersPerPixel = 1f;
    [Tooltip("FitLength: the whole drawn road (all splines together) is scaled to this many metres.")]
    [Min(1f)] public float targetLength = 300f;

    [Header("Road")]
    [Tooltip("Smooth mode only: width given to every drawn spline. Sections mode uses the road's default width.")]
    [Min(0.1f)] public float roadWidth = 1f;
    [Tooltip("Height (local Y) of every knot.")]
    public float roadHeight = 1f;

    [Header("Shape")]
    [Tooltip("How round the corners get, in metres. 0 = only pixel steps are smoothed; bigger = wider turns.")]
    [Min(0f)] public float cornerSmoothing = 3f;
    [Tooltip("How far the spline may stray from the smoothed line, in metres. Bigger = fewer knots.")]
    [Min(0.01f)] public float knotTolerance = 0.2f;

    [Header("Junctions")]
    [Tooltip("Forks drawn with gaps: road ends closer than this (in image pixels) to a road's end join it " +
             "(the arms snap to that road's sides). 0 = automatic, 8 stroke widths.")]
    [Min(0f)] public float forkJoinDistance = 0f;

    [Header("Objects")]
    [Tooltip("Placed on every brown dot, top at Road Height (into the \"Islands\" holder in the level's Track).")]
    [FormerlySerializedAs("helperPlatformPrefab")]
    public GameObject islandPrefab;
    [Tooltip("Placed on every yellow dot, facing along the road (into the level's \"JumpPads\" holder).")]
    public GameObject jumpPadPrefab;
    // What "Place Objects" made last time — replaced on the next placing. Moving them by hand is fine.
    [HideInInspector] public List<GameObject> placedObjects = new List<GameObject>();

    // Stroke thickness of the image in pixels, measured by the last Preview (0 = not measured). Used by the Inspector's width check.
    [HideInInspector] public float measuredStrokePx;

    // Filled by the editor's Preview, drawn while selected. Local space of this object.
    [System.NonSerialized] public List<Vector3[]> previewPaths;
    [System.NonSerialized] public List<bool> previewClosed;
    [System.NonSerialized] public Vector3? previewStart, previewFinish;
    [System.NonSerialized] public List<Vector3> previewIslands;
    [System.NonSerialized] public List<Pose> previewJumpPads; // rotation = facing along the road

    public SplineRoad FindRoad()
    {
        if (road != null) return road;
        return transform.root.GetComponentInChildren<SplineRoad>(true);
    }

    /// <summary>True: the road is built to the SplineRoad's default width instead of this drawer's Road Width.</summary>
    public virtual bool UsesRoadDefaultWidth => false;

    protected virtual void OnDrawGizmosSelected()
    {
        if (previewPaths == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        for (int p = 0; p < previewPaths.Count; p++)
        {
            Vector3[] points = previewPaths[p];
            Gizmos.color = Color.Lerp(Color.cyan, Color.magenta, previewPaths.Count > 1 ? p / (previewPaths.Count - 1f) : 0f);
            for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1], points[i]);
            if (previewClosed[p] && points.Length > 2) Gizmos.DrawLine(points[points.Length - 1], points[0]);
            foreach (Vector3 point in points) Gizmos.DrawSphere(point, Mathf.Max(0.15f, roadWidth * 0.15f));
        }
        float marker = Mathf.Max(0.5f, roadWidth * 0.6f);
        Gizmos.color = Color.green;
        if (previewStart.HasValue) Gizmos.DrawSphere(previewStart.Value, marker);
        Gizmos.color = Color.red;
        if (previewFinish.HasValue) Gizmos.DrawSphere(previewFinish.Value, marker);

        Gizmos.color = new Color(0.6f, 0.35f, 0.15f);
        if (previewIslands != null)
            foreach (Vector3 platform in previewIslands)
                Gizmos.DrawWireCube(platform + Vector3.down * marker * 0.25f, new Vector3(marker * 2f, marker * 0.5f, marker * 2f));

        Gizmos.color = Color.yellow;
        if (previewJumpPads != null)
            foreach (Pose pad in previewJumpPads)
            {
                Vector3 forward = pad.rotation * Vector3.forward * marker * 1.5f;
                Vector3 side = pad.rotation * Vector3.right * marker * 0.6f;
                Gizmos.DrawLine(pad.position - forward * 0.5f, pad.position + forward);          // arrow along the road
                Gizmos.DrawLine(pad.position + forward, pad.position + forward * 0.4f + side);
                Gizmos.DrawLine(pad.position + forward, pad.position + forward * 0.4f - side);
            }
    }
}
