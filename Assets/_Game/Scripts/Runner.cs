using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public abstract class Runner : MonoBehaviour, IRunner
{
    [Header("Speed")]
    [SerializeField] protected float baseSpeed = 7f;
    [SerializeField] protected float maxSpeedBonus = 3f;
    [Space]
    [Header("Carry penalty")]
    [Tooltip("Speed lost per plank in hands (0.02 = -2% per plank)")]
    [SerializeField] float speedPenaltyPerPlank = 0.02f;
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
    protected Plank planks;
    protected List<BaseBoard> CollectedBoards = new List<BaseBoard>();

    public float SpeedBonus { get; private set; } = 1f;
    public bool IsRunning { get; protected set; }

    protected float CarryMultiplier =>
        Mathf.Max(minCarryMultiplier, 1f - planks.CollectedPlanks.Count * speedPenaltyPerPlank);

    /// <summary>Single source of truth for speed. The player reads it every frame, the bot writes it to the agent.</summary>
    public float CurrentSpeed => baseSpeed * SpeedBonus * CarryMultiplier;

    /// <summary>Raised once when this runner crosses the finish. The run manager decides what happens next.</summary>
    public event Action<Runner> RunFinished;

    // ---------- Lifecycle ----------
    // Subclasses must call base.Awake() / base.Start(), otherwise these do not run.

    protected virtual void Awake()
    {
        animations = GetComponent<RunnerAnimations>();
        planks = GetComponent<Plank>();
    }

    protected virtual void Start()
    {
        // Moves to the run manager later.
        GameManager.Instance.RegistrRunner(transform);
    }

    // ---------- Abstract: no sensible default ----------

    /// <summary>Player: enable input. Bot: set the destination. Must set IsRunning = true.</summary>
    public abstract void StartRun();

    /// <summary>Player: stop input, tell the camera. Bot: disable the agent.</summary>
    protected abstract void StopMoving();

    // ---------- Virtual hooks: base knows WHEN, subclass decides WHAT ----------

    /// <summary>Called after the speed bonus or the plank count changed.</summary>
    protected virtual void OnSpeedChanged() { }

    /// <summary>Called only when a plank is picked up (not when placed). Player: update the counter UI.</summary>
    protected virtual void OnPlankCollected(int stackCount) { }

    // ---------- Shared behaviour (IRunner) ----------

    public void ChangeSpeedBonus(float bonus)
    {
        SpeedBonus = Mathf.Clamp(SpeedBonus + bonus, 1f, maxSpeedBonus);
        OnSpeedChanged();
    }

    public void CheckPlanks()
    {
        if (planks.CollectedPlanks.Count > 0)
            animations.SetRunningWithPlanks();
        else
            animations.SetRunning();

        // The carry multiplier depends on the plank count.
        OnSpeedChanged();
    }

    /// <summary>Called by Plank after a pickup; replaces the "is PlayerController" check.</summary>
    public void PlankCollected(int stackCount)
    {
        CheckPlanks();
        OnPlankCollected(stackCount);
    }

    public void Jump() => animations.SetJump();
    public void Climb(bool climbing) => animations.SetClimbing(climbing);
    public void IsFailing() => animations.SetFailing();

    // ---------- Finish ----------

    /// <summary>Both player (trigger) and bot (last waypoint) call this.</summary>
    protected void FinishRun()
    {
        if (!IsRunning) return;

        IsRunning = false;
        StopMoving();
        animations.SetDance();
        //------------
        GameManager.Instance.UnRegisterRunner(transform, true);
        //------------
        RunFinished?.Invoke(this);
    }

    /// <summary>Called by the run manager for runners that do not go to the bonus level.</summary>
    public void WalkToFinalPoint() => StartCoroutine(WalkToFinalPointRoutine());

    IEnumerator WalkToFinalPointRoutine()
    {
        Vector3 target = Finish.Instance.GetFreePoint();

        while (SqrDistanceXZ(target) > finishStopSqrDistance)
        {
            MoveTowards(target, finishWalkSpeed);
            yield return null;
        }

        planks.RemoveAllPlanks();

        Vector3 direction = Finish.Instance.transform.position - transform.position;
        direction.y = 0f;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    // ---------- Helpers ----------

    /// <summary>Squared horizontal distance.</summary>
    protected float SqrDistanceXZ(Vector3 point)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude;
    }

    /// <summary>Shared move-and-face. A bot that needs its agent in sync sets Agent.nextPosition after calling it.</summary>
    protected void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}