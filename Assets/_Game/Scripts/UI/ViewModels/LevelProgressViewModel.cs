/// <summary>
/// The level bar at the top of the menu: levels come in groups of StagesPerBar (1-5, 6-10, 11-15 …)
/// and the bar shows the group the player is in, with the levels already beaten filled.
///
/// Where the level comes from: GameManager.LevelNumber, which is the index LevelLoader saves
/// (PlayerPrefs "LevelIndex") every time the player moves on to the next level. The save only ever goes
/// forward — there is no level select, so an old level can only be played again after wiping the app data.
/// GameUiViewModel calls SetLevel each time the menu is shown; a future save system only has to keep
/// feeding GameManager.LevelNumber and this class does not change.
/// </summary>
public class LevelProgressViewModel
{
    public const int StagesPerBar = 5;

    /// <summary>The level the player is on. 1 = first.</summary>
    public readonly Observable<int> Level = new Observable<int>(1);

    /// <summary>Number on the first stage of the bar: 1, 6, 11 …</summary>
    public int FirstStage => (Level.Value - 1) / StagesPerBar * StagesPerBar + 1;

    /// <summary>How many stages of the bar are already beaten (0 … StagesPerBar - 1).</summary>
    public int CompletedStages => (Level.Value - 1) % StagesPerBar;

    /// <summary>Level number written on stage `index` (0 = leftmost).</summary>
    public int StageNumber(int index) => FirstStage + index;

    public void SetLevel(int levelNumber) => Level.Value = levelNumber < 1 ? 1 : levelNumber;
}
