using System;
using UnityEngine;

/// <summary>
/// The 1st-place bonus rules. The finish counts as x1; every platform the player stands on raises it.
/// The bonus is over when the player:
///  - reaches the last platform (can't leave it), or
///  - stands on the last reached platform (or the finish) with no boards left — nowhere to go.
/// A fall is handled by RunManager: it drags the player back to StandPoint, with no board bonus.
/// Pure rules — moves nothing, plays nothing.
/// </summary>
public class BonusRound
{
    private readonly int lastMultiplier;

    public int Multiplier { get; private set; } = 1;
    /// <summary>Where the finish flow happens if the bonus ends now.</summary>
    public Transform StandPoint { get; private set; }

    public event Action<int> MultiplierReached;

    public BonusRound(Transform finishStandPoint, int lastMultiplier)
    {
        StandPoint = finishStandPoint;
        this.lastMultiplier = lastMultiplier;
    }

    /// <summary>Call every frame during the bonus. True = the bonus is over; run the finish flow at StandPoint.</summary>
    public bool Tick(Runner player)
    {
        Collider ground = player.GroundCollider; // null while bridging, jumping, climbing or falling
        MultiplierPlatform platform = ground != null ? ground.GetComponentInParent<MultiplierPlatform>() : null;

        if (platform != null && platform.Multiplier > Multiplier)
        {
            Multiplier = platform.Multiplier;
            StandPoint = platform.StandPoint;
            MultiplierReached?.Invoke(Multiplier);
        }

        if (Multiplier >= lastMultiplier) return true;
        if (player.BoardCount > 0 || ground == null) return false;

        // Out of boards and standing: on the reached platform, or still on the finish (x1).
        // Someone's placed boards don't count — the player is still on the way.
        if (platform != null) return platform.Multiplier == Multiplier;
        return Multiplier == 1 && !ground.TryGetComponent(out PlaceableBoard _);
    }
}
