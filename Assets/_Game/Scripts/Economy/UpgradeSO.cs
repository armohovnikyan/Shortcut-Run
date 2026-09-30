using UnityEngine;

// Prices of one upgrade (board level, offline earnings, …). Tuning data only — the logic is in Upgrade.
[CreateAssetMenu(fileName = "Upgrade", menuName = "Scriptable Objects/Upgrade")]
public class UpgradeSO : ScriptableObject
{
    [Tooltip("Name the level is saved under. Must be different for every upgrade; don't change it after release.")]
    [SerializeField] private string saveKey = "Upgrade";
    [Tooltip("Price of the first level-up (level 1 → 2).")]
    [SerializeField, Min(0)] private int startPrice = 2500;
    [Tooltip("Added to the price for every level already bought.")]
    [SerializeField, Min(0)] private int priceStepPerLevel = 500;
    [Tooltip("Highest level. At this level the button shows MAX.")]
    [SerializeField, Min(1)] private int maxLevel = 50;

    public string SaveKey => saveKey;
    public int MaxLevel => maxLevel;

    /// <summary>Price to go from `level` to `level + 1`.</summary>
    public int GetPrice(int level) => startPrice + priceStepPerLevel * Mathf.Max(0, level - 1);
}
