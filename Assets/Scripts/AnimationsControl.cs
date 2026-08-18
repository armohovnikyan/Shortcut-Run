using UnityEditor.Animations;
using UnityEngine;

public class AnimationsControl : MonoBehaviour
{
    public Animator Animator;

    public void Rebind()
   {
      Animator.Rebind();
   }

    public void SetRunning()
    {
       Animator.SetBool("Running", true);
       Animator.SetBool("Jump", false);
       Animator.SetBool("Failing", false); 
       Animator.SetBool("HavePlanks", false);
       Animator.SetBool("Idle", false);
    }
     public void SetRunningWithPlanks()
    {
       Animator.SetBool("HavePlanks", true);
       Animator.SetBool("Failing", false); 
       Animator.SetBool("Running", true);
       Animator.SetBool("Idle", false);
    }

    public void SetIdle()
    {
        Animator.SetBool("Idle", true);
        Animator.SetBool("Failing", false); 
        Animator.SetBool("Running", false);
        Animator.SetBool("HavePlanks", false);
    }

      public void SetDance()
    {
        Animator.SetTrigger("Dance");
        Animator.SetBool("Idle", false);
        Animator.SetBool("Failing", false); 
        Animator.SetBool("Running", false);
    }

    public void SetFailing()
    {
       Animator.SetBool("Failing", true); 
       Animator.SetBool("Running", false);
    }

    public void SetJump()
   {
      Animator.SetBool("Jump", true); 
      Animator.SetBool("Running", false);
      Animator.SetBool("HavePlanks", false);
   }

   public void SetClimbing(bool Climbing)
   {
       Animator.SetBool("Jump", !Climbing); 
       Animator.SetBool("Climbing", Climbing);
   }
}
