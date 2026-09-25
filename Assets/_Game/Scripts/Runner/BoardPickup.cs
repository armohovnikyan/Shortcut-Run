using UnityEngine;

// Goes on a child of the runner: the pickup sphere.
// Kinematic Rigidbody is required so triggers fire for NavMeshAgent NPCs too (not just the CharacterController player).
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class BoardPickup : MonoBehaviour
{
    private Runner runner;

    private void Awake()
    {
        runner = GetComponentInParent<Runner>();
        GetComponent<SphereCollider>().isTrigger = true;
        GetComponent<Rigidbody>().isKinematic = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!runner.IsRunning) return;

        if (other.TryGetComponent(out ICollectable collectable) && collectable.TryCollect())
            runner.CollectBoards(1);
    }
}
