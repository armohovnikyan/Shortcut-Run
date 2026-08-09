using UnityEngine;

public class PlayerKiller : MonoBehaviour
{
    [Tooltip("Тег, которым помечены боты/другие бегуны")]
    public string botTag = "Bot";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(botTag)) return;

        IKillable killable = other.GetComponentInParent<IKillable>();
        if (killable == null) return;


        Vector3 hitDirection = other.transform.position - transform.position;
        hitDirection.y = 0f;

        if (hitDirection.sqrMagnitude < 0.0001f)
        {
            hitDirection = transform.forward; 
        }

        killable.GetKnockedOut(hitDirection.normalized);
    }
}