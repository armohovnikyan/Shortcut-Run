using UnityEngine;

// Timed magnet: the board magnet stays on for a while — every board that comes within the radius is collected.
// Same radius and board layers as the one-shot magnet; the cooldown starts after the duration.
[CreateAssetMenu(fileName = "TimedMagnet", menuName = "Scriptable Objects/Buffs/Timed Magnet")]
public class TimedMagnetSO : BoardMagnetSO
{
    [Tooltip("Seconds the magnet stays on. The cooldown starts after this.")]
    [SerializeField, Min(0f)] private float duration = 5f;

    public override float Apply(Player player)
    {
        Sweep(player);
        return duration;
    }

    // A fallen player keeps nothing: no pulling boards while falling.
    public override void TickEffect(Player player, float deltaTime)
    {
        if (!player.HasFallen) Sweep(player);
    }
}
