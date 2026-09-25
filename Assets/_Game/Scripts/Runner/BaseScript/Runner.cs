using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
// Для всех бегунов пока одинаковый исход финиша — просто идём к точке стоянки.
// Если позже понадобится особая логика для 1-го места (бонус-уровень и т.п.) —
// именно здесь нужно будет её разветвить.
public abstract class Runner : MonoBehaviour, IRunner
{
    [Header("Speed")]
    [SerializeField] protected float baseSpeed = 7f;
    [SerializeField] protected float maxSpeedBonus = 3f;
    [Space]
    [Header("Finish walk")]
    [SerializeField] float finishWalkSpeed = 5f;
    [SerializeField] float finishStopSqrDistance = 4f;
    [Space]
    [Header("Board")]
    [SerializeField] protected Transform boardStackPosition;
    [SerializeField] protected float boardStackSpace = 0.15f; // space distance between stacked boards.
    [SerializeField] protected PlaceableBoard placeableBoardPrefab;
    [SerializeField] protected BoardCarrier boardCarrier = new BoardCarrier();
    [Space]
    [Header("Motion")]
    [SerializeField] protected GroundProbe groundProbe = new GroundProbe();
    [SerializeField] protected RunnerMotion motion = new RunnerMotion();
    [Tooltip("Speed bonus gained per second on placed boards, and lost per second anywhere else.")]
    [SerializeField] private float placedBoardBonusPerSecond = 1.8f;


    protected RunnerAnimations animations;

    public float SpeedBonus { get; private set; } = 1f;
    public bool IsRunning { get; protected set; }


    public float CurrentSpeed => baseSpeed * SpeedBonus * boardCarrier.CarryMultiplier;

    public event Action<Runner> ReachedFinish;
    public event Action<Runner> Fell;

    // ---------- Lifecycle ----------
    // Subclasses must call base.Awake() / base.Start(), otherwise these do not run.

    protected virtual void Awake()
    {
        animations = new RunnerAnimations(GetComponent<Animator>());

        motion.Init(groundProbe, new BridgeBuilder(boardCarrier), transform.position);
        motion.Jumped += Jump;
        motion.ClimbStarted += OnClimbStarted;
        motion.Landed += CheckBoards;
        motion.BoardPlaced += CheckBoards;
        motion.Fell += OnFell;
    }

    protected virtual void Start()
    {
        // Moves to the run manager later.
        GameManager.Instance.RegistrRunner(transform);


        //ReachedFinish += runner => runner.WalkToFinalPoint();
    }

    public void BeginRace()
    {
        IsRunning = true;
        CheckBoards(); // switches the animator out of idle into running / running-with-boards
        OnBeginRace();
    }
    protected abstract void OnBeginRace();

    public void EndRace()
    {
        IsRunning = false;
        StopMoving();
        animations.TriggerDancing();
    }

    protected abstract void StopMoving();
    protected virtual void OnSpeedChanged() { }
    protected virtual void OnBoardsCollected(int stackCount) { }
    protected virtual void OnClimbStarted() { }

    // ---------- Shared behaviour (IRunner) ----------

    public void ChangeSpeedBonus(float bonus)
    {
        SpeedBonus = Mathf.Clamp(SpeedBonus + bonus, 1f, maxSpeedBonus);
        OnSpeedChanged();
    }

    public void CheckBoards()
    {
        if (boardCarrier.HasBoards)
            animations.SetRunningWithBoards();
        else
            animations.SetRunning();

        //The carry multiplier depends on the plank count.
        OnSpeedChanged();
    }

    // Called by BoardPickup. Spawns one carried PlaceableBoard per collected board onto the stack in the hands.
    public void CollectBoards(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            PlaceableBoard board = Instantiate(placeableBoardPrefab, boardStackPosition);
            board.transform.SetLocalPositionAndRotation(
                Vector3.up * boardStackSpace * boardCarrier.Count, Quaternion.identity);
            board.OnCarried();
            boardCarrier.Add(board);
        }
        BoardsCollected(boardCarrier.Count);
    }

    public void BoardsCollected(int stackCount)
    {
        CheckBoards();
        OnBoardsCollected(stackCount);
    }

    public void Jump() => animations.SetJumping();
    public void IsFailing() => animations.TriggerFalling();

    // ---------- Motion ----------

    /// <summary>
    /// Subclass passes where it wants to be horizontally this frame; gets back the final position
    /// with height handled (road, bridge, jump, climb, fall).
    /// </summary>
    protected Vector3 TickMotion(Vector3 plannedPosition)
    {
        Vector3 final = motion.Tick(plannedPosition, transform.forward, Time.deltaTime);

        float direction = motion.IsOnPlacedBoard ? 1f : -1f;
        ChangeSpeedBonus(direction * placedBoardBonusPerSecond * Time.deltaTime);

        return final;
    }

    /// <summary>Hand height control back to RunnerMotion after something else (NavMeshAgent) had it.</summary>
    protected void ResumeMotion() => motion.Resume(transform.position);

    // Only reports, like ReachedFinish. The run manager decides what a fall means.
    private void OnFell()
    {
        IsFailing();
        Fell?.Invoke(this);
    }

    // ---------- Finish ----------

    // Subclass calls this from its own trigger/waypoint check. It only reports.
    protected void NotifyReachedFinish()
    {
        ReachedFinish?.Invoke(this);
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

        boardCarrier.RemoveAll();

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
        Destroy(gameObject);
    }
}