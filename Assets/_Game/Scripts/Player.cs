using UnityEngine;

public class Player : Runner
{
    protected override void Awake()
    {
        base.Awake();
    }
    public override void StartRun()
    {

    }

    protected override void StopMoving()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Finish"))
        {
            FinishRun();
        }
    }
}