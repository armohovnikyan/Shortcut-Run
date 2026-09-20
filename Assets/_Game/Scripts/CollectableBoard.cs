using System.Collections;
using UnityEngine;
public class CollectableBoard : BaseBoard
{
    private bool interactable = true;
    private float timerDuration = 2.8f; 
    private Coroutine timerRoutine = null;
    private string targetTag => boardData.TargetTag;
    protected override void Awake()
    {
        base.Awake();
    }

    private void OnCollisionEnter(Collision target)
    {
        if (!interactable) return;
        if (target.gameObject.CompareTag(targetTag))
        {
            ChangeBoolState(interactable);
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
        timerRoutine = StartCoroutine(ResetToInteractable(timerDuration));
    }
    private IEnumerator ResetToInteractable(float duration) 
    {
        float timeRemaining = duration;
        while (timeRemaining > 0)
        {
            timeRemaining -= Time.unscaledDeltaTime;
            yield return null;
        }
        ChangeBoolState(interactable);
        ChangeObjectState(interactable);
    } 

    private void ChangeBoolState(bool variable)
    {
        variable = !variable;
    }
}
