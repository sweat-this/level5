using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Where the game gets its mode and level catalogs.
///
/// Authored <see cref="GameModeDefinition"/> / <see cref="LevelDefinition"/> assets under
/// <c>Resources/Match</c> win when they exist. Until the editor migration has written them, the
/// catalogs are built from the legacy start-menu prefab components instead, so the new code path is
/// live from the first commit and cannot drift from the shipping data while it waits for assets.
///
/// Everything is cached per source so a menu that asks on every highlighted button does not rebuild
/// definitions each frame.
/// </summary>
public static class MatchCatalogs
{
    public const string ModeResourcesPath = "Match/Modes";
    public const string LevelResourcesPath = "Match/Levels";

    private static GameModeCatalog cachedModes;
    private static LevelDefinitionCatalog cachedLevels;
    private static GameModeCompatibility cachedCompatibility;
    private static MatchConfigurationBuilder cachedBuilder;
    private static object modeSourceKey;
    private static object levelSourceKey;
    private static readonly List<string> conversionAnomalies = new List<string>();

    /// <summary>Anomalies found while converting legacy authored data. Empty after a clean load.</summary>
    public static IReadOnlyList<string> ConversionAnomalies => conversionAnomalies;

    /// <summary>True once both catalogs hold at least one entry.</summary>
    public static bool IsReady => cachedModes != null
        && cachedModes.Count > 0
        && cachedLevels != null
        && cachedLevels.Count > 0;

    public static GameModeCatalog Modes => cachedModes ?? GameModeCatalog.Empty();

    public static LevelDefinitionCatalog Levels => cachedLevels ?? LevelDefinitionCatalog.Empty();

    public static GameModeCompatibility Compatibility =>
        cachedCompatibility ??= new GameModeCompatibility(Modes, Levels);

    public static MatchConfigurationBuilder Builder =>
        cachedBuilder ??= new MatchConfigurationBuilder(Modes, Levels, Compatibility);

    /// <summary>
    /// Builds the catalogs from whatever the loading scene produced. Safe to call repeatedly: it
    /// rebuilds only when the source lists change identity.
    /// </summary>
    public static void EnsureBuilt(
        IReadOnlyList<StartScreenModeSelected> modeSources,
        IReadOnlyList<LevelSelected> levelSources)
    {
        EnsureModes(modeSources);
        EnsureLevels(levelSources);
    }

    /// <summary>Builds from <see cref="LoadedData"/> when it is available. Returns whether it could.</summary>
    public static bool EnsureBuiltFromLoadedData()
    {
        if (LoadedData.instance == null
            || LoadedData.instance.ModeSelectedData == null
            || LoadedData.instance.LevelSelectedData == null)
        {
            return false;
        }

        EnsureBuilt(LoadedData.instance.ModeSelectedData, LoadedData.instance.LevelSelectedData);
        return IsReady;
    }

    /// <summary>Replaces the catalogs outright. For the editor migration and for tests.</summary>
    public static void Override(GameModeCatalog modes, LevelDefinitionCatalog levels)
    {
        cachedModes = modes;
        cachedLevels = levels;
        modeSourceKey = null;
        levelSourceKey = null;
        cachedCompatibility = null;
        cachedBuilder = null;
    }

    public static void Reset()
    {
        Override(null, null);
        conversionAnomalies.Clear();
    }

    private static void EnsureModes(IReadOnlyList<StartScreenModeSelected> modeSources)
    {
        if (cachedModes != null && ReferenceEquals(modeSourceKey, modeSources))
        {
            return;
        }

        List<GameModeDefinition> definitions = LoadAuthoredModes();
        if (definitions.Count == 0)
        {
            conversionAnomalies.Clear();
            List<string> anomalies = new List<string>();
            definitions = GameModeDefinitionFactory.CreateAll(modeSources, anomalies);
            conversionAnomalies.AddRange(anomalies);
        }

        cachedModes = new GameModeCatalog(definitions);
        modeSourceKey = modeSources;
        cachedCompatibility = null;
        cachedBuilder = null;
        ReportProblems("game mode", cachedModes.Problems);
        ReportProblems("game mode", conversionAnomalies);
    }

    private static void EnsureLevels(IReadOnlyList<LevelSelected> levelSources)
    {
        if (cachedLevels != null && ReferenceEquals(levelSourceKey, levelSources))
        {
            return;
        }

        List<LevelDefinition> definitions = LoadAuthoredLevels();
        if (definitions.Count == 0)
        {
            definitions = LevelDefinitionFactory.CreateAll(levelSources);
        }

        cachedLevels = new LevelDefinitionCatalog(definitions);
        levelSourceKey = levelSources;
        cachedCompatibility = null;
        cachedBuilder = null;
        ReportProblems("level", cachedLevels.Problems);
    }

    private static List<GameModeDefinition> LoadAuthoredModes()
    {
        GameModeDefinition[] assets = Resources.LoadAll<GameModeDefinition>(ModeResourcesPath);
        return assets == null ? new List<GameModeDefinition>() : new List<GameModeDefinition>(assets);
    }

    private static List<LevelDefinition> LoadAuthoredLevels()
    {
        LevelDefinition[] assets = Resources.LoadAll<LevelDefinition>(LevelResourcesPath);
        return assets == null ? new List<LevelDefinition>() : new List<LevelDefinition>(assets);
    }

    private static void ReportProblems(string what, IReadOnlyList<string> problems)
    {
        if (problems == null)
        {
            return;
        }

        foreach (string problem in problems)
        {
            Debug.LogError($"Level 5 {what} catalog: {problem}");
        }
    }
}
