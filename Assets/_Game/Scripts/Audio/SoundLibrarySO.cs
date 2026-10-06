using UnityEngine;

// Every game sound in one asset: which clips play for what. Code never names audio files —
// swapping a sound is done here, in the Inspector.
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Scriptable Objects/Sound Library")]
public class SoundLibrarySO : ScriptableObject
{
    [Header("UI")]
    [SerializeField] private SoundCue click;
    [Tooltip("The whole 3-2-1-GO countdown in one clip: equal beats, the last one is GO.")]
    [SerializeField] private SoundCue countdown;
    [Tooltip("Seconds per beat in the countdown clip. Each countdown number plays one beat, GO plays the last.")]
    [SerializeField, Min(0.05f)] private float countdownBeatLength = 0.5f;
    [Tooltip("Beats in the countdown clip, GO included.")]
    [SerializeField, Min(1)] private int countdownBeats = 4;
    [Tooltip("Coin ticks when the reward lands in the wallet.")]
    [SerializeField] private SoundCue coin;
    [Tooltip("After the coin ticks.")]
    [SerializeField] private SoundCue coinDone;

    [Header("Run")]
    [Tooltip("The runners appear on the level.")]
    [SerializeField] private SoundCue opponentsSpawn;
    [Tooltip("The player finished 1st.")]
    [SerializeField] private SoundCue victory;

    [Header("Bonus multipliers")]
    [Tooltip("x2 – x4")]
    [SerializeField] private SoundCue multiplierLow;
    [Tooltip("x5 – x9")]
    [SerializeField] private SoundCue multiplierMid;
    [Tooltip("x10 – x14")]
    [SerializeField] private SoundCue multiplierHigh;
    [Tooltip("x15 and above")]
    [SerializeField] private SoundCue multiplierMax;

    [Header("Player")]
    [Tooltip("Played in order (01, 02, …) while picking boards up in a row, so the pitch climbs.")]
    [SerializeField] private SoundCue boardPickup;
    [Tooltip("Out of boards over a gap.")]
    [SerializeField] private SoundCue jump;
    [Tooltip("Bounced off a jump pad.")]
    [SerializeField] private SoundCue jumpPad;
    [SerializeField] private SoundCue ledgeGrab;
    [Tooltip("Fell during the race.")]
    [SerializeField] private SoundCue fallWater;
    [Tooltip("Fell off the bonus platforms.")]
    [SerializeField] private SoundCue fallVoid;
    [Tooltip("The board speed boost kicks in.")]
    [SerializeField] private SoundCue boostStart;
    [Tooltip("Loops while the board speed boost is on. Only the first clip is used.")]
    [SerializeField] private SoundCue boostLoop;
    [SerializeField] private SoundCue footstepRoad;
    [SerializeField] private SoundCue footstepBoards;

    [Header("Opponents")]
    [Tooltip("An opponent was knocked off the road.")]
    [SerializeField] private SoundCue opponentKnockedOut;

    public SoundCue Click => click;
    public SoundCue Countdown => countdown;
    public float CountdownBeatLength => countdownBeatLength;
    public int CountdownBeats => countdownBeats;
    public SoundCue Coin => coin;
    public SoundCue CoinDone => coinDone;
    public SoundCue OpponentsSpawn => opponentsSpawn;
    public SoundCue Victory => victory;
    public SoundCue BoardPickup => boardPickup;
    public SoundCue Jump => jump;
    public SoundCue JumpPad => jumpPad;
    public SoundCue LedgeGrab => ledgeGrab;
    public SoundCue FallWater => fallWater;
    public SoundCue FallVoid => fallVoid;
    public SoundCue BoostStart => boostStart;
    public SoundCue BoostLoop => boostLoop;
    public SoundCue FootstepRoad => footstepRoad;
    public SoundCue FootstepBoards => footstepBoards;
    public SoundCue OpponentKnockedOut => opponentKnockedOut;

    public SoundCue MultiplierFor(int multiplier)
    {
        if (multiplier < 5) return multiplierLow;
        if (multiplier < 10) return multiplierMid;
        if (multiplier < 15) return multiplierHigh;
        return multiplierMax;
    }
}
