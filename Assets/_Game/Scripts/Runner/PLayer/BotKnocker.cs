using UnityEngine;

// Goes on every runner (Player and NPC prefabs). While its runner is boosted (running on boards, or still
// carrying the boost), any NPC it bumps into is knocked off the road and its boards in hand move to the
// knocker. Nobody knocks the player — the player isn't IKillAble.
// An overlap check, not OnTriggerEnter: NPCs must knock NPCs too, and BotsLayer ignores itself in the
// collision matrix (so the NavMesh agents don't push each other). Overlap queries ignore the matrix.
[RequireComponent(typeof(Runner))]
public class BotKnocker : MonoBehaviour
{
    [Tooltip("Layers of the runners that can be knocked out (the NPCs' BotsLayer).")]
    [SerializeField] private LayerMask botLayers;
    [Tooltip("How close another runner's collider must come to count as a bump, in metres.")]
    [SerializeField, Min(0.1f)] private float bumpRadius = 0.7f;
    [Tooltip("Height of the bump check above the runner's feet (its pivot).")]
    [SerializeField] private float bumpHeight = 1f;

    private readonly Collider[] hits = new Collider[8];
    private Runner runner;

    private void Awake()
    {
        runner = GetComponent<Runner>();
    }

    // Update, like the runners' own movement: checked every frame they move.
    private void Update()
    {
        if (!runner.CanKnockOut) return;

        Vector3 centre = transform.position + Vector3.up * bumpHeight;
        int count = Physics.OverlapSphereNonAlloc(centre, bumpRadius, hits, botLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Runner other = hits[i].GetComponentInParent<Runner>();
            if (other == null || other == runner || other is not IKillAble victim) continue;

            // Away from the knocker; dead-on overlap = straight ahead.
            Vector3 direction = other.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

            // Whether it can be knocked right now (racing, not already falling) is the victim's own call.
            if (!victim.TryKnockOut(direction.normalized)) continue;

            int boards = other.TakeAllBoards();
            if (boards > 0) runner.CollectBoards(boards);
        }
    }
}
