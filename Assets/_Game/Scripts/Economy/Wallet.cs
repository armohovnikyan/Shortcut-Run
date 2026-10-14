using System;

/// <summary>
/// The player's coins: the only place they are added and spent. Saved through GameSave.
/// Rewards add; upgrades and cosmetics go through TrySpend. UI listens to CoinsChanged.
/// </summary>
public class Wallet
{
    private readonly GameSave save;

    public int Coins => save.Data.coins;

    public event Action<int> CoinsChanged;

    /// <summary>Coins come from the save (a new save starts with GameManager's Starting Coins).</summary>
    public Wallet(GameSave save) => this.save = save;

    public bool CanAfford(int price) => price >= 0 && price <= Coins;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        SetCoins(Coins + amount);
    }

    /// <summary>False = not enough coins; nothing is taken.</summary>
    public bool TrySpend(int price)
    {
        if (!CanAfford(price)) return false;
        SetCoins(Coins - price);
        return true;
    }

    private void SetCoins(int coins)
    {
        save.Data.coins = coins;
        save.Save();
        CoinsChanged?.Invoke(coins);
    }
}
