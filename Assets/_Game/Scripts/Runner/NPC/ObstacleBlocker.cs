using UnityEngine;
using UnityEngine.AI;

// Goes on the NPC prefab. Makes moving obstacles solid for NPCs, the way the player's CharacterController
// already finds them: walking into one stops the NPC, one sweeping into it pushes it aside.
// A NavMeshAgent ignores colliders, so every frame this checks the NPC's own box against the obstacles and
// moves the agent back out of any it overlaps. Only colliders under a MovingObstacle count — an obstacle
// without colliders (the swing) does nothing.
[RequireComponent(typeof(NavMeshAgent), typeof(BoxCollider))]
public class ObstacleBlocker : MonoBehaviour
{
    [Tooltip("Layers the obstacles' colliders are on.")]
    [SerializeField] private LayerMask obstacleLayers = 1; // Default

    private readonly Collider[] hits = new Collider[8];
    private NavMeshAgent agent;
    private BoxCollider body;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        body = GetComponent<BoxCollider>();
    }

    // LateUpdate: after the agent has moved the NPC this frame.
    private void LateUpdate()
    {
        // Agent off = shortcut, jump or knockout: RunnerMotion owns the position then.
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Vector3 centre = transform.TransformPoint(body.center);
        Vector3 halfSize = Vector3.Scale(body.size, transform.lossyScale) * 0.5f;
        int count = Physics.OverlapBoxNonAlloc(centre, halfSize, hits, transform.rotation, obstacleLayers, QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit.GetComponentInParent<MovingObstacle>() == null) continue; // road, boards, other runners
            if (Physics.ComputePenetration(body, transform.position, transform.rotation,
                                           hit, hit.transform.position, hit.transform.rotation,
                                           out Vector3 direction, out float distance))
                push += direction * distance;
        }

        // Sideways only: the agent keeps the NPC on the NavMesh surface.
        push.y = 0f;
        if (push.sqrMagnitude > 0f) agent.Move(push);
    }
}
