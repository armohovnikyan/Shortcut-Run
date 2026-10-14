using UnityEngine;

/// <summary>
/// One buff the player buys with coins and uses during the race (run boost, board magnet …).
/// Shared tuning lives here; each buff's subclass holds its own numbers and says what using it does.
/// The count, the cooldown and the save are handled by Buff.
/// </summary>
public abstract class BuffSO : ScriptableObject
{
    [Tooltip("Name the owned count is saved under. Must be different for every buff; don't change it after release.")]
    [SerializeField] private string saveKey = "Buff";
    [Tooltip("Shown on the menu and in-run buttons.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Price of one piece.")]
    [SerializeField, Min(0)] private int price = 500;
    [Tooltip("Seconds before it can be used again, counted from when its effect ends.")]
    [SerializeField, Min(0f)] private float cooldown = 10f;

    public string SaveKey => saveKey;
    public Sprite Icon => icon;
    public int Price => price;
    public float Cooldown => cooldown;

    /// <summary>Does the buff's effect on the player. Returns how many seconds the effect lasts (0 = instant).</summary>
    public abstract float Apply(Player player);
}
