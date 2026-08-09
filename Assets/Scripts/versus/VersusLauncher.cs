using Assets.Scripts.Utility;
using Level5.Core.Match;
using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// Starts the gameplay match for one competitive attempt.
///
/// This is the join between the two domains, and the direction of the join matters: it reads the
/// series to find out which mode to play, then builds an ordinary <c>MatchRequest</c> and launches
/// it through the same builder, the same validation and the same bridge every other launch path
/// uses. The gameplay scene is handed a normal match and is never told that a series exists.
///
/// The mode comes from the series' frozen ruleset rather than from the catalog, so a mid-series
/// balance patch cannot change which mode game five turns out to be.
///
/// The roster is one local human. An attempt is one participant's run, whether the opponent is
/// sitting next to them or answering on Thursday - which is exactly why the same code covers local
/// alternating play and correspondence with nothing switching between them.
/// </summary>
public static class VersusLauncher
{
    /// <summary>
    /// Issues the participant's attempt and loads the match for it.
    ///
    /// The attempt is issued and saved <em>before</em> the scene loads. If the application dies
    /// during the load, the turn is already outstanding in the stored series and is handed back on
    /// the next request rather than lost.
    /// </summary>
    public static VersusLaunch Launch(
        SeriesId seriesId,
        ParticipantId participantId,
        int levelId,
        CharacterSelection character,
        MatchModifiers modifiers = null)
    {
        VersusMatchCoordinator coordinator = VersusRuntime.Coordinator;

        AttemptOperation issued = coordinator.IssueAttempt(seriesId, participantId);
        if (!issued.Succeeded)
        {
            return VersusLaunch.Failure(issued.Validation);
        }

        VersusSeries series = issued.Series;
        Attempt attempt = issued.Attempt;
        CompetitiveRuleset ruleset = series.Snapshot.GameAt(attempt.GameIndex);

        MatchConfiguration configuration = BuildMatch(ruleset, levelId, participantId, character, modifiers);
        if (configuration == null)
        {
            // The attempt stays outstanding on purpose. It is a legitimate turn that could not be
            // played on this arena, and abandoning it here would cost the participant their go for
            // a reason that has nothing to do with them.
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable,
                $"{ruleset.DisplayName} cannot be played on the chosen arena"));
        }

        ActiveMatch.Begin(configuration);

        // Tied to this configuration, so that a player who abandons this match to the menu and then
        // plays an ordinary one does not have that match submitted as their turn.
        ActiveVersusAttempt.Begin(seriesId, attempt, configuration);

        // The same one-way push every other launch path does, for the consumers still reading the
        // old globals.
        LegacyGameOptionsBridge.Apply(configuration);

        coordinator.StartAttempt(seriesId, attempt.Id);

        SceneTransition.LoadScene(configuration.SceneName);
        return VersusLaunch.Success(series, attempt, configuration);
    }

    /// <summary>
    /// Builds the match for a ruleset without launching it.
    ///
    /// Separate so a screen can find out whether a turn is playable on a given arena before
    /// offering it, and so tests can check the join without loading a scene. Returns null when the
    /// combination is refused; the reason is logged by the builder's own validation.
    /// </summary>
    public static MatchConfiguration BuildMatch(
        CompetitiveRuleset ruleset,
        int levelId,
        ParticipantId participantId,
        CharacterSelection character,
        MatchModifiers modifiers = null)
    {
        if (ruleset == null)
        {
            return null;
        }

        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            new PlayerRosterEntry(
                PlayerControlType.LocalHuman,
                character ?? CharacterSelection.None,
                participantId.Value)
        });

        MatchRequest request = new MatchRequest(
            ruleset.ModeId,
            levelId,
            roster,
            modifiers ?? MatchModifiers.Default,
            CheerleaderSelection.None,
            "versus series");

        MatchBuildResult result = MatchCatalogs.Builder.Build(request);
        if (result.Succeeded)
        {
            return result.Configuration;
        }

        Debug.LogWarning(
            $"A versus attempt at {ruleset.DisplayName} could not be launched on level {levelId}: "
            + result.Validation);
        return null;
    }
}

/// <summary>The outcome of trying to start a competitive attempt.</summary>
public readonly struct VersusLaunch
{
    private VersusLaunch(
        VersusSeries series,
        Attempt attempt,
        MatchConfiguration configuration,
        VersusValidationResult validation)
    {
        Series = series;
        Attempt = attempt;
        Configuration = configuration;
        Validation = validation;
    }

    public VersusSeries Series { get; }

    public Attempt Attempt { get; }

    public MatchConfiguration Configuration { get; }

    public VersusValidationResult Validation { get; }

    public bool Succeeded => Attempt != null;

    public static VersusLaunch Success(VersusSeries series, Attempt attempt, MatchConfiguration configuration)
    {
        return new VersusLaunch(series, attempt, configuration, VersusValidationResult.Valid());
    }

    public static VersusLaunch Failure(VersusValidationResult validation)
    {
        return new VersusLaunch(null, null, null, validation);
    }
}
