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


    public RunnerAnimations(Animator animator)
    {
        _animator = animator;
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

    public void TriggerFalling()
    {
        _animator.SetTrigger(IsFallingHash);
    }

    public void TriggerDancing()
    {
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