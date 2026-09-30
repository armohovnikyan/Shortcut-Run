using UnityEngine;

// Coins per finishing place. Tuning data only — the math is in RewardCalculator.
[CreateAssetMenu(fileName = "RewardTable", menuName = "Scriptable Objects/Reward Table")]
public class RewardTableSO : ScriptableObject
{
    [Tooltip("Coins for 1st, 2nd, 3rd … place (element 0 = 1st).")]
    [SerializeField] private int[] placeRewards = { 100, 75, 50, 25, 20, 15, 10 };
    [Tooltip("Coins for every place after the list ends.")]
    [SerializeField, Min(0)] private int otherPlacesReward = 5;
    [Tooltip("Coins per board still in hand at the finish.")]
    [SerializeField, Min(0)] private int coinsPerBoard = 1;

    public int CoinsPerBoard => coinsPerBoard;

    public int GetPlaceReward(int place)
    {
        if (place < 1) return 0;
        return place <= placeRewards.Length ? placeRewards[place - 1] : otherPlacesReward;
    }
}
