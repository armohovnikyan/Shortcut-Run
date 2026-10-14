using UnityEngine;

// Run boost: the player runs faster for a while. Stacks on top of the board speed boost.
[CreateAssetMenu(fileName = "RunBoost", menuName = "Scriptable Objects/Buffs/Run Boost")]
public class RunBoostSO : BuffSO
{
    [Tooltip("Added to the player's speed while the boost lasts.")]
    [SerializeField, Min(0f)] private float speedBonus = 2f;
    [Tooltip("Seconds the boost lasts. The cooldown starts after this.")]
    [SerializeField, Min(0f)] private float duration = 5f;

    public override float Apply(Player player)
    {
        player.AddSpeedBonus(speedBonus, duration);
        return duration;
    }
}
