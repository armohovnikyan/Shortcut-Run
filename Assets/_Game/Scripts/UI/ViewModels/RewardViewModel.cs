/// <summary>What the reward panel shows after a finished run.</summary>
public class RewardViewModel
{
    /// <summary>Coins the GET button gives.</summary>
    public readonly Observable<int> Coins = new Observable<int>();
    /// <summary>The player's finishing place. 1 = first.</summary>
    public readonly Observable<int> Place = new Observable<int>(1);
}
