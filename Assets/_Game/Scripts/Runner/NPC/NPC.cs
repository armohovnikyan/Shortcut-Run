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
    private Vector3 _destination;
    private bool _finishNotified;

    public bool IsKnockedOut { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
    }

    /// <summary>Called once by the spawner right after Instantiate — NPC has no
    /// other way to know its route or where the race actually ends.</summary>
    public void SetPath(Vector3[] checkpoints, Vector3 destination)
    {
        _destination = destination;
        _navigator = new WaypointNavigator(checkpoints);
    }

    protected override void OnBeginRace()
    {
        _agent.SetDestination(_destination);
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
        if (!IsRunning || _navigator == null) return;

        _navigator.Tick(transform.position, boardCarrier.Count);

        switch (_navigator.State)
        {
            case NavigationState.Finished:
                // Guarded: State stays Finished every following frame, but the
                // run manager should only be told once.
                if (!_finishNotified)
                {
                    _finishNotified = true;
                    NotifyReachedFinish();
                }
                break;

            case NavigationState.Shortcutting:
                // Agent off = RunnerMotion owns the height (bridging, jumping) until the shortcut ends.
                if (_agent.enabled)
                {
                    _agent.enabled = false;
                    ResumeMotion();
                }
                MoveTowards(_navigator.ShortcutTarget, CurrentSpeed);
                transform.position = TickMotion(transform.position);
                break;

            case NavigationState.FollowingRoute:
                if (!_agent.enabled)
                {
                    _agent.enabled = true;
                    _agent.Warp(transform.position);
                    _agent.SetDestination(_destination);
                }
                break;
        }
    }

    // ---------- IKillAble ----------

    public void GetKnockedOut(Vector3 launchDirection)
    {
        if (IsKnockedOut) return;

        IsKnockedOut = true;
        _agent.enabled = false;
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