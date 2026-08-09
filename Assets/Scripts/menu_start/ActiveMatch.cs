using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Holds the configuration for the match currently being loaded or played.
///
/// This is deliberately the only piece of match state that survives a scene load, and it is
/// write-once per match: a launch source calls <see cref="Begin"/> and everything downstream reads.
/// A gameplay scene that finds nothing here was entered directly (the editor's play-from-scene
/// workflow, or a scene reload), which is a case the runtime has to keep handling - see
/// <see cref="IsActive"/> callers.
/// </summary>
public static class ActiveMatch
{
    private static MatchConfiguration configuration;

    /// <summary>The configuration for this match, or null when the scene was entered directly.</summary>
    public static MatchConfiguration Configuration => configuration;

    public static bool IsActive => configuration != null;

    /// <summary>
    /// Makes a validated configuration the current match and starts a new result id for it.
    /// Called by the launch source immediately before the scene load.
    /// </summary>
    public static void Begin(MatchConfiguration matchConfiguration)
    {
        if (matchConfiguration == null)
        {
            Debug.LogError("ActiveMatch.Begin was given no configuration; the previous match stays current.");
            return;
        }

        configuration = matchConfiguration;
        MatchSession.BeginNewMatch();
    }

    /// <summary>
    /// The current match moved to another arena, keeping its mode, roster and modifiers.
    ///
    /// This is what a campaign round advance is: the same match continuing in the next level. It
    /// re-resolves the rules against the new arena rather than carrying the old ones across, since
    /// resolution depends on the level.
    ///
    /// Returns null when there is no match to continue, which the caller has to handle - it means
    /// the campaign was entered from somewhere that never built a configuration.
    /// </summary>
    public static MatchConfiguration ContinueInLevel(LevelDefinition level)
    {
        if (configuration == null || level == null)
        {
            return null;
        }

        return new MatchConfiguration(
            configuration.Mode,
            level,
            configuration.Roster,
            configuration.Modifiers,
            MatchConfigurationBuilder.Resolve(configuration.Mode, level, configuration.Roster, configuration.Modifiers),
            configuration.Cheerleader,
            configuration.Source);
    }

    /// <summary>Clears the current match. Used when returning to the menu and by tests.</summary>
    public static void Clear()
    {
        configuration = null;
    }
}
