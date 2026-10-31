using System.Collections.Generic;
using UnityEngine;

// Editor tool that lives in a level prefab, next to (or instead of) the LevelDrawer: reads the same road drawing
// (PNG, same colours) but builds the road out of the sections in its Library, the way the section chain does —
// every turn is one of the library's pieces (S s L R l r), so lines are straight and corners are real corners.
// "Preview" shows the road as sections (outline + name, like the section chain preview); a part of the drawing no
// library section fits is drawn in red and listed in the warnings. "Draw Road" writes the sections into the level's
// SplineRoad. The image's up = this object's forward (+Z); the start sits on this object's position.
// The work is done by SectionDrawerEditor / SectionMatcher; nothing here runs in the game.
public class SectionDrawer : LevelDrawer
{
    [Header("Sections")]
    [Tooltip("The sections the road may be built from. Fill Sample Library adds a starter set. " +
             "Start / Finish tags, min level and max uses are ignored here.")]
    public List<SectionDef> library = new List<SectionDef>();
    [Tooltip("Only sections of this piece scale are used (1.5 = the default road width).")]
    public float sectionScale = 1.5f;
    [Tooltip("A section further than this from the drawing is flagged. 0 = 0.6 × road width.")]
    [Min(0f)] public float flagDeviation;

    public override bool UsesRoadDefaultWidth => true;

    /// <summary>One section of the built road, in this object's local space (x, z).</summary>
    public class PlacedSection
    {
        public SectionDef def;
        public Vector2 position;
        public float yaw;
        public readonly List<SectionDef.Sample> samples = new List<SectionDef.Sample>();
        public float maxDeviation; // metres from the clean line the section was matched to
        public bool flagged;
    }

    /// <summary>The road made for one drawn line.</summary>
    public class Chain
    {
        public int number;                      // which drawn line (1-based)
        public readonly List<PlacedSection> sections = new List<PlacedSection>();
        public List<Vector2> target;            // the clean line it was matched to
        public List<Vector2> uncovered;         // the part of it no section reached (empty when complete)
        public bool complete;
        public float endGap;                    // metres between the road's end and the line's end
        public float meanError, maxError;
    }

    [System.NonSerialized] public List<Chain> chains;
    [System.NonSerialized] public List<string> warnings;

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.matrix = Matrix4x4.identity;
        if (chains == null) return;

        Vector3 World(Vector2 p) => transform.TransformPoint(new Vector3(p.x, roadHeight, p.y));
        foreach (Chain chain in chains)
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f);
            if (chain.uncovered != null)
                for (int i = 1; i < chain.uncovered.Count; i++) Gizmos.DrawLine(World(chain.uncovered[i - 1]), World(chain.uncovered[i]));

            for (int i = 0; i < chain.sections.Count; i++)
            {
                PlacedSection placed = chain.sections[i];
                Gizmos.color = placed.flagged ? new Color(1f, 0.25f, 0.25f) : new Color(0.55f, 1f, 0.55f);
                float half = placed.def.Width * 0.5f;

                Vector3 previousLeft = default, previousRight = default;
                for (int k = 0; k < placed.samples.Count; k++)
                {
                    SectionDef.Sample sample = placed.samples[k];
                    Vector2 right = SectionDef.Right(sample.yaw) * half;
                    Vector3 left = World(sample.position - right), rightEdge = World(sample.position + right);
                    if (k > 0)
                    {
                        Gizmos.DrawLine(previousLeft, left);
                        Gizmos.DrawLine(previousRight, rightEdge);
                    }
                    if (k == 0 || k == placed.samples.Count - 1) Gizmos.DrawLine(left, rightEdge);
                    previousLeft = left;
                    previousRight = rightEdge;
                }

#if UNITY_EDITOR
                SectionDef.Sample middle = placed.samples[placed.samples.Count / 2];
                string prefix = chains.Count > 1 ? $"{chain.number}-" : "";
                UnityEditor.Handles.Label(World(middle.position) + Vector3.up * 2f, $"{prefix}{i + 1}. {placed.def.name}");
#endif
            }
        }
    }
}
