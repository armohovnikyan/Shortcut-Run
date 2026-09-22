using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Runner))]
public abstract class Runner : MonoBehaviour, IRunner
{
    [Header("Speed")]
    [SerializeField] protected float baseSpeed = 7f;
    [SerializeField] protected float maxSpeedBonus = 3f;
    [Space]
    [Header("Carry penalty")]
    [Tooltip("Speed lost per plank in hands (0.01 = -1% per plank)")]
    [SerializeField] float speedPenaltyPerPlank = 0.01f;
    [Tooltip("Lowest carry multiplier, no matter how many planks")]
    [SerializeField] float minCarryMultiplier = 0.6f;
    [Space]
    [Header("Finish walk")]
    [SerializeField] float finishWalkSpeed = 5f;
    [SerializeField] float finishStopSqrDistance = 4f;
    [Space]
    [Header("Board")]
    [SerializeField] protected Transform boardStackPosition;
    [SerializeField] protected float boardStackSpace = 0.15f; // space distance between stacked boards.
    
    
    protected RunnerAnimations animations;
    //protected Plank planks;

    protected List<BaseBoard> collectedBoards = new List<BaseBoard>();

    public float SpeedBonus { get; private set; } = 1f;
    public bool IsRunning { get; protected set; }

    protected float CarryMultiplier =>
        Mathf.Max(minCarryMultiplier, 1f - collectedBoards.Count * speedPenaltyPerPlank);

    public float CurrentSpeed => baseSpeed * SpeedBonus * CarryMultiplier;

    public event Action<Runner> RunFinished;

    // ---------- Lifecycle ----------
    // Subclasses must call base.Awake() / base.Start(), otherwise these do not run.

    protected virtual void Awake()
    {
        animations = new RunnerAnimations(GetComponent<Animator>());
        //planks = GetComponent<Plank>();
    }

    protected virtual void Start()
    {
        // Moves to the run manager later.
        //GameManager.Instance.RegistrRunner(transform);
    }

    // ---------- Abstract: no sensible default ----------

    protected abstract void BeginRace();

    protected abstract void EndRace();

    // ---------- Virtual hooks: base knows WHEN, subclass decides WHAT ----------

    protected virtual void OnSpeedChanged() { }

    protected virtual void OnPlankCollected(int stackCount) { }

    // ---------- Shared behaviour (IRunner) ----------

    public void ChangeSpeedBonus(float bonus)
    {
        SpeedBonus = Mathf.Clamp(SpeedBonus + bonus, 1f, maxSpeedBonus);
        OnSpeedChanged();
    }

    public void CheckPlanks()
    {
        if (collectedBoards.Count > 0)
            animations.SetRunningWithBoards();
        else
            animations.SetRunning();

        //The carry multiplier depends on the plank count.
        OnSpeedChanged();
    }

    public void PlankCollected(int stackCount)
    {
        CheckPlanks();
        OnPlankCollected(stackCount);
    }

    public void Jump() => animations.SetJumping();
    public void Climb() => animations.SetClimbing();
    public void IsFailing() => animations.TriggerFalling();

    // ---------- Finish ----------

    protected void FinishRun()
    {
        if (!IsRunning) return;

        IsRunning = false;
        EndRace();
        animations.TriggerDancing();
        //------------
        //GameManager.Instance.UnRegisterRunner(transform, true);
        //------------
        RunFinished?.Invoke(this);
    }

    public void WalkToFinalPoint() => StartCoroutine(WalkToFinalPointRoutine());

    IEnumerator WalkToFinalPointRoutine()
    {
        Vector3 target = Finish.Instance.GetFreePoint();

        while (SqrDistanceXZ(target) > finishStopSqrDistance)
        {
            MoveTowards(target, finishWalkSpeed);
            yield return null;
        }

        //planks.RemoveAllPlanks();

        Vector3 direction = Finish.Instance.transform.position - transform.position;
        direction.y = 0f;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    // ---------- Helpers ----------

    protected float SqrDistanceXZ(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude;
    }

    protected void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
    
    // ---- Destroy game object -------
    public void DestroyRunner()
    {
        Destroy(this);
    }
}