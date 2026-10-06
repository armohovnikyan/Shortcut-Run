using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
// The runner only reports (Fell) and obeys (BeginRace, FinishAt). What a finish or a fall means —
// place, bonus, game over — is decided by RunManager; the finish line is the level's FinishLine.
public abstract class Runner : MonoBehaviour, IRunner
{
    [Header("Speed")]
    [SerializeField] protected float baseSpeed = 7f;
    [Tooltip("On from the first board placed or any placed board stepped on, off once back on normal road. " +
             "Jumps (jump pads too) keep whatever it was.")]
    [SerializeField] private SpeedBoost speedBoost = new SpeedBoost();
    [Space]
    [Header("Finish walk")]
    [SerializeField] float finishWalkSpeed = 7f;
    [Tooltip("How close to the stand point counts as arrived, in metres.")]
    [SerializeField] float finishStopDistance = 0.1f;
    [Tooltip("Bonus fall: how fast the runner is pulled back to the last platform's stand point.")]
    [SerializeField] float dragBackSpeed = 25f;
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
    [Space]
    [Header("Skin")]
    [Tooltip("Where a skin model is spawned. Empty = the object with the Animator.")]
    [SerializeField] private Transform skinParent;
    [Tooltip("Name the spawned model gets. Must match the root bone name the animations were made with (Mixamo: mixamorig:Hips). Empty = keep the prefab name.")]
    [SerializeField] private string skinRootName = "mixamorig:Hips";


    protected RunnerAnimations animations;
    private Animator animator;
    private BridgeBuilder bridge;

    private bool boostOn;
    private bool boostReported;
    private bool motionTickedThisFrame;

    public bool IsRunning { get; protected set; }
    /// <summary>The boost switch (on = speeding up or boosted). For effects / camera.</summary>
    public bool IsBoosted => boostOn;

    public float CurrentSpeed => speedBoost.SpeedFrom(baseSpeed) * boardCarrier.CarryMultiplier;

    /// <summary>Metres of gap the boards in hand can bridge right now.</summary>
    public float BridgeReach => placeableBoardPrefab == null
        ? 0f
        : boardCarrier.Count * BridgeBuilder.StepLength(placeableBoardPrefab);

    public int BoardCount => boardCarrier.Count;

    /// <summary>What the runner is standing on. Null while bridging, jumping, climbing or falling.</summary>
    public Collider GroundCollider => motion.State == MotionState.OnRoad ? motion.GroundCollider : null;

    /// <summary>Standing on something: road, a placed board or our own fresh bridge. False in the air.</summary>
    public bool IsGrounded => motion.State == MotionState.OnRoad || motion.State == MotionState.Bridging;
    /// <summary>Running over boards (placing them, or on a bridge) rather than the road. For footsteps.</summary>
    public bool IsOnBoards => motion.State == MotionState.Bridging || motion.IsOnPlacedBoard;

    public event Action<Runner> Fell;

    // For effects (sound, later VFX / vibration). The runner only reports; it never plays anything itself.
    /// <summary>A jump began. True = off a jump pad, false = out of boards over a gap.</summary>
    public event Action<bool> Jumped;
    public event Action LedgeGrabbed;
    /// <summary>Boards were picked up. The new stack count.</summary>
    public event Action<int> BoardsPickedUp;
    /// <summary>The board speed boost switched on / off. Also off once the runner stops (finish, fall).</summary>
    public event Action<bool> BoostChanged;
    /// <summary>A foot touched the ground, timed by the run animation. Only while running on the ground.</summary>
    public event Action Footstep;

    // ---------- Lifecycle ----------
    // Subclasses must call base.Awake() / base.Start(), otherwise these do not run.

    protected virtual void Awake()
    {
        // InChildren: the Animator can sit on a child (NPC keeps it on the object the skins go under).
        animator = GetComponentInChildren<Animator>();
        animations = new RunnerAnimations(animator);

        bridge = new BridgeBuilder(boardCarrier);
        motion.Init(groundProbe, bridge, transform.position);
        motion.Jumped += fromPad => { Jump(); Jumped?.Invoke(fromPad); };
        motion.ClimbStarted += () => { OnClimbStarted(); LedgeGrabbed?.Invoke(); };
        motion.Landed += CheckBoards;
        motion.BoardPlaced += CheckBoards;
        motion.Fell += OnFell;
    }

    protected virtual void Start() { }

    public void BeginRace()
    {
        IsRunning = true;
        CheckBoards(); // switches the animator out of idle into running / running-with-boards
        OnBeginRace();
    }
    protected abstract void OnBeginRace();

    protected abstract void StopMoving();
    protected virtual void OnSpeedChanged() { }
    protected virtual void OnBoardsCollected(int stackCount) { }
    protected virtual void OnClimbStarted() { }

    // ---------- Shared behaviour (IRunner) ----------

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
        BoardsPickedUp?.Invoke(boardCarrier.Count);
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
        UpdateBoostSwitch();
        motionTickedThisFrame = true;
        return final;
    }

    // ---------- Speed boost ----------

    // The switch only flips at two moments; everything in between (jumping, a jump pad, climbing,
    // falling) keeps it as it was. Any runner's placed boards count, not only our own.
    private void UpdateBoostSwitch()
    {
        if (motion.State == MotionState.Bridging || motion.IsOnPlacedBoard)
        {
            boostOn = true; // placing boards, or running on a bridge
            return;
        }

        Collider ground = GroundCollider; // non-null only when standing (OnRoad)
        if (ground != null && !ground.TryGetComponent(out JumpPad _))
            boostOn = false; // back on normal road
    }

    // After the subclass's Update, so the boost follows this frame's motion.
    protected virtual void LateUpdate()
    {
        if (!IsRunning)
        {
            motionTickedThisFrame = false;
            ReportBoost(false);
            return;
        }

        // Not moved by RunnerMotion this frame = an NPC walked by its NavMeshAgent, i.e. on the road.
        if (!motionTickedThisFrame) boostOn = false;
        motionTickedThisFrame = false;

        if (speedBoost.Tick(boostOn, Time.deltaTime)) OnSpeedChanged();
        ReportBoost(boostOn);

        // Taken every frame even in the air, so landing doesn't replay the steps missed while jumping.
        if (animations.TakeFootsteps() > 0 && IsGrounded) Footstep?.Invoke();
    }

    // boostOn can flip several times inside one frame's motion; listeners only hear the frame's result.
    private void ReportBoost(bool on)
    {
        if (on == boostReported) return;
        boostReported = on;
        BoostChanged?.Invoke(on);
    }

    /// <summary>Called by the spawner: bridges this runner builds belong to the level and are destroyed with it.</summary>
    public void SetBoardParent(Transform parent) => bridge.Parent = parent;

    /// <summary>
    /// Called by the spawner right after Instantiate, before the race: puts the model under the
    /// Animator and rebinds it, so the animations drive the new model's bones.
    /// </summary>
    public void ApplySkin(GameObject skinPrefab)
    {
        if (skinPrefab == null) return;
        if (animator == null)
        {
            Debug.LogError($"{name}: can't apply a skin — no Animator on this runner or its children.", this);
            return;
        }

        Transform parent = skinParent != null ? skinParent : animator.transform;
        GameObject model = Instantiate(skinPrefab, parent.position, parent.rotation, parent);
        if (!string.IsNullOrEmpty(skinRootName)) model.name = skinRootName;

        // A model imported with its own Animator brings its Avatar: take it, drop the duplicate Animator.
        if (model.TryGetComponent(out Animator modelAnimator))
        {
            if (modelAnimator.avatar != null) animator.avatar = modelAnimator.avatar;
            modelAnimator.enabled = false;
            Destroy(modelAnimator);
        }

        animations.Rebind(); // Rebind also resets parameters — set the pre-race state again
        animations.SetIdle();
    }

    /// <summary>Hand height control back to RunnerMotion after something else (NavMeshAgent) had it.</summary>
    protected void ResumeMotion() => motion.Resume(transform.position);

    // Only reports. The run manager decides what a fall means (game over, or drag-back in the bonus).
    // Protected: NPC also reports a knockout this way — it's out of the race either way.
    protected void OnFell()
    {
        IsFailing();
        Fell?.Invoke(this);
    }

    // ---------- Finish ----------

    /// <summary>
    /// The finish flow: stop racing, drop the boards, walk to the stand point, face its direction,
    /// then play the finish animation. `arrived` runs after that (RunManager opens the reward UI there).
    /// </summary>
    public void FinishAt(Transform standPoint, int place, Action arrived = null)
    {
        IsRunning = false;
        StopMoving();
        boardCarrier.DropAll(bridge.Parent);
        animations.SetRunning(); // hands are empty now
        StartCoroutine(FinishRoutine(standPoint, place, arrived, finishWalkSpeed, false));
    }

    /// <summary>
    /// Bonus fall: the runner is pulled from wherever it fell back to the stand point, still in its
    /// fall animation, and only then plays the finish animation. Same ending as FinishAt.
    /// </summary>
    public void DragTo(Transform standPoint, int place, Action arrived = null)
    {
        IsRunning = false;
        StopMoving();
        boardCarrier.DropAll(bridge.Parent);
        StartCoroutine(FinishRoutine(standPoint, place, arrived, dragBackSpeed, true));
    }

    // walking: arrived = close on the ground plane. dragged: the runner is far below, so height counts too.
    private IEnumerator FinishRoutine(Transform standPoint, int place, Action arrived, float speed, bool dragged)
    {
        float stopSqr = finishStopDistance * finishStopDistance;
        while ((dragged ? (standPoint.position - transform.position).sqrMagnitude
                        : SqrDistanceXZ(standPoint.position)) > stopSqr)
        {
            MoveTowards(standPoint.position, speed);
            yield return null;
        }

        // Stand points are rotated in the level to face where the camera will look from.
        transform.rotation = Quaternion.Euler(0f, standPoint.eulerAngles.y, 0f);
        PlayFinishAnimation(place);
        arrived?.Invoke();
    }

    // 2nd place and below will get the "upset" animation here once it exists.
    protected virtual void PlayFinishAnimation(int place) => animations.TriggerDancing();

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