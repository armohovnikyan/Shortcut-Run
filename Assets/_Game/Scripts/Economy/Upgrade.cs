using System;
using UnityEngine;

/// <summary>
/// One upgrade the player levels up with coins: its saved level and the price of the next level.
/// Only knows level and price — what a level actually gives (more boards, offline coins) is read
/// from Level by the system that uses it.
/// </summary>
public class Upgrade
{
    private readonly UpgradeSO config;
    private readonly string saveKey;

    /// <summary>1 = nothing bought yet.</summary>
    public int Level { get; private set; } = 1;
    public bool IsMaxed => config == null || Level >= config.MaxLevel;
    /// <summary>Price of the next level. 0 when maxed.</summary>
    public int Price => IsMaxed ? 0 : config.GetPrice(Level);

    public event Action Changed;

    /// <summary>Loads the saved level. No config = an upgrade that can't be bought (shows MAX).</summary>
    public Upgrade(UpgradeSO config)
    {
        this.config = config;
        if (config == null) return;

        saveKey = "Upgrade_" + config.SaveKey;
        Level = Mathf.Clamp(PlayerPrefs.GetInt(saveKey, 1), 1, config.MaxLevel);
    }

    /// <summary>Pays from the wallet and levels up. False = maxed or not enough coins; nothing is taken.</summary>
    public bool TryBuy(Wallet wallet)
    {
        if (IsMaxed || !wallet.TrySpend(Price)) return false;

        Level++;
        PlayerPrefs.SetInt(saveKey, Level);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }
}
