using System;
using UnityEngine;

/// <summary>
/// The one pre-race timer. RunManager ticks it; the UI never runs its own — it listens to Ticked
/// (3, 2, 1) and the run's RaceStarted ("GO!"), and may read Remaining / Progress01 for smooth art.
/// One timer means the art and the runners can never disagree about when "GO!" happens.
/// </summary>
public class Countdown
{
    private readonly int seconds;
    private float remaining;
    private int lastShown;
    private bool running;

    public event Action<int> Ticked;
    public event Action Finished;

    public float Remaining => remaining;
    public float Progress01 => seconds <= 0 ? 1f : 1f - remaining / seconds;

    public Countdown(int seconds)
    {
        this.seconds = Mathf.Max(0, seconds);
    }

    /// <summary>Starts at full time and reports the first number right away.</summary>
    public void Start()
    {
        remaining = seconds;
        lastShown = seconds;
        running = true;

        if (seconds == 0) { Finish(); return; }
        Ticked?.Invoke(seconds);
    }

    public void Tick(float deltaTime)
    {
        if (!running) return;

        remaining -= deltaTime;
        if (remaining <= 0f) { Finish(); return; }

        int shown = Mathf.CeilToInt(remaining);
        if (shown != lastShown)
        {
            lastShown = shown;
            Ticked?.Invoke(shown);
        }
    }

    private void Finish()
    {
        running = false;
        remaining = 0f;
        Finished?.Invoke();
    }
}
