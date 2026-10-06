using UnityEngine;

public class RunnerAnimations
{
    private readonly Animator _animator;

    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    private static readonly int IsRunningWithBoardsHash = Animator.StringToHash("IsRunningWithBoards");

    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");

    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");

    private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");

    private static readonly int IsDancingHash = Animator.StringToHash("IsDancing");

    private static readonly int IsIdleHash = Animator.StringToHash("IsIdle");

    private static readonly int IsIdleWithBoardsHash = Animator.StringToHash("IsIdleWithBoards");


    private static readonly int RunningState = Animator.StringToHash("Running");

    private static readonly int RunningWithBoardsState = Animator.StringToHash("RunnigWithPlanks");

    // When a foot touches the ground, as a fraction of each run loop. Measured from the clips (the foot's
    // lowest point): Run = left, right; Run_WithBoards holds two strides = 4 steps. New run clips → measure again.
    private static readonly float[] RunContacts = { 0.35f, 0.83f };

    private static readonly float[] RunWithBoardsContacts = { 0.15f, 0.42f, 0.65f, 0.92f };

    private int _stepState;
    private float _stepTime;


    public RunnerAnimations(Animator animator)
    {
        _animator = animator;
    }

    /// <summary>
    /// How many feet touched the ground since the last call; 0 outside the run states.
    /// Call once per frame: it follows the run animation itself, so steps match the feet at any speed.
    /// </summary>
    public int TakeFootsteps()
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return 0;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        float[] contacts = state.shortNameHash == RunningState ? RunContacts
                         : state.shortNameHash == RunningWithBoardsState ? RunWithBoardsContacts
                         : null;

        // normalizedTime keeps counting up across loops (2.35 = third loop, 35%): a new state or a restart
        // only sets the starting point, so entering the run never plays a step of its own.
        float now = state.normalizedTime;
        if (contacts == null || state.shortNameHash != _stepState || now < _stepTime)
        {
            _stepState = contacts == null ? 0 : state.shortNameHash;
            _stepTime = now;
            return 0;
        }

        int steps = 0;
        foreach (float contact in contacts)
            steps += Mathf.FloorToInt(now - contact) - Mathf.FloorToInt(_stepTime - contact);
        _stepTime = now;
        return steps;
    }
    public void Rebind()
    {
        _animator.Rebind();
    }

    public void SetIdle()
    {
        SetState(IsIdleHash);
    }

    public void SetIdleWithBoards()
    {
        SetState(IsIdleWithBoardsHash);
    }

    public void SetRunning()
    {
        SetState(IsRunningHash);
    }

    public void SetRunningWithBoards()
    {
        SetState(IsRunningWithBoardsHash);
    }

    public void SetClimbing()
    {
        SetState(IsClimbingHash);
    }

    public void SetJumping()
    {
        SetState(IsJumpingHash);
    }

    // Triggers clear the bools too: the controller enters every looping state from Any State
    // while its bool is true, so a bool left on would pull the runner straight out of fall/dance.
    public void TriggerFalling()
    {
        ResetStates();
        _animator.SetTrigger(IsFallingHash);
    }

    public void TriggerDancing()
    {
        ResetStates();
        _animator.SetTrigger(IsDancingHash);
    }


    private void SetState(int stateHash)
    {
        ResetStates();
        _animator.SetBool(stateHash, true);
    }

    private void ResetStates()
    {
        _animator.SetBool(IsIdleHash, false);
        _animator.SetBool(IsIdleWithBoardsHash, false);
        _animator.SetBool(IsRunningHash, false);
        _animator.SetBool(IsRunningWithBoardsHash, false);
        _animator.SetBool(IsClimbingHash, false);
        _animator.SetBool(IsJumpingHash, false);
    }
}