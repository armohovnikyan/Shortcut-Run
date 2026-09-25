using System.Collections;
using UnityEngine;

// Passive: it never looks for runners. A runner's BoardPickup finds it and calls TryCollect().
// Filtering who can touch it is done by the layer collision matrix, not by tags.
public class CollectableBoard : BaseBoard, ICollectable
{
    [SerializeField] private BoardSO boardSO;
    public BoardSO Data => boardSO;

    private bool interactable = true;
    private Coroutine timerRoutine = null;
    private Collider boardCollider;
    private Renderer[] renderers;

    private void Awake()
    {
        boardCollider = GetComponent<Collider>();
        boardCollider.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
    }

    public bool TryCollect()
    {
        if (!interactable) return false;

        SetInteractable(false);
        StartTimer();
        return true;
    }

    // Hide instead of SetActive(false): an inactive GameObject can't run the respawn coroutine.
    private void SetInteractable(bool condition)
    {
        interactable = condition;
        boardCollider.enabled = condition;
        foreach (Renderer r in renderers)
            r.enabled = condition;
    }

    private void StartTimer()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }
        timerRoutine = StartCoroutine(ResetInteractableStatus(boardSO.RespawnTime));
    }

    // Unscaled on purpose: boards keep respawning while the game is paused.
    private IEnumerator ResetInteractableStatus(float duration)
    {
        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            timeRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        timerRoutine = null;
        SetInteractable(true);
    }

    // If the stack gets disabled mid-respawn the coroutine dies with it — don't leave the board hidden forever.
    private void OnDisable()
    {
        timerRoutine = null;
        if (!interactable) SetInteractable(true);
    }
}
