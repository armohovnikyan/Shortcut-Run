using System;
using UnityEngine;

/// <summary>
/// The sounds of one player: listens to its events (footsteps, jump, ledge grab, boards, boost).
/// Plain class: GameAudio makes one per spawned player and disposes it when the player goes.
/// </summary>
public class PlayerSounds : IDisposable
{
    private readonly Runner player;
    private readonly SoundLibrarySO library;
    private readonly AudioPlayer audio;
    private readonly float pickupComboTime;

    private int pickupIndex;
    private float lastPickupTime = float.NegativeInfinity;

    /// <param name="pickupComboTime">Seconds between pickups that still count as "in a row".</param>
    public PlayerSounds(Runner player, SoundLibrarySO library, AudioPlayer audio, float pickupComboTime)
    {
        this.player = player;
        this.library = library;
        this.audio = audio;
        this.pickupComboTime = pickupComboTime;

        player.Footstep += OnFootstep;
        player.Jumped += OnJumped;
        player.LedgeGrabbed += OnLedgeGrabbed;
        player.BoardsPickedUp += OnBoardsPickedUp;
        player.BoostChanged += OnBoostChanged;
    }

    public void Dispose()
    {
        if (player != null)
        {
            player.Footstep -= OnFootstep;
            player.Jumped -= OnJumped;
            player.LedgeGrabbed -= OnLedgeGrabbed;
            player.BoardsPickedUp -= OnBoardsPickedUp;
            player.BoostChanged -= OnBoostChanged;
        }
        audio.StopLoop(); // a boost loop must not outlive its player
    }

    private void OnFootstep() => audio.Play(player.IsOnBoards ? library.FootstepBoards : library.FootstepRoad);

    private void OnJumped(bool fromPad) => audio.Play(fromPad ? library.JumpPad : library.Jump);

    private void OnLedgeGrabbed() => audio.Play(library.LedgeGrab);

    // In a row: 01, 02, 03 … (stays on the last one). After a pause: back to 01.
    private void OnBoardsPickedUp(int stackCount)
    {
        if (Time.time - lastPickupTime > pickupComboTime) pickupIndex = 0;
        lastPickupTime = Time.time;

        audio.PlayAt(library.BoardPickup, pickupIndex);
        pickupIndex++;
    }

    private void OnBoostChanged(bool on)
    {
        if (on)
        {
            audio.Play(library.BoostStart);
            audio.StartLoop(library.BoostLoop);
        }
        else audio.StopLoop();
    }
}
