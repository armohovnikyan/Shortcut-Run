using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum SectionTags
{
    None = 0,
    Start = 1,  // may only be the first section
    Finish = 2, // may only be the last section
}

/// <summary>
/// One reusable piece of track, described the way the original game builds them: a chain of 15 m straights
/// and 45° turns. Everything the chain generator needs to know is here — where the section ends (exit pose),
/// what ground it covers (centre-line samples + width), how often to pick it and how hard it is.
/// A section always starts at its own origin facing +Z; the generator moves it into place.
/// </summary>
[Serializable]
public class SectionDef
{
    // Measured from the original meshes before the piece's scale (same numbers as the original importer).
    public const float StraightLength = 15f;
    public const float TurnRadius = 17f;
    public const float ShortTurnRadius = 7f;
    public const float TurnAngle = 45f;
    public const float BaseRoadWidth = 6.11f;
    private const float SampleSpacing = 2f;

    /// <summary>One point of the road's centre line: position on the ground (x, z) and heading in degrees.</summary>
    public struct Sample
    {
        public Vector2 position;
        public float yaw;
    }

    public string name = "Section";
    [Tooltip("Pieces in order. S = straight, L / R = 45° turn left / right, l / r = short 45° turn. Space separated.")]
    public string pieces = "S";
    [Tooltip("Piece scale of the original (1 or 1.5). A level only uses sections of one scale, so the road width never jumps.")]
    public float scale = 1.5f;

    [Header("Picking")]
    [Min(0.01f)] public float weight = 1f;
    [Tooltip("0 = easy ... 3 = hard. Early slots pick easy sections, later slots harder ones.")]
    [Range(0, 3)] public int difficulty;
    [Tooltip("First level this section may appear on.")]
    [Min(1)] public int minLevel = 1;
    [Tooltip("How many times one level may use it. 0 = no limit.")]
    [Min(0)] public int maxUses;
    public SectionTags tags;

    public float Width => BaseRoadWidth * scale;

    [NonSerialized] private bool built;
    [NonSerialized] private List<Sample> samples;
    [NonSerialized] private Sample exit;
    [NonSerialized] private float length;
    [NonSerialized] private string builtFrom;
    [NonSerialized] private float builtScale;

    /// <summary>Centre line from the entry (0, 0, facing +Z) to the exit, about every 2 m.</summary>
    public IReadOnlyList<Sample> Samples { get { Build(); return samples; } }
    public Sample Exit { get { Build(); return exit; } }
    public float Length { get { Build(); return length; } }
    /// <summary>How far the section turns the road overall, in degrees (right = positive).</summary>
    public float NetTurn => Exit.yaw;

    public bool IsValid { get { Build(); return samples != null && samples.Count > 0; } }

    private void Build()
    {
        if (built && builtFrom == pieces && Mathf.Approximately(builtScale, scale)) return;
        built = true;
        builtFrom = pieces;
        builtScale = scale;

        samples = new List<Sample>();
        Vector2 position = Vector2.zero;
        float yaw = 0f;
        length = 0f;
        samples.Add(new Sample { position = position, yaw = yaw });

        foreach (string token in (pieces ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token)
            {
                case "S":
                    Straight(StraightLength * scale);
                    break;
                case "L": Arc(-1, TurnRadius * scale); break;
                case "R": Arc(+1, TurnRadius * scale); break;
                case "l": Arc(-1, ShortTurnRadius * scale); break;
                case "r": Arc(+1, ShortTurnRadius * scale); break;
                default:
                    Debug.LogWarning($"Section \"{name}\": unknown piece \"{token}\" (use S, L, R, l, r).");
                    samples.Clear();
                    return;
            }
        }
        exit = new Sample { position = position, yaw = yaw };

        void Straight(float distance)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / SampleSpacing));
            Vector2 forward = Forward(yaw);
            for (int i = 1; i <= steps; i++)
                samples.Add(new Sample { position = position + forward * (distance * i / steps), yaw = yaw });
            position += forward * distance;
            length += distance;
        }

        // direction +1 turns right (clockwise from above), -1 turns left.
        void Arc(int direction, float radius)
        {
            Vector2 center = position + direction * radius * Right(yaw);
            float startYaw = yaw;
            int steps = Mathf.Max(2, Mathf.CeilToInt(radius * TurnAngle * Mathf.Deg2Rad / SampleSpacing));
            for (int i = 1; i <= steps; i++)
            {
                float stepYaw = startYaw + direction * TurnAngle * i / steps;
                samples.Add(new Sample { position = center - direction * radius * Right(stepYaw), yaw = stepYaw });
            }
            yaw = startYaw + direction * TurnAngle;
            position = center - direction * radius * Right(yaw);
            length += radius * TurnAngle * Mathf.Deg2Rad;
        }
    }

    public static Vector2 Forward(float yawDegrees)
    {
        float r = yawDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
    }

    public static Vector2 Right(float yawDegrees)
    {
        float r = yawDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
    }
}
