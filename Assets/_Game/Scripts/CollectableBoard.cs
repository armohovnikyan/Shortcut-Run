using System.Collections;
using UnityEngine;
public class CollectableBoard : BaseBoard
{
    private bool interactable = true;
    private float timerDuration = 2.8f; 
    private Coroutine timerRoutine = null;
    private string targetTag => boardSO.TargetTag;

    private void OnCollisionEnter(Collision target)
    {
        if (!interactable) return;
        if (target.gameObject.CompareTag(targetTag))
        {
            interactable = !interactable;   
            ChangeObjectState(interactable);
            StartTimer();
        }
    }

    private void ChangeObjectState(bool condition)
    {
        gameObject.SetActive(condition);
    }
    private void StartTimer()
    {
        if(timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }
        timerRoutine = StartCoroutine(ResetInteractableStatus(timerDuration));
    }
    private IEnumerator ResetInteractableStatus(float duration) 
    {
        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            timeRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        interactable = !interactable;
        ChangeObjectState(interactable);
    } 

}
