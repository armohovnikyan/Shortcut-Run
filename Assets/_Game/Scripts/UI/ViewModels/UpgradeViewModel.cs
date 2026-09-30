using System;

/// <summary>One upgrade button: its level, the price of the next level and whether the player can pay it.</summary>
public class UpgradeViewModel : IDisposable
{
    private readonly GameManager game;
    private readonly Upgrade upgrade;

    public readonly Observable<int> Level = new Observable<int>();
    public readonly Observable<int> Price = new Observable<int>();
    public readonly Observable<bool> IsMaxed = new Observable<bool>();
    /// <summary>Not maxed and the wallet covers the price.</summary>
    public readonly Observable<bool> CanBuy = new Observable<bool>();

    public UpgradeViewModel(GameManager game, Upgrade upgrade)
    {
        this.game = game;
        this.upgrade = upgrade;

        upgrade.Changed += Refresh;
        game.Wallet.CoinsChanged += OnCoinsChanged;
        Refresh();
    }

    public void Dispose()
    {
        upgrade.Changed -= Refresh;
        game.Wallet.CoinsChanged -= OnCoinsChanged;
    }

    /// <summary>The button was pressed.</summary>
    public void Buy() => game.BuyUpgrade(upgrade);

    private void OnCoinsChanged(int coins) => Refresh();

    private void Refresh()
    {
        Level.Value = upgrade.Level;
        Price.Value = upgrade.Price;
        IsMaxed.Value = upgrade.IsMaxed;
        CanBuy.Value = !upgrade.IsMaxed && game.Wallet.CanAfford(upgrade.Price);
    }
}
