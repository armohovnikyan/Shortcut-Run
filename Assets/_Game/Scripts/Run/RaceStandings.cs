using System.Collections.Generic;

/// <summary>
/// Race places. Order = finished runners (in finishing order), then racing runners by metres left
/// ALONG the track (TrackRoute — not straight-line distance), then runners who are out (fell, knocked out).
/// Refresh() is called by the run manager a few times per second, not every frame.
/// </summary>
public class RaceStandings
{
    private readonly Level level;
    private readonly IReadOnlyList<Runner> runners;

    private readonly List<Runner> finished = new List<Runner>();
    private readonly List<Runner> outOfRace = new List<Runner>();
    private readonly List<Runner> racing = new List<Runner>();
    private readonly List<Runner> order = new List<Runner>();
    private readonly Dictionary<Runner, float> remaining = new Dictionary<Runner, float>();

    public IReadOnlyList<Runner> Order => order;

    public RaceStandings(Level level, IReadOnlyList<Runner> runners)
    {
        this.level = level;
        this.runners = runners;
    }

    public void Refresh()
    {
        racing.Clear();
        remaining.Clear();

        foreach (Runner runner in runners)
        {
            if (runner == null || finished.Contains(runner) || outOfRace.Contains(runner)) continue;
            remaining[runner] = level.GetRemainingDistance(runner.transform.position);
            racing.Add(runner);
        }
        racing.Sort((a, b) => remaining[a].CompareTo(remaining[b]));

        order.Clear();
        order.AddRange(finished);
        order.AddRange(racing);
        order.AddRange(outOfRace);
    }

    /// <summary>Locks in a finishing place. Returns it (1 = first). Calling twice returns the same place.</summary>
    public int MarkFinished(Runner runner)
    {
        if (!finished.Contains(runner))
        {
            outOfRace.Remove(runner);
            finished.Add(runner);
        }
        Refresh();
        return finished.IndexOf(runner) + 1;
    }

    public void MarkOut(Runner runner)
    {
        if (!finished.Contains(runner) && !outOfRace.Contains(runner))
            outOfRace.Add(runner);
    }

    /// <summary>1 = first. 0 = not in this race.</summary>
    public int GetPlace(Runner runner)
    {
        int index = order.IndexOf(runner);
        return index < 0 ? 0 : index + 1;
    }
}
