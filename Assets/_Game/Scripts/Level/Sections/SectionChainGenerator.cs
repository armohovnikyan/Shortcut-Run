using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds one level's track by chaining SectionDefs end to start. Picking by weight alone would give tracks that
/// loop back over themselves or drift off into a corner, so every pick goes through rules, and a dead end
/// backs up one section and tries another:
///  1. Filter: first = a Start section, last = a Finish section, nothing in the middle is either; same piece
///     scale as the level (road width never jumps); minLevel; maxUses; not one of the last few picks again;
///     difficulty not above the level's cap.
///  2. Weight: the section's own weight, pulled towards the slot's target difficulty (easy early, harder later)
///     and boosted when it turns the road back towards the start direction after a big drift.
///  3. Fit: the whole section must stay inside the allowed area and must not touch the road already laid
///     (centre lines closer than the two half widths plus a margin), except right at the joint with its neighbour.
///  4. Dead end (nothing fits): undo the previous section, forbid that pick at that slot and continue.
/// Same seed + same library = same track, so a level can be stored as just a seed.
/// </summary>
public static class SectionChainGenerator
{
    [System.Serializable]
    public class Settings
    {
        [Min(2)] public int sectionCount = 8;
        [Min(1)] public int level = 1;
        public int seed = 1;
        [Tooltip("Only sections of this piece scale are used.")]
        public float scale = 1.5f;
        [Range(0, 3)] public int maxDifficulty = 3;
        [Tooltip("A pick may not repeat any of the last N sections.")]
        [Min(0)] public int recentMemory = 2;
        [Tooltip("Extra clearance between two road stretches, on top of their half widths, in metres.")]
        [Min(0f)] public float margin = 4f;
        [Tooltip("The road may never head more than this far off the start direction, in degrees.")]
        [Range(45, 360)] public float maxDrift = 180f;
        [Tooltip("The whole track must stay inside this area (x, z in metres from the start).")]
        public Rect area = new Rect(-250f, -40f, 500f, 520f);
        [Tooltip("Safety stop for the backtracking search.")]
        [Min(10)] public int maxSteps = 2000;
    }

    public class Placed
    {
        public SectionDef def;
        public Vector2 position; // where the section starts
        public float yaw;        // heading at its start
        public Vector2 exitPosition;
        public float exitYaw;
        public readonly List<SectionDef.Sample> samples = new List<SectionDef.Sample>(); // world space
    }

    public class Result
    {
        public bool success;
        public readonly List<Placed> sections = new List<Placed>();
        public int steps, backtracks;
        public string message;
    }

    public static Result Generate(IList<SectionDef> library, Settings settings)
    {
        var run = new Run(library, settings);
        return run.Execute();
    }

    private class Run
    {
        private readonly IList<SectionDef> library;
        private readonly Settings s;
        private readonly System.Random random;
        private readonly Result result = new Result();
        private readonly List<HashSet<SectionDef>> banned = new List<HashSet<SectionDef>>();
        private readonly Dictionary<SectionDef, int> uses = new Dictionary<SectionDef, int>();
        private readonly Dictionary<SectionDef, string> fitFailures = new Dictionary<SectionDef, string>(); // why a pick last didn't fit
        private string lastReason;

        public Run(IList<SectionDef> library, Settings settings)
        {
            this.library = library;
            s = settings;
            random = new System.Random(settings.seed);
        }

        public Result Execute()
        {
            for (int i = 0; i < s.sectionCount; i++) banned.Add(new HashSet<SectionDef>());

            if (library == null || library.Count == 0) { result.message = "Library is empty."; return result; }
            result.success = Fill(0);
            if (!result.success && result.steps > s.maxSteps)
                result.message = $"Gave up after {s.maxSteps} steps. Last dead end: {result.message}";
            else if (!result.success && result.message == null)
                result.message = "No track found: every combination was tried and none fits (area too small, margin too big or drift too strict).";
            return result;
        }

        // Depth-first search; a slot's `banned` set remembers picks that led nowhere.
        private bool Fill(int slot)
        {
            if (slot == s.sectionCount) return true;

            while (true)
            {
                if (++result.steps > s.maxSteps) return false;

                SectionDef pick = PickFor(slot);
                if (pick == null) return false; // nothing left at this slot -> caller backs up

                Placed placed = TryPlace(slot, pick);
                if (placed == null) { banned[slot].Add(pick); fitFailures[pick] = lastReason; continue; }

                result.sections.Add(placed);
                uses[pick] = Uses(pick) + 1;

                if (Fill(slot + 1)) return true;

                result.sections.RemoveAt(result.sections.Count - 1);
                uses[pick]--;
                banned[slot].Add(pick);
                result.backtracks++;
                for (int i = slot + 1; i < banned.Count; i++) banned[i].Clear();
            }
        }

        private int Uses(SectionDef def) => uses.TryGetValue(def, out int n) ? n : 0;

        // Rules 1 + 2: filter, then roll by weight.
        private SectionDef PickFor(int slot)
        {
            bool first = slot == 0, last = slot == s.sectionCount - 1;
            float progress = s.sectionCount <= 1 ? 1f : slot / (float)(s.sectionCount - 1);
            float targetDifficulty = progress * s.maxDifficulty;
            float drift = result.sections.Count > 0 ? result.sections[result.sections.Count - 1].exitYaw : 0f;

            var candidates = new List<SectionDef>();
            var weights = new List<float>();
            float total = 0f;
            int invalid = 0, tried = 0, wrongScale = 0, locked = 0, tooHard = 0, used = 0, wrongRole = 0, recent = 0, drifted = 0;

            foreach (SectionDef def in library)
            {
                if (!def.IsValid) { invalid++; continue; }
                if (banned[slot].Contains(def)) { tried++; continue; }
                if (Mathf.Abs(def.scale - s.scale) > 0.01f) { wrongScale++; continue; }
                if (def.minLevel > s.level) { locked++; continue; }
                if (def.difficulty > s.maxDifficulty) { tooHard++; continue; }
                if (def.maxUses > 0 && Uses(def) >= def.maxUses) { used++; continue; }

                bool isStart = (def.tags & SectionTags.Start) != 0, isFinish = (def.tags & SectionTags.Finish) != 0;
                if (first != isStart || last != isFinish) { wrongRole++; continue; }
                // A one-section track can't be both; the first/last rules above already make that impossible.

                if (IsRecent(def)) { recent++; continue; }

                float weight = def.weight;
                weight /= 1f + 1.5f * Mathf.Abs(def.difficulty - targetDifficulty);
                float afterDrift = drift + def.NetTurn;
                if (Mathf.Abs(afterDrift) > s.maxDrift) { drifted++; continue; }
                if (Mathf.Abs(drift) > 90f && Mathf.Abs(afterDrift) < Mathf.Abs(drift)) weight *= 2f;

                candidates.Add(def);
                weights.Add(weight);
                total += weight;
            }

            if (candidates.Count == 0)
            {
                // Remember why the first dead end happened — it's usually a setting, not bad luck.
                if (result.message == null)
                    result.message = $"Slot {slot + 1} of {s.sectionCount} ({(first ? "must be a Start section" : last ? "must be a Finish section" : "middle")}) " +
                                     $"had no section to pick. Library of {library.Count} rejected: {wrongScale} wrong scale (level uses {s.scale}), " +
                                     $"{wrongRole} wrong Start/Finish role, {locked} locked by minLevel (level {s.level}), {tooHard} above max difficulty {s.maxDifficulty}, " +
                                     $"{used} at maxUses, {recent} used recently, {drifted} drift too big, {tried} already tried here, {invalid} with bad pieces text.";
                foreach (SectionDef def in banned[slot])
                    if (fitFailures.TryGetValue(def, out string why)) result.message += $"\n\"{def.name}\" did not fit: {why}";
                return null;
            }

            double roll = random.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private bool IsRecent(SectionDef def)
        {
            int count = result.sections.Count;
            for (int i = Mathf.Max(0, count - s.recentMemory); i < count; i++)
                if (result.sections[i].def == def) return true;
            return false;
        }

        // Rule 3: move the section onto the previous exit and check it fits.
        private Placed TryPlace(int slot, SectionDef def)
        {
            var placed = new Placed { def = def };
            if (result.sections.Count == 0) { placed.position = Vector2.zero; placed.yaw = 0f; }
            else
            {
                Placed previous = result.sections[result.sections.Count - 1];
                placed.position = previous.exitPosition;
                placed.yaw = previous.exitYaw;
            }

            float cos = Mathf.Cos(placed.yaw * Mathf.Deg2Rad), sin = Mathf.Sin(placed.yaw * Mathf.Deg2Rad);
            foreach (SectionDef.Sample local in def.Samples)
            {
                // Same rotation as Forward/Right: local +Z maps to heading, local +X to its right.
                Vector2 world = placed.position + local.position.x * new Vector2(cos, -sin) + local.position.y * new Vector2(sin, cos);
                placed.samples.Add(new SectionDef.Sample { position = world, yaw = placed.yaw + local.yaw });
            }
            placed.exitPosition = placed.samples[placed.samples.Count - 1].position;
            placed.exitYaw = placed.yaw + def.Exit.yaw;

            foreach (SectionDef.Sample sample in placed.samples)
                if (!s.area.Contains(sample.position))
                {
                    lastReason = $"leaves the area at x {sample.position.x:0}, z {sample.position.y:0} (area x {s.area.xMin:0}..{s.area.xMax:0}, z {s.area.yMin:0}..{s.area.yMax:0}). Enlarge Area.";
                    return null;
                }

            // The section must not run into itself either: points far apart along the road but close in space.
            float selfNeeded = def.Width + s.margin;
            int minGap = Mathf.CeilToInt(selfNeeded); // samples are ~2 m apart, so this is ~2x the clearance along the road
            for (int a = 0; a < placed.samples.Count; a++)
                for (int b = a + minGap; b < placed.samples.Count; b++)
                    if ((placed.samples[a].position - placed.samples[b].position).sqrMagnitude < selfNeeded * selfNeeded)
                    {
                        lastReason = $"crosses or touches itself (its road comes within {selfNeeded:0.0} m of itself near sample {a}/{b}). Widen the turns or shorten the shape.";
                        return null;
                    }

            // Everything but the previous section is checked in full; the previous one except around the joint.
            for (int i = 0; i < result.sections.Count; i++)
            {
                Placed other = result.sections[i];
                bool neighbour = i == result.sections.Count - 1;
                float needed = (def.Width + other.def.Width) * 0.5f + s.margin;
                float grace = neighbour ? needed * 1.5f : 0f;

                for (int a = 0; a < placed.samples.Count; a++)
                {
                    float fromStart = a * 2f;
                    for (int b = 0; b < other.samples.Count; b++)
                    {
                        if (neighbour && fromStart < grace && (other.samples.Count - 1 - b) * 2f < grace) continue;
                        if ((placed.samples[a].position - other.samples[b].position).sqrMagnitude < needed * needed)
                        {
                            lastReason = $"touches section {i + 1} (\"{other.def.name}\"). Roads must stay {needed:0.0} m apart.";
                            return null;
                        }
                    }
                }
            }
            return placed;
        }
    }
}
