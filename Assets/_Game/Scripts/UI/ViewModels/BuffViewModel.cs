using System;
using UnityEngine;

/// <summary>
/// One buff, for both its buttons: the menu one (buy) and the in-run one (use, with the cooldown fill).
/// RaceActive is set by GameUiViewModel — the buff itself doesn't know whether the race is on.
/// </summary>
public class BuffViewModel : IDisposable
{
    private readonly GameManager game;
    private readonly Buff buff;
    private bool raceActive;

    public Sprite Icon => buff.Icon;
    public readonly Observable<int> Count = new Observable<int>();
    public readonly Observable<int> Price = new Observable<int>();
    /// <summary>The wallet covers one more piece.</summary>
    public readonly Observable<bool> CanBuy = new Observable<bool>();
    /// <summary>Racing, a piece left, not running or cooling down. Off = the in-run button is greyed out.</summary>
    public readonly Observable<bool> CanUse = new Observable<bool>();
    /// <summary>Share of the wait still left: 1 = just used, 0 = ready (the cooldown fill).</summary>
    public readonly Observable<float> WaitLeft = new Observable<float>();

    public BuffViewModel(GameManager game, Buff buff)
    {
        this.game = game;
        this.buff = buff;

        buff.Changed += Refresh;
        buff.WaitTicked += OnWaitTicked;
        game.Wallet.CoinsChanged += OnCoinsChanged;
        Refresh();
    }

    public void Dispose()
    {
        buff.Changed -= Refresh;
        buff.WaitTicked -= OnWaitTicked;
        game.Wallet.CoinsChanged -= OnCoinsChanged;
    }

    /// <summary>Menu button pressed.</summary>
    public void Buy() => game.BuyBuff(buff);

    /// <summary>In-run button pressed.</summary>
    public void Use() => game.UseBuff(buff);

    /// <summary>The race started (true) or is over for the player (false).</summary>
    public void SetRaceActive(bool active)
    {
        raceActive = active;
        Refresh();
    }

    private void OnCoinsChanged(int coins) => Refresh();
    private void OnWaitTicked(float waitLeft) => WaitLeft.Value = waitLeft;

    private void Refresh()
    {
        Count.Value = buff.Count;
        Price.Value = buff.Price;
        CanBuy.Value = game.Wallet.CanAfford(buff.Price);
        CanUse.Value = raceActive && buff.IsReady;
        WaitLeft.Value = buff.WaitLeft;
    }
}
