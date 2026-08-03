using System.Collections.Generic;
using UnityEngine;

public static class EndRoundData 
{
    static public List<LevelSelected> levelsList;

    static public bool currentRoundWinnerIsCpu;
    static public bool currentRoundLoserIsCpu;

    // This static field is only ever decremented or zeroed (StartManager.setGameOptions, for
    // hardcore mode) after a run starts - nothing reset it back to the default between separate
    // campaign attempts in the same session, so exhausting continues once left every later
    // attempt with zero. Fixed by explicitly resetting to DefaultContinues in setGameOptions()
    // for non-hardcore runs. Value preserved as-is (2) - not a confirmed design number, just the
    // pre-existing default; the bug was the missing reset, not this count.
    public const int DefaultContinues = 2;
    static public int numberOfContinues = DefaultContinues;
    static public int currentRoundWinnerScore;
    static public int currentRoundLoserScore;

    static public int currentLevelIndex;
    static public int nextLevelIndex;

    static public Sprite currentRoundPlayerWinnerImage;
    static public Sprite currentRoundPlayerLoserImage;
    static public Sprite currentRoundCpuWinnerImage;
    static public Sprite currentRoundCpuLoserImage;

    static public string nextRoundLevelName;
    static public string nextRoundOpponentName;
    static public string currentRoundLevelName;
    static public string currentRoundOpponentName;

    //public static bool campaignGameOver { get; internal set; }
}
