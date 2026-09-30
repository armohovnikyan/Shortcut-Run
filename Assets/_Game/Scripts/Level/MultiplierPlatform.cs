using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// One bonus platform (x2 … x15). Needs a solid collider on the Road layer so the runner can stand on it —
/// the run finds it through the runner's own ground check, no trigger needed.
/// Holds only its number and where the player stands for the finish flow.
/// </summary>
public class MultiplierPlatform : MonoBehaviour
{
    [SerializeField, Min(1)] private int multiplier = 2;
    [Tooltip("Where the player stands and dances if the run ends here. Empty = the platform's own position. " +
             "Its blue (Z) arrow = the way the player faces.")]
    [SerializeField] private Transform standPoint;
    [Tooltip("Optional text showing the number (x5).")]
    [SerializeField] private TMP_Text label;

    public int Multiplier => multiplier;
    public Transform StandPoint => standPoint != null ? standPoint : transform;

    /// <summary>Called by the spawner right after Instantiate.</summary>
    public void Setup(int value)
    {
        multiplier = value;
        if (label != null) label.text = $"x{value}";
    }

    /// <summary>Starts `depth` metres below its place and rises into it after `delay` seconds.</summary>
    public void RiseFrom(float depth, float duration, float delay)
    {
        StartCoroutine(RiseRoutine(depth, duration, delay));
    }

    private IEnumerator RiseRoutine(float depth, float duration, float delay)
    {
        Vector3 target = transform.position;
        Vector3 start = target + Vector3.down * depth;
        transform.position = start;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float t = time / duration;
            transform.position = Vector3.Lerp(start, target, t * t * (3f - 2f * t)); // ease in and out
            yield return null;
        }
        transform.position = target;
    }
}
