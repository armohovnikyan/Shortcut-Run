using UnityEngine;

/// <summary>
/// Settings of the NPC board-collecting behaviour. RunManager holds the asset; from `startLevel` on, every NPC
/// of the run uses it. Board numbers are absolute (boards in hand), not "drops".
/// Example (10 / 6 / 4): the NPC passes 10 → armed. After a shortcut it is down to 6 → it also collects boards
/// near its path (secondary to shortcuts). At 4 or fewer, collecting beats a shortcut whenever a board detour is
/// available. Back at 10 → collecting stops and the roles reset (stays armed).
/// </summary>
[CreateAssetMenu(fileName = "NpcBoardCollecting", menuName = "Scriptable Objects/NPC Board Collecting")]
public class NpcBoardCollectingSO : ScriptableObject
{
    [Tooltip("First level (1 = the first level) on which NPCs collect boards. Earlier levels: NPCs ignore this.")]
    [SerializeField, Min(1)] private int startLevel = 5;

    [Header("Board numbers")]
    [Tooltip("Arms the behaviour: once an NPC holds this many boards it starts watching its stock. " +
             "Also the number it collects back up to.")]
    [SerializeField, Min(1)] private int armBoards = 10;
    [Tooltip("Armed NPC with this many boards or fewer starts collecting boards near its path (secondary to shortcuts).")]
    [SerializeField, Min(0)] private int collectAtOrBelow = 6;
    [Tooltip("This many boards or fewer: collecting becomes the main goal and wins over a shortcut " +
             "(only while a board detour is available). Without boards a shortcut is impossible anyway.")]
    [SerializeField, Min(0)] private int priorityAtOrBelow = 4;

    [Header("Detour")]
    [Tooltip("How many route checkpoints ahead the NPC looks for boards.")]
    [SerializeField, Min(1)] private int lookAheadCheckpoints = 4;
    [Tooltip("A board counts only if it is at most this far (metres) from the NPC and from the route.")]
    [SerializeField, Min(1f)] private float maxDetour = 12f;
    [Tooltip("Boards higher or lower than this (metres) are on another level of the track (loop, bridge) — ignored.")]
    [SerializeField, Min(0.1f)] private float maxHeightDifference = 1.5f;
    [Tooltip("How often (seconds) an NPC without a target looks for one.")]
    [SerializeField, Min(0.05f)] private float scanInterval = 0.25f;
    [Tooltip("Gives up on a board after this many seconds (stuck, or someone else took it).")]
    [SerializeField, Min(0.5f)] private float giveUpAfter = 6f;

    public int StartLevel => startLevel;
    public int ArmBoards => armBoards;
    public int CollectAtOrBelow => collectAtOrBelow;
    public int PriorityAtOrBelow => priorityAtOrBelow;
    public int LookAheadCheckpoints => lookAheadCheckpoints;
    public float MaxDetour => maxDetour;
    public float MaxHeightDifference => maxHeightDifference;
    public float ScanInterval => scanInterval;
    public float GiveUpAfter => giveUpAfter;

    // Keeps the three numbers in order: priority <= collect < arm.
    private void OnValidate()
    {
        collectAtOrBelow = Mathf.Min(collectAtOrBelow, armBoards - 1);
        priorityAtOrBelow = Mathf.Min(priorityAtOrBelow, collectAtOrBelow);
    }
}
