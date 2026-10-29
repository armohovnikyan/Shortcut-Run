using UnityEngine;

// Board magnet: one sweep — every board lying within the radius around the player is collected at once.
[CreateAssetMenu(fileName = "BoardMagnet", menuName = "Scriptable Objects/Buffs/Board Magnet")]
public class BoardMagnetSO : BuffSO
{
    [Tooltip("Metres around the player. Every collectable board inside is picked up.")]
    [SerializeField, Min(0f)] private float radius = 6f;
    [Tooltip("Layers the collectable boards are on. Everything = works, but checks more colliders.")]
    [SerializeField] private LayerMask boardLayers = ~0;

    // Shared buffer: the sweep runs at most once per press, never twice at the same time.
    private static readonly Collider[] hits = new Collider[256];

    public override float Apply(Player player)
    {
        Sweep(player);
        return 0f;
    }

    /// <summary>Collects every board inside the radius around the player right now.</summary>
    protected void Sweep(Player player)
    {
        int count = Physics.OverlapSphereNonAlloc(
            player.transform.position, radius, hits, boardLayers, QueryTriggerInteraction.Collide);
        if (count == hits.Length)
            Debug.LogWarning($"{name}: more than {hits.Length} colliders in the magnet radius — some boards may be missed.", this);

        int collected = 0;
        for (int i = 0; i < count; i++)
        {
            // TryCollect is false for a board already taken (its second collider, or still respawning).
            if (hits[i].TryGetComponent(out ICollectable board) && board.TryCollect())
                collected++;
            hits[i] = null;
        }

        if (collected > 0) player.CollectBoards(collected);
    }
}
