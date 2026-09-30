/// <summary>What the in-run HUD shows: the countdown and the player's live place.</summary>
public class HudViewModel
{
    public const int CountdownHidden = -1;
    public const int CountdownGo = 0;

    /// <summary>3, 2, 1, then CountdownGo ("GO!"). CountdownHidden = no countdown on screen.</summary>
    public readonly Observable<int> Countdown = new Observable<int>(CountdownHidden);
    /// <summary>1 = first.</summary>
    public readonly Observable<int> Place = new Observable<int>(1);
    /// <summary>The place is only shown once the race is on.</summary>
    public readonly Observable<bool> PlaceVisible = new Observable<bool>();

    /// <summary>Back to the pre-run state (a new menu is shown).</summary>
    public void Reset()
    {
        Countdown.Value = CountdownHidden;
        PlaceVisible.Value = false;
        Place.Value = 1;
    }

    /// <summary>1 → "st", 2 → "nd", 3 → "rd", 4 → "th", 11 → "th", 21 → "st" …</summary>
    public static string PlaceSuffix(int place)
    {
        int lastTwo = place % 100;
        if (lastTwo >= 11 && lastTwo <= 13) return "th";

        switch (place % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }
}
