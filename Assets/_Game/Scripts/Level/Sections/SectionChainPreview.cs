using System.Collections.Generic;
using UnityEngine;

// Scene-view preview of SectionChainGenerator: put it on an empty object, press "Fill Sample Library",
// then "Roll" to see a new track made of sections. Draws the road outline, one colour per difficulty,
// with the section names. Nothing here runs in the game — it only shows what the generator would build.
// The object's position is the start of the track, its forward (+Z) the start direction.
public class SectionChainPreview : MonoBehaviour
{
    public SectionChainGenerator.Settings settings = new SectionChainGenerator.Settings();
    public List<SectionDef> library = new List<SectionDef>();

    [HideInInspector] public SectionChainGenerator.Result result;

    public void Roll()
    {
        result = SectionChainGenerator.Generate(library, settings);
    }

    public void RollNewSeed()
    {
        settings.seed = Random.Range(1, 1000000);
        Roll();
    }

    private static readonly Color[] DifficultyColors =
    {
        new Color(0.35f, 0.85f, 0.4f), new Color(0.95f, 0.85f, 0.25f),
        new Color(0.95f, 0.55f, 0.2f), new Color(0.9f, 0.25f, 0.25f),
    };

    private void OnDrawGizmos()
    {
        // The area the track has to stay inside.
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Vector3 areaCenter = ToWorld(settings.area.center);
        Gizmos.DrawWireCube(areaCenter, new Vector3(settings.area.width, 0f, settings.area.height));

        if (result == null) return;

        for (int i = 0; i < result.sections.Count; i++)
        {
            SectionChainGenerator.Placed placed = result.sections[i];
            Gizmos.color = DifficultyColors[Mathf.Clamp(placed.def.difficulty, 0, 3)];
            float half = placed.def.Width * 0.5f;

            Vector3 previousLeft = default, previousRight = default;
            for (int k = 0; k < placed.samples.Count; k++)
            {
                SectionDef.Sample sample = placed.samples[k];
                Vector2 right = SectionDef.Right(sample.yaw) * half;
                Vector3 left = ToWorld(sample.position - right), rightEdge = ToWorld(sample.position + right);
                if (k > 0)
                {
                    Gizmos.DrawLine(previousLeft, left);
                    Gizmos.DrawLine(previousRight, rightEdge);
                }
                if (k == 0 || k == placed.samples.Count - 1) Gizmos.DrawLine(left, rightEdge); // joint lines
                previousLeft = left;
                previousRight = rightEdge;
            }

#if UNITY_EDITOR
            SectionDef.Sample middle = placed.samples[placed.samples.Count / 2];
            UnityEditor.Handles.Label(ToWorld(middle.position) + Vector3.up * 2f, $"{i + 1}. {placed.def.name}");
#endif
        }
    }

    private Vector3 ToWorld(Vector2 flat) => transform.TransformPoint(new Vector3(flat.x, 0f, flat.y));
}
