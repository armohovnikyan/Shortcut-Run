using System;
using UnityEngine;

/// <summary>
/// The player's coins: the only place they are added, spent and saved.
/// Rewards add; upgrades and cosmetics go through TrySpend. UI listens to CoinsChanged.
/// </summary>
public class Wallet
{
    private const string CoinsKey = "Coins";

    public int Coins { get; private set; }

    public event Action<int> CoinsChanged;

    /// <summary>Loads the saved coins; startingCoins is used only the first time the game runs.</summary>
    public Wallet(int startingCoins)
    {
        Coins = PlayerPrefs.GetInt(CoinsKey, startingCoins);
    }

    public bool CanAfford(int price) => price >= 0 && price <= Coins;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Coins += amount;
        Save();
    }

    /// <summary>False = not enough coins; nothing is taken.</summary>
    public bool TrySpend(int price)
    {
        if (!CanAfford(price)) return false;
        Coins -= price;
        Save();
        return true;
    }

    private void Save()
    {
        PlayerPrefs.SetInt(CoinsKey, Coins);
        PlayerPrefs.Save();
        CoinsChanged?.Invoke(Coins);
    }
}
