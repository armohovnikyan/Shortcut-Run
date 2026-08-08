using UnityEngine;

public class PlayerKiller : MonoBehaviour
{
    [Tooltip("Тег, которым помечены боты/другие бегуны")]
    public string botTag = "Bot";

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PlayerKiller] Триггер сработал с: {other.name}, тег: {other.tag}");
        if (!other.CompareTag(botTag)) return;
        IKillable killable = other.GetComponentInParent<IKillable>();
        if (killable == null) return;

        killable.GetKnockedOut(transform.forward);
    }
}