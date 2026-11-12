using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPC : Runner, IKillAble
{
    [Space]
    [Header("Knockout")]
    [SerializeField] private float knockoutHeight = 4.5f;
    [SerializeField] private float knockoutDistance = 8f;
    [SerializeField] private float knockoutDuration = 2.8f;
    [Tooltip("Plays during the knockout flight. Should be a stopped, world-space child of the NPC.")]
    [SerializeField] private ParticleSystem knockoutTrail;

    private NavMeshAgent _agent;
    private WaypointNavigator _navigator;
    private int _agentTargetIndex = -1; // checkpoint the agent is currently walking to
    private bool _leftRoadInShortcut;   // the current shortcut has gone off the road (bridge or jump)
    private Vector3[] _checkpoints;
    private BoardCollector _collector;  // null = this level has no board collecting
    private NpcBoardCollectingSO _collectSettings;
    private CollectableBoard _agentDetour; // board the agent is currently walking to instead of a checkpoint
    private NavMeshPath _scratchPath; // created in Awake: Unity forbids NavMeshPath in field initializers

    public bool IsKnockedOut { get; private set; }
    /// <summary>Hit off the road by a boosted runner (BotKnocker). For effects; the run hears it as a fall (Fell).</summary>
    public event Action<NPC> KnockedOut;

    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
        _scratchPath = new NavMeshPath();
    }

    /// <summary>Called once by the spawner right after Instantiate (Level.BuildNpcCheckpoints) —
    /// NPC has no other way to know its route. The last checkpoint should lie past the finish line.
    /// Joins the route at the next checkpoint ahead of where it was spawned, not at the track start.</summary>
    public void SetPath(Vector3[] checkpoints)
    {
        _checkpoints = checkpoints;
        _navigator = new WaypointNavigator(checkpoints, WaypointNavigator.NextIndexFrom(checkpoints, transform.position));
        _agentTargetIndex = -1;
        _collector = null;
    }

    /// <summary>Turns on board collecting for this NPC (after SetPath). Not called = the NPC never detours for boards.</summary>
    public void SetBoardCollecting(NpcBoardCollectingSO settings, CollectableBoard[] boards)
    {
        if (settings == null || boards == null || boards.Length == 0 || _checkpoints == null) return;
        _collectSettings = settings;
        _collector = new BoardCollector(settings, boards, _checkpoints);
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

        // Priority collecting with a board in reach: no shortcut now (it would spend the boards it needs).
        bool holdShortcuts = _collector != null && _collector.SuppressShortcuts;
        _navigator.Tick(transform.position, holdShortcuts ? 0f : BridgeReach);

        _collector?.Tick(transform.position, BoardCount, _navigator.CurrentIndex,
            _navigator.State == NavigationState.FollowingRoute && _agent.enabled, Time.deltaTime, IsReachable);

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
            _agentDetour = null;
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

        // Board detour: walk to the board; when it's gone the checkpoint is set again below (back on the route).
        CollectableBoard detour = _collector?.Target;
        if (detour != null)
        {
            if (_agentDetour != detour)
            {
                _agentDetour = detour;
                _agent.SetDestination(detour.transform.position);
            }
            return;
        }
        if (_agentDetour != null)
        {
            _agentDetour = null;
            _agentTargetIndex = -1;
        }

        if (_agentTargetIndex != _navigator.CurrentIndex)
        {
            _agentTargetIndex = _navigator.CurrentIndex;
            _agent.SetDestination(_navigator.CurrentCheckpoint);
            return; // path is computed over the next frames
        }

        if (!_agent.pathPending && _agent.pathStatus != NavMeshPathStatus.PathComplete)
            _navigator.ForceShortcut();
    }

    // A walkable NavMesh path to the board that isn't much longer than the detour limit.
    private bool IsReachable(Vector3 point)
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return false;
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return false;
        if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, _scratchPath)
            || _scratchPath.status != NavMeshPathStatus.PathComplete) return false;

        float length = 0f;
        Vector3[] corners = _scratchPath.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return length <= _collectSettings.MaxDetour * 1.5f;
    }

    // ---------- IKillAble ----------

    public bool TryKnockOut(Vector3 launchDirection)
    {
        // Only mid-race: not during the countdown, not once finished, not while already falling or flying.
        if (HasFallen || !IsRunning) return false;

        IsKnockedOut = true;
        _agent.enabled = false;
        OnFell(); // out of the race — the run manager takes it out of the standings
        KnockedOut?.Invoke(this);
        if (knockoutTrail != null) knockoutTrail.Play();
        StartCoroutine(KnockoutRoutine(launchDirection));
        return true;
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

        ReleaseTrail();
        gameObject.SetActive(false);
    }

    // Deactivating the NPC would cut the trail off mid-air: let it go, stop emitting and destroy itself once faded.
    // Kept under the NPC's parent so it still goes away with the level.
    private void ReleaseTrail()
    {
        if (knockoutTrail == null) return;

        knockoutTrail.transform.SetParent(transform.parent, true);
        ParticleSystem.MainModule main = knockoutTrail.main;
        main.stopAction = ParticleSystemStopAction.Destroy;
        knockoutTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        knockoutTrail = null;
    }
}