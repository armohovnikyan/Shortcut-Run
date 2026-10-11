using UnityEngine;

// Goes on the Player (next to its CharacterController): touching a bot while racing knocks it off the road.
// Only the player knocks — bots can't knock each other (BotsLayer ignores itself in the collision matrix)
// and the player isn't IKillAble. Whether the bot can be knocked right now is the bot's own call.
[RequireComponent(typeof(Runner))]
public class BotKnocker : MonoBehaviour
{
    [Tooltip("Layers of the runners that can be knocked out (the NPCs' BotsLayer).")]
    [SerializeField] private LayerMask botLayers;

    private Runner runner;

    private void Awake()
    {
        runner = GetComponent<Runner>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!runner.IsRunning) return;
        if ((botLayers.value & (1 << other.gameObject.layer)) == 0) return;

        IKillAble killable = other.GetComponentInParent<IKillAble>();
        if (killable == null) return;

        // Away from the player; dead-on overlap = straight ahead.
        Vector3 direction = other.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        killable.GetKnockedOut(direction.normalized);
    }
}
