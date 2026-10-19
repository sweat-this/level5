using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Last-launch menu memory only. Scene changes retain it; a new application/play session clears it.
/// Live edits belong to StartMenuSelectionState, and gameplay always uses match configuration.
/// </summary>
public static class StartMenuPreferences
{
    private static int modeIndex;
    private static int levelIndex;
    private static int friendIndex;
    private static bool trafficEnabled;
    private static bool hardcoreEnabled;
    private static bool obstaclesEnabled;

    public static bool HardcoreEnabled => hardcoreEnabled;

    public static void RestoreTo(StartMenuSelectionState selection)
    {
        selection.ModeIndex = modeIndex;
        selection.LevelIndex = levelIndex;
        selection.FriendIndex = friendIndex;
        selection.TrafficEnabled = trafficEnabled;
        selection.HardcoreEnabled = hardcoreEnabled;
        selection.ObstaclesEnabled = obstaclesEnabled;
    }

    public static void RememberSelection(StartMenuSelectionState selection)
    {
        modeIndex = selection.ModeIndex;
        levelIndex = selection.LevelIndex;
        friendIndex = selection.FriendIndex;
    }

    /// <summary>
    /// Preserve the old bridge publication boundary, including campaign and versus launches.
    /// Remember resolved values, not requested toggles a mode or arena may have rejected.
    /// This never changes the live menu or publishes gameplay configuration.
    /// </summary>
    public static void RememberResolvedModifiers(ResolvedMatchRules rules)
    {
        trafficEnabled = rules.TrafficEnabled;
        hardcoreEnabled = rules.Hardcore;
        obstaclesEnabled = rules.ObstaclesEnabled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewProcess()
    {
        modeIndex = levelIndex = friendIndex = 0;
        trafficEnabled = hardcoreEnabled = obstaclesEnabled = false;
    }
}
