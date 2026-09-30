/// <summary>
/// Pure math: place, multiplier and boards in → coins out. No state, no Unity objects besides the table.
///   fell in the race      → 0
///   1st place             → place money × multiplier + boards     (100 × 4 + boards)
///   2nd place and below   → place money + boards
/// </summary>
public static class RewardCalculator
{
    public static int Calculate(RunResult result, RewardTableSO table)
    {
        if (!result.Finished || table == null) return 0;

        int placeMoney = table.GetPlaceReward(result.Place);
        if (result.Place == 1) placeMoney *= result.Multiplier < 1 ? 1 : result.Multiplier;

        return placeMoney + result.Boards * table.CoinsPerBoard;
    }
}
