using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC : Runner, IKillAble
{
    [Space]
    [Header("Knockout")]
    [SerializeField] private float knockoutHeight = 3f;
    [SerializeField] private float knockoutDistance = 4f;
    [SerializeField] private float knockoutDuration = 1.2f;

    private NavMeshAgent _agent;
    private WaypointNavigator _navigator;
    private int _agentTargetIndex = -1; // checkpoint the agent is currently walking to
    private bool _leftRoadInShortcut;   // the current shortcut has gone off the road (bridge or jump)

    public bool IsKnockedOut { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>Called once by the spawner right after Instantiate (Level.BuildNpcCheckpoints) —
    /// NPC has no other way to know its route. The last checkpoint should lie past the finish line.
    /// Joins the route at the next checkpoint ahead of where it was spawned, not at the track start.</summary>
    public void SetPath(Vector3[] checkpoints)
    {
        _navigator = new WaypointNavigator(checkpoints, WaypointNavigator.NextIndexFrom(checkpoints, transform.position));
        _agentTargetIndex = -1;
    }

    protected override void OnBeginRace()
    {
        if (_navigator == null)
        {
            Debug.LogWarning($"{name}: no path — the spawner must call SetPath before the race starts.", this);
            return;
        }
        FollowRoute();
    }

    protected override void StopMoving()
    {
        _agent.enabled = false;
    }

    protected override void OnSpeedChanged()
    {
        _agent.speed = CurrentSpeed;
    }

    private void Update()
    {
        // Knocked out: the knockout arc owns the transform — don't let FollowRoute re-enable the agent.
        if (!IsRunning || _navigator == null || IsKnockedOut) return;

        _navigator.Tick(transform.position, BridgeReach);

        // Finishing is reported by the level's FinishLine, not here. Finished only means
        // "last checkpoint reached" — the agent just keeps walking to it.
        if (_navigator.State == NavigationState.Shortcutting)
            Shortcut();
        else
            FollowRoute();
    }

    // Agent off = RunnerMotion owns the height (bridging, jumping) until the shortcut ends.
    private void Shortcut()
    {
        if (_agent.enabled)
        {
            _agent.enabled = false;
            _agentTargetIndex = -1;
            _leftRoadInShortcut = false;
            ResumeMotion();
        }
        MoveTowards(_navigator.CurrentCheckpoint, CurrentSpeed);
        transform.position = TickMotion(transform.position);

        // Back on real road after the gap: hand over to the agent right here, at the road edge.
        // Waiting until the checkpoint (mid-road) is close would switch the agent on over the gap,
        // and its Warp would snap the NPC onto the NavMesh — a jump across the rest of the gap.
        bool onRoad = motion.State == MotionState.OnRoad && !motion.IsOnPlacedBoard;
        if (!onRoad) _leftRoadInShortcut = true;
        else if (_leftRoadInShortcut) _navigator.EndShortcut();
    }

    // The agent walks to the checkpoint just ahead — never straight to the finish, so it can't take
    // "the other way round" a loop. No NavMesh path to it = a gap between sections: cross it as a shortcut.
    private void FollowRoute()
    {
        if (!_agent.enabled)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);
        }
        if (!_agent.isOnNavMesh) return;

        if (_agentTargetIndex != _navigator.CurrentIndex)
        {
            _agentTargetIndex = _navigator.CurrentIndex;
            _agent.SetDestination(_navigator.CurrentCheckpoint);
            return; // path is computed over the next frames
        }

        if (!_agent.pathPending && _agent.pathStatus != NavMeshPathStatus.PathComplete)
            _navigator.ForceShortcut();
    }

    // ---------- IKillAble ----------

    public void GetKnockedOut(Vector3 launchDirection)
    {
        if (IsKnockedOut) return;

        IsKnockedOut = true;
        _agent.enabled = false;
        OnFell(); // out of the race — the run manager takes it out of the standings
        StartCoroutine(KnockoutRoutine(launchDirection));
    }

    private IEnumerator KnockoutRoutine(Vector3 launchDirection)
    {
        launchDirection.y = 0f;
        launchDirection.Normalize();

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < knockoutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / knockoutDuration);

            float height = 4f * knockoutHeight * t * (1f - t);
            Vector3 horizontal = launchDirection * knockoutDistance * t;

            transform.position = startPos + horizontal + Vector3.up * height;
            yield return null;
        }

        gameObject.SetActive(false);
    }
}