using UnityEditor.Animations;
using UnityEngine;

public class AnimationsControl : MonoBehaviour
{
    public Animator Animator;

    public void SetRunning()
    {
       Animator.SetBool("Running", true);
       Animator.SetBool("Idle", false);
    }

    public void SetIdle()
    {
        Animator.SetBool("Idle", true);
        Animator.SetBool("Running", false);
    }

      public void SetDance()
    {
       Animator.SetTrigger("Dance");
        Animator.SetBool("Idle", false);
        Animator.SetBool("Running", false);
    }
}
