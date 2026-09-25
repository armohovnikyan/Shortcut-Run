using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : Runner
{
    [Space]
    [SerializeField] private PlayerSteering steering = new PlayerSteering();
    
    private CharacterController _controller;
    private IInputReader _input;

    protected override void Awake()
    {
        base.Awake();
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<IInputReader>();
    }

    protected override void Start()
    {
        base.Start();
    }

    private void Update()
    {
        // Trigger-based callbacks (finish, planks) live on Runner and keep
        // working regardless of this check — only per-frame movement is gated.
        if (!IsRunning) return;

        steering.Rotate(transform, _input.TurnInput, Time.deltaTime);

        // Controller only pushes forward and handles collisions; RunnerMotion decides the height.
        Vector3 planned = transform.position + steering.ForwardStep(transform, CurrentSpeed, Time.deltaTime);
        Vector3 final = TickMotion(planned);
        _controller.Move(final - transform.position);
    }

    protected override void OnBeginRace()
    {
        // Nothing extra needed yet — IsRunning (set by BeginRace) is enough
        // to unlock Update(). Reset any input-driven state here later if needed.
    }

    protected override void StopMoving()
    {
        // Update() already stops moving once IsRunning is false.
    }

    protected override void OnClimbStarted() => Climb();

    private void Climb() => animations.SetClimbing();
}
