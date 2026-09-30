using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TEMPORARY — stands in for the UI buttons until the UI exists. Delete it then.
/// Menu: Space / tap = Play. After the run: R = Replay, N = Next level.
/// </summary>
public class DebugGameControls : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    private void Update()
    {
        RunState state = gameManager.Run.State;

        if (state == RunState.Ready && StartPressed())
            gameManager.Play();
        else if (state == RunState.Ended && Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame) gameManager.Replay();
            else if (Keyboard.current.nKey.wasPressedThisFrame) gameManager.NextLevel();
        }
    }

    private static bool StartPressed()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
        return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    }
}
