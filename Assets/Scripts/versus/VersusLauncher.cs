using System;
using Assets.Scripts.Utility;
using Level5.Core.Match;
using Level5.Core.Progression;
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
///
/// Takes the caller's current <see cref="UnlockSnapshot"/> (issue #203, following remote
/// correspondence's #198) and checks the local level against it twice: once here, before
/// <c>IssueAttempt</c>, so an unknown/non-selectable/locked level never consumes a competitive
/// attempt; and again inside <see cref="BuildMatch"/>, through the ordinary
/// <see cref="MatchConfigurationBuilder.Build"/> gate, so the final <see cref="MatchConfiguration"/>
/// is never produced any other way than every other launch path uses.
/// </summary>
public static class VersusLauncher
{
    private static Action<string> sceneLoaderOverride;

    /// <summary>
    /// Issues the participant's attempt and loads the match for it.
    ///
    /// The attempt is issued and saved <em>before</em> the scene loads. If the application dies
    /// during the load, the turn is already outstanding in the stored series and is handed back on
    /// the next request rather than lost.
    ///
    /// <paramref name="unlock"/> is required, not optional the way
    /// <see cref="MatchConfigurationBuilder.Build"/>'s own parameter is for unmigrated callers: this
    /// path must never silently fall back to permissive null-unlock behavior. The local level
    /// eligibility check runs before <c>IssueAttempt</c> - unlike an incompatible mode/arena
    /// combination (which is deliberately only caught afterward, at <see cref="BuildMatch"/>, and
    /// leaves the attempt outstanding for retry), an unknown, non-selectable or locked level is known
    /// without spending anything on the attempt, so it must never reach <c>IssueAttempt</c> at all.
    /// </summary>
    public static VersusLaunch Launch(
        SeriesId seriesId,
        ParticipantId participantId,
        int levelId,
        CharacterSelection character,
        UnlockSnapshot unlock,
        MatchModifiers modifiers = null)
    {
        if (unlock == null)
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable,
                "no local unlock snapshot was provided - refusing to start a turn without a level eligibility check"));
        }

        LevelDefinition level = MatchCatalogs.Levels.Find(levelId);
        ValidationResult levelValidation = LevelEligibility.ValidateForLaunch(level, levelId, unlock);
        if (!levelValidation.IsValid)
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, levelValidation.ToString()));
        }

        VersusMatchCoordinator coordinator = VersusRuntime.Coordinator;

        AttemptOperation issued = coordinator.IssueAttempt(seriesId, participantId);
        if (!issued.Succeeded)
        {
            return VersusLaunch.Failure(issued.Validation);
        }

        VersusSeries series = issued.Series;
        Attempt attempt = issued.Attempt;
        CompetitiveRuleset ruleset = series.Snapshot.GameAt(attempt.GameIndex);

        MatchConfiguration configuration = BuildMatch(ruleset, levelId, participantId, character, unlock, modifiers);
        if (configuration == null)
        {
            // The attempt stays outstanding on purpose. It is a legitimate turn that could not be
            // played on this arena, and abandoning it here would cost the participant their go for
            // a reason that has nothing to do with them. This is a mode/arena compatibility failure,
            // not a local-eligibility one - those are already refused above, before IssueAttempt.
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

        (sceneLoaderOverride ?? SceneTransition.LoadScene)(configuration.SceneName);
        return VersusLaunch.Success(series, attempt, configuration);
    }

    /// <summary>
    /// Builds the match for a ruleset without launching it.
    ///
    /// Separate so a screen can find out whether a turn is playable on a given arena before
    /// offering it, and so tests can check the join without loading a scene. Returns null when the
    /// combination is refused (including a missing <paramref name="unlock"/>, which fails closed
    /// rather than falling back to <see cref="MatchConfigurationBuilder"/>'s permissive
    /// unmigrated-caller default); the reason is logged by the builder's own validation.
    /// </summary>
    public static MatchConfiguration BuildMatch(
        CompetitiveRuleset ruleset,
        int levelId,
        ParticipantId participantId,
        CharacterSelection character,
        UnlockSnapshot unlock,
        MatchModifiers modifiers = null)
    {
        if (ruleset == null || unlock == null)
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

        MatchBuildResult result = MatchCatalogs.Builder.Build(request, unlock);
        if (result.Succeeded)
        {
            return result.Configuration;
        }

        Debug.LogWarning(
            $"A versus attempt at {ruleset.DisplayName} could not be launched on level {levelId}: "
            + result.Validation);
        return null;
    }

    /// <summary>Test-only seam so the success path can be exercised in EditMode without a real
    /// <c>SceneManager.LoadScene</c> call, matching <see cref="Level5.BackendV2.RemoteAttemptLauncher"/>'s
    /// <c>OverrideSceneLoader</c>/<c>ResetSceneLoader</c> convention.</summary>
    public static void OverrideSceneLoader(Action<string> loader)
    {
        sceneLoaderOverride = loader;
    }

    public static void ResetSceneLoader()
    {
        sceneLoaderOverride = null;
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
