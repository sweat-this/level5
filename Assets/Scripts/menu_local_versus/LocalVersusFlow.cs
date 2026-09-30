using System;
using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Progression;
using Level5.Core.Versus;

/// <summary>
/// The composition seam behind the production Local Versus screen.
///
/// Everything here delegates: series creation to <see cref="VersusMatchCoordinator"/>, whose turn it
/// is to <see cref="VersusSeries.CanIssueAttempt"/>, level legality to <see cref="LevelEligibility"/>,
/// and the launch itself to <see cref="VersusLauncher"/>. It keeps no series, no score and no turn
/// index - a screen calls these with whatever it just loaded, so a series restored from disk behaves
/// exactly like one created a moment ago.
///
/// Two kinds of local series are exposed. <see cref="VersusMode.LocalAlternating"/> is the default
/// everywhere a mode is not named: one participant plays per turn, through
/// <see cref="VersusLauncher.Launch"/>. <see cref="VersusMode.LocalSimultaneous"/> has no turns -
/// both participants play one two-human match, through <see cref="VersusLauncher.LaunchSimultaneous"/>
/// - and only rulesets that declare that capability can be created as one (today, Most Points).
/// </summary>
public static class LocalVersusFlow
{
    /// <summary>Diagnostic label on the request; never a behavioral authority.</summary>
    public const string RequestSource = "local versus UI";

    /// <summary>The default kind of local series: the one every call that does not name a mode means.</summary>
    public const VersusMode Mode = VersusMode.LocalAlternating;

    /// <summary>The local kinds of series the screen can create, in the order its mode button cycles them.</summary>
    public static readonly VersusMode[] OfferedModes = { VersusMode.LocalAlternating, VersusMode.LocalSimultaneous };

    /// <summary>Fixed semantics of a production local series.</summary>
    public const InformationPolicy Policy = InformationPolicy.SealedAttempt;

    public const bool RequiresInvitation = false;

    public const bool AlternatesFirstAttempt = true;

    /// <summary>The formats the screen offers, ascending. Which lengths are valid stays with <see cref="SeriesFormat"/>.</summary>
    public static readonly int[] OfferedGameCounts = { 1, 3, 5, 7 };

    /// <summary>
    /// Rulesets that can be played as <paramref name="mode"/> (alternating unless named), in catalog
    /// order, filtered through <see cref="VersusCapability"/> so a ruleset never offered the
    /// capability is never offered the mode.
    /// </summary>
    public static List<CompetitiveRuleset> SelectableRulesets(
        CompetitiveRulesetCatalog catalog = null,
        VersusMode mode = Mode)
    {
        return (catalog ?? VersusCatalogs.Rulesets).Supporting(VersusModes.RequiredCapability(mode));
    }

    public static bool IsSimultaneous(VersusMode mode)
    {
        return mode == VersusMode.LocalSimultaneous;
    }

    /// <summary>
    /// A fresh, opaque id for one side of one series. Deliberately unrelated to any account, profile
    /// or backend identity: a participant is a side of this competition, not a person.
    /// </summary>
    public static ParticipantId NewParticipantId()
    {
        return new ParticipantId(Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Builds the request for a production local series: two new participants, the chosen ruleset
    /// repeated for every game, and the fixed local-alternating semantics.
    /// </summary>
    public static SeriesRequest BuildRequest(
        string firstDisplayName,
        string secondDisplayName,
        int gameCount,
        RulesetId rulesetId,
        VersusMode mode = Mode)
    {
        SeriesFormat format = SeriesFormat.FromGameCount(gameCount);

        List<RulesetId> playlist = new List<RulesetId>(format.GameCount);
        for (int index = 0; index < format.GameCount; index++)
        {
            playlist.Add(rulesetId);
        }

        return new SeriesRequest(
            new MatchParticipant(NewParticipantId(), firstDisplayName),
            new MatchParticipant(NewParticipantId(), secondDisplayName),
            format,
            playlist,
            mode,
            Policy,
            RequiresInvitation,
            AlternatesFirstAttempt,
            RequestSource);
    }

    /// <summary>Creates a local series through the coordinator; the caller renders whatever it says.</summary>
    public static SeriesOperation CreateSeries(
        VersusMatchCoordinator coordinator,
        string firstDisplayName,
        string secondDisplayName,
        int gameCount,
        RulesetId rulesetId,
        VersusMode mode = Mode)
    {
        SeriesRequest request;
        try
        {
            request = BuildRequest(firstDisplayName, secondDisplayName, gameCount, rulesetId, mode);
        }
        catch (VersusDomainException exception)
        {
            return SeriesOperation.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, exception.Message));
        }

        return coordinator.CreateSeries(request);
    }

    /// <summary>
    /// The stored local series without loading any of them: unfinished series first, then finished
    /// ones, newest first within each. Archived series and the non-local modes (correspondence) are
    /// not this screen's.
    /// </summary>
    public static List<SeriesSummary> ListLocal(VersusMatchCoordinator coordinator)
    {
        List<SeriesSummary> local = new List<SeriesSummary>();
        foreach (SeriesSummary summary in coordinator.ListSeries())
        {
            if (Array.IndexOf(OfferedModes, summary.Mode) >= 0 && !summary.Archived)
            {
                local.Add(summary);
            }
        }

        local.Sort((left, right) =>
        {
            int byUnfinished = IsUnfinished(right).CompareTo(IsUnfinished(left));
            return byUnfinished != 0 ? byUnfinished : right.CreatedAtUtc.CompareTo(left.CreatedAtUtc);
        });
        return local;
    }

    public static bool IsUnfinished(SeriesSummary summary)
    {
        return summary.Status == SeriesStatus.Active || summary.Status == SeriesStatus.Invited;
    }

    /// <summary>
    /// Whoever the loaded series says can attempt right now, preferring the side the current game
    /// designates first, then the other. None when nobody can.
    /// </summary>
    public static ParticipantId NextParticipant(VersusSeries series)
    {
        VersusGame game = series?.CurrentGame;
        if (game == null || series.Mode == VersusMode.LocalSimultaneous)
        {
            // A simultaneous series has no turn order: both participants play every game together.
            return default;
        }

        ParticipantId first = series.Participants.At(game.FirstAttemptParticipantIndex).Id;
        if (series.CanIssueAttempt(first, out _))
        {
            return first;
        }

        ParticipantId second = series.Participants.Opponent(first).Id;
        return series.CanIssueAttempt(second, out _) ? second : default;
    }

    /// <summary>
    /// The arenas offered for a game's frozen ruleset: known, selectable, unlocked and compatible
    /// with the ruleset's mode. Filtering here means the normal path never reaches the launcher's
    /// issue-then-fail mode/arena behavior; the launcher still revalidates authoritatively.
    /// </summary>
    public static List<LevelDefinition> EligibleLevels(CompetitiveRuleset ruleset, UnlockSnapshot unlock)
    {
        return EligibleLevels(ruleset, unlock, requiresMultiplayerArena: false);
    }

    /// <summary>
    /// As above; <paramref name="requiresMultiplayerArena"/> additionally keeps only arenas that
    /// declare <see cref="ArenaCapability.Multiplayer"/>, which is what a two-human game needs. It is
    /// the same capability <see cref="MatchConfigurationBuilder"/> enforces for a two-human roster, read
    /// here only so the screen does not offer an arena the builder would then refuse.
    /// </summary>
    public static List<LevelDefinition> EligibleLevels(
        CompetitiveRuleset ruleset,
        UnlockSnapshot unlock,
        bool requiresMultiplayerArena)
    {
        List<LevelDefinition> eligible = new List<LevelDefinition>();
        if (ruleset == null || unlock == null)
        {
            return eligible;
        }

        GameModeDefinition mode = MatchCatalogs.Modes.Find(ruleset.ModeId);
        if (mode == null)
        {
            return eligible;
        }

        foreach (LevelDefinition level in MatchCatalogs.Levels.Definitions)
        {
            if (requiresMultiplayerArena && !level.Supports(ArenaCapability.Multiplayer))
            {
                continue;
            }

            if (LevelEligibility.CanSelect(level, mode, MatchCatalogs.Compatibility, unlock))
            {
                eligible.Add(level);
            }
        }

        return eligible;
    }

    /// <summary>
    /// Launches one participant's turn through <see cref="VersusLauncher"/> and nothing else: no
    /// attempt is issued or started here, no <c>GameOptions</c> written, no scene loaded.
    ///
    /// The transient return hint is set immediately before the launcher's scene transition and
    /// cleared again if the launch fails, so a failed turn cannot leave the screen believing a match
    /// is running.
    /// </summary>
    public static VersusLaunch LaunchTurn(
        SeriesId seriesId,
        ParticipantId participantId,
        int levelId,
        CharacterSelection character,
        UnlockSnapshot unlock,
        MatchModifiers modifiers = null)
    {
        LocalVersusNavigationState.Begin(seriesId);

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            participantId,
            levelId,
            character,
            unlock,
            modifiers ?? MatchModifiers.Default);

        if (launch.Succeeded)
        {
            LocalVersusNavigationState.Bind(launch.Configuration);
        }
        else
        {
            LocalVersusNavigationState.Clear();
        }

        return launch;
    }

    /// <summary>
    /// Launches one simultaneous game through <see cref="VersusLauncher.LaunchSimultaneous"/> and
    /// nothing else, with the same return-hint handling as <see cref="LaunchTurn"/>: set just before
    /// the launcher's scene transition, cleared again if the launch fails.
    /// </summary>
    public static VersusLaunch LaunchGame(
        SeriesId seriesId,
        CharacterSelection firstCharacter,
        CharacterSelection secondCharacter,
        int levelId,
        UnlockSnapshot unlock,
        MatchModifiers modifiers = null)
    {
        LocalVersusNavigationState.Begin(seriesId);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            seriesId,
            firstCharacter,
            secondCharacter,
            levelId,
            unlock,
            modifiers ?? MatchModifiers.Default);

        if (launch.Succeeded)
        {
            LocalVersusNavigationState.Bind(launch.Configuration);
        }
        else
        {
            LocalVersusNavigationState.Clear();
        }

        return launch;
    }
}
