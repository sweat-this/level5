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
    private static Func<int, string> devicePreflightOverride;

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
    /// leaves the attempt outstanding for retry), an unknown, non-selectable or locked level - and a
    /// locked or missing character - is known without spending anything on the attempt, so it must
    /// never reach <c>IssueAttempt</c> at all.
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

        // Same reasoning as the level check: a locked (or missing) character is known before anything
        // is spent on the attempt, and neither the builder nor anything downstream checks it - player
        // select is the only other place character unlock is enforced.
        if (character == null || character.IsEmpty || !unlock.IsCharacterUnlocked(character.CharacterId))
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable,
                "that character is not unlocked, so the turn was not started"));
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
    /// Starts one simultaneous game: both series participants, two local humans, one match.
    ///
    /// The simultaneous counterpart of <see cref="Launch"/>, which it leaves untouched. The roster is
    /// two <c>LocalHuman</c> slots in series order - slot 0 carries <c>Participants.First</c> and
    /// slot 1 carries <c>Participants.Second</c> - and the match is built through the same
    /// <see cref="MatchConfigurationBuilder"/> every launch path uses, so the ordinary checks
    /// (<see cref="ArenaCapability.Multiplayer"/>, character/mode compatibility, level lock state)
    /// are the gate rather than a parallel copy of them. Nothing here spawns a player or assigns a
    /// device: that stays with the gameplay scene, which composes the match from this roster.
    ///
    /// Everything that can be refused without spending anything is checked before the pair of
    /// attempts is issued - the series, the level, both characters, the connected devices, and the
    /// match itself, which is built first. Unlike the alternating path, a mode/arena that cannot be
    /// played therefore never consumes attempt state. The issue step is still idempotent, so a retry
    /// after a failed save or a crash before the scene loaded gets the same two attempts back.
    /// </summary>
    public static VersusLaunch LaunchSimultaneous(
        SeriesId seriesId,
        CharacterSelection firstCharacter,
        CharacterSelection secondCharacter,
        int levelId,
        UnlockSnapshot unlock,
        MatchModifiers modifiers = null)
    {
        if (unlock == null)
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable,
                "no local unlock snapshot was provided - refusing to start a game without a level eligibility check"));
        }

        VersusMatchCoordinator coordinator = VersusRuntime.Coordinator;

        VersusSeries loaded = coordinator.Load(seriesId);
        if (loaded == null)
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotFound,
                $"there is no series '{seriesId}'"));
        }

        if (!loaded.CanIssueSimultaneousAttempts(out string unavailable))
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                loaded.IsActive ? VersusValidationCode.AttemptNotAvailable : VersusValidationCode.SeriesNotPlayable,
                unavailable));
        }

        LevelDefinition level = MatchCatalogs.Levels.Find(levelId);
        ValidationResult levelValidation = LevelEligibility.ValidateForLaunch(level, levelId, unlock);
        if (!levelValidation.IsValid)
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, levelValidation.ToString()));
        }

        // Neither the builder nor anything downstream checks character unlock, so both sides are
        // checked here, before anything is spent.
        if (!CharacterIsUsable(firstCharacter, unlock) || !CharacterIsUsable(secondCharacter, unlock))
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable,
                "a chosen character is not unlocked, so the game was not started"));
        }

        // Hardware state, which the pure Core rules must not read - the same Unity-side preflight
        // StartManager runs before it launches a two-human match.
        if (!TryPreflightDevices(SimultaneousHumanCount, out string deviceFailure))
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, deviceFailure));
        }

        CompetitiveRuleset ruleset = loaded.CurrentGame.Ruleset;
        MatchConfiguration configuration = BuildSimultaneousMatch(
            ruleset,
            levelId,
            loaded.Participants.First.Id,
            firstCharacter,
            loaded.Participants.Second.Id,
            secondCharacter,
            unlock,
            modifiers);
        if (configuration == null)
        {
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable,
                $"{ruleset.DisplayName} cannot be played by two local players on the chosen arena"));
        }

        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(seriesId);
        if (!issued.Succeeded)
        {
            return VersusLaunch.Failure(issued.Validation);
        }

        VersusSeries series = issued.Series;
        Attempt first = issued.Attempts.First;
        Attempt second = issued.Attempts.Second;

        ActiveMatch.Begin(configuration);

        // Tied to this configuration, like the single-attempt context: abandoning this match to the
        // menu must not let the next ordinary match be submitted as this game.
        ActiveVersusAttempt.BeginSimultaneous(seriesId, first, second, configuration);

        LegacyGameOptionsBridge.Apply(configuration);

        SimultaneousAttemptOperation started = coordinator.StartSimultaneousAttempts(seriesId, first.Id, second.Id);
        if (!started.Succeeded)
        {
            // Not fatal, as for a single attempt: both attempts are issued and durable, and a Ready
            // attempt can be completed just as a Started one can.
            Debug.LogWarning($"Could not record that simultaneous game attempts started: {started.Validation}");
        }

        (sceneLoaderOverride ?? SceneTransition.LoadScene)(configuration.SceneName);
        return VersusLaunch.SuccessSimultaneous(series, first, second, configuration);
    }

    /// <summary>Two local humans, which is the whole of what a simultaneous launch supports.</summary>
    public const int SimultaneousHumanCount = 2;

    /// <summary>
    /// Builds the two-human match for a simultaneous game without launching it. Slot 0 is the first
    /// participant and slot 1 the second, each carrying their own <see cref="ParticipantId"/>; the
    /// order is the identity mapping the rest of the runtime relies on. Returns null when the builder
    /// refuses it (and logs why), exactly as <see cref="BuildMatch"/> does.
    /// </summary>
    public static MatchConfiguration BuildSimultaneousMatch(
        CompetitiveRuleset ruleset,
        int levelId,
        ParticipantId firstParticipantId,
        CharacterSelection firstCharacter,
        ParticipantId secondParticipantId,
        CharacterSelection secondCharacter,
        UnlockSnapshot unlock,
        MatchModifiers modifiers = null)
    {
        if (ruleset == null)
        {
            return null;
        }

        if (unlock == null)
        {
            Debug.LogWarning(
                $"A simultaneous versus game at {ruleset.DisplayName} could not be launched on level {levelId}: "
                + "no local unlock snapshot was provided - refusing to build a match without a level eligibility check");
            return null;
        }

        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            new PlayerRosterEntry(
                PlayerControlType.LocalHuman,
                firstCharacter ?? CharacterSelection.None,
                firstParticipantId.Value),
            new PlayerRosterEntry(
                PlayerControlType.LocalHuman,
                secondCharacter ?? CharacterSelection.None,
                secondParticipantId.Value)
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
            $"A simultaneous versus game at {ruleset.DisplayName} could not be launched on level {levelId}: "
            + result.Validation);
        return null;
    }

    private static bool CharacterIsUsable(CharacterSelection character, UnlockSnapshot unlock)
    {
        return character != null && !character.IsEmpty && unlock.IsCharacterUnlocked(character.CharacterId);
    }

    private static bool TryPreflightDevices(int localHumanCount, out string failureReason)
    {
        if (devicePreflightOverride != null)
        {
            failureReason = devicePreflightOverride(localHumanCount);
            return string.IsNullOrEmpty(failureReason);
        }

        return PlayerControlsProvider.TryPreflightGameplayDevices(localHumanCount, out failureReason);
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
        if (ruleset == null)
        {
            return null;
        }

        if (unlock == null)
        {
            // Unlike ruleset (an internal, always-supplied value), unlock is a caller-supplied
            // parameter a direct BuildMatch caller could plausibly forget - so this failure gets the
            // same diagnostic every other rejection below gets, rather than a silent null.
            Debug.LogWarning(
                $"A versus attempt at {ruleset.DisplayName} could not be launched on level {levelId}: "
                + "no local unlock snapshot was provided - refusing to build a match without a level eligibility check");
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

    /// <summary>
    /// Test-only seam for the connected-device check a simultaneous launch runs. The real one reads
    /// whatever the Input System reports, which an EditMode test machine cannot be relied on to have.
    /// The override returns a failure reason, or null when the devices can seat the humans.
    /// </summary>
    public static void OverrideDevicePreflight(Func<int, string> preflight)
    {
        devicePreflightOverride = preflight;
    }

    public static void ResetDevicePreflight()
    {
        devicePreflightOverride = null;
    }
}

/// <summary>The outcome of trying to start a competitive attempt.</summary>
public readonly struct VersusLaunch
{
    private VersusLaunch(
        VersusSeries series,
        Attempt attempt,
        Attempt secondAttempt,
        MatchConfiguration configuration,
        VersusValidationResult validation)
    {
        Series = series;
        Attempt = attempt;
        SecondAttempt = secondAttempt;
        Configuration = configuration;
        Validation = validation;
    }

    public VersusSeries Series { get; }

    /// <summary>The attempt launched; for a simultaneous game, the first participant's (roster slot 0).</summary>
    public Attempt Attempt { get; }

    /// <summary>The second participant's attempt (roster slot 1) of a simultaneous game; null otherwise.</summary>
    public Attempt SecondAttempt { get; }

    public MatchConfiguration Configuration { get; }

    public VersusValidationResult Validation { get; }

    public bool Succeeded => Attempt != null;

    public static VersusLaunch Success(VersusSeries series, Attempt attempt, MatchConfiguration configuration)
    {
        return new VersusLaunch(series, attempt, null, configuration, VersusValidationResult.Valid());
    }

    public static VersusLaunch SuccessSimultaneous(
        VersusSeries series,
        Attempt first,
        Attempt second,
        MatchConfiguration configuration)
    {
        return new VersusLaunch(series, first, second, configuration, VersusValidationResult.Valid());
    }

    public static VersusLaunch Failure(VersusValidationResult validation)
    {
        return new VersusLaunch(null, null, null, null, validation);
    }
}
