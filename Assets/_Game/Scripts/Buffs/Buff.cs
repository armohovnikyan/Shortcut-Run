using System;
using UnityEngine;

public enum BuffPhase { Ready, Active, Cooldown }

/// <summary>
/// One buff the player owns: how many pieces (saved), buying one, and using one during a run.
/// Using it starts the effect (BuffSO.Apply), then the cooldown; both are run-only and start over every run.
/// Ticked by GameManager. Whether the run allows using it right now (racing, not fallen) is GameManager's call.
/// </summary>
public class Buff
{
    private readonly BuffSO config;
    private readonly GameSave save;

    public int Count { get; private set; }
    public int Price => config != null ? config.Price : 0;
    public Sprite Icon => config != null ? config.Icon : null;
    public BuffPhase Phase { get; private set; }
    /// <summary>Has a piece and isn't running or cooling down. The run state is checked separately.</summary>
    public bool IsReady => config != null && Count > 0 && Phase == BuffPhase.Ready;
    /// <summary>Share of the wait still left: 1 = just used, 0 = ready. Covers the effect and the cooldown.</summary>
    public float WaitLeft => totalWait > 0f ? Mathf.Clamp01(timeLeft / totalWait) : 0f;

    private float effectLeft;   // seconds of the effect still to go (Active)
    private float timeLeft;     // seconds until Ready again (effect + cooldown)
    private float totalWait;

    /// <summary>Count or phase changed.</summary>
    public event Action Changed;
    /// <summary>Every tick while waiting (Active / Cooldown), for the cooldown fill. The new WaitLeft.</summary>
    public event Action<float> WaitTicked;

    /// <summary>No config = a buff that can't be bought or used (a missing asset on GameManager).</summary>
    public Buff(BuffSO config, GameSave save)
    {
        this.config = config;
        this.save = save;
        if (config != null) Count = Mathf.Max(0, save.Data.GetBuffCount(config.SaveKey));
    }

    /// <summary>Pays from the wallet and adds one piece. False = not enough coins; nothing is taken.</summary>
    public bool TryBuy(Wallet wallet)
    {
        if (config == null || !wallet.TrySpend(Price)) return false;

        SetCount(Count + 1);
        return true;
    }

    /// <summary>Spends one piece and applies it to the player. False = none left, or still running / cooling down.</summary>
    public bool TryUse(Player player)
    {
        if (!IsReady || player == null) return false;

        SetCount(Count - 1);
        effectLeft = config.Apply(player);
        totalWait = effectLeft + config.Cooldown;
        timeLeft = totalWait;
        SetPhase(effectLeft > 0f ? BuffPhase.Active : timeLeft > 0f ? BuffPhase.Cooldown : BuffPhase.Ready);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (Phase == BuffPhase.Ready) return;

        effectLeft -= deltaTime;
        timeLeft -= deltaTime;
        if (timeLeft <= 0f) SetPhase(BuffPhase.Ready);
        else if (Phase == BuffPhase.Active && effectLeft <= 0f) SetPhase(BuffPhase.Cooldown);
        WaitTicked?.Invoke(WaitLeft);
    }

    /// <summary>A new run: no effect running, no cooldown. Pieces are kept.</summary>
    public void ResetRun()
    {
        effectLeft = timeLeft = totalWait = 0f;
        SetPhase(BuffPhase.Ready);
        WaitTicked?.Invoke(0f);
    }

    private void SetPhase(BuffPhase phase)
    {
        if (phase == BuffPhase.Ready) timeLeft = totalWait = 0f;
        if (Phase == phase) return;
        Phase = phase;
        Changed?.Invoke();
    }

    private void SetCount(int count)
    {
        Count = count;
        save.Data.SetBuffCount(config.SaveKey, count);
        save.Save();
        Changed?.Invoke();
    }
}
