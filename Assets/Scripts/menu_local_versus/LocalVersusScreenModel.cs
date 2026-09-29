using System;
using System.Collections.Generic;
using System.Text;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;
using Level5.Core.Versus;

/// <summary>
/// What the Local Versus screen is currently looking at, and the strings it shows for it.
///
/// This holds <em>selection</em> state only - which ruleset/format the create form is on, which
/// stored series is highlighted, which character and arena the next turn will use. It holds no
/// competitive state: scores, whose turn it is and whether a series is over are read from the loaded
/// <see cref="VersusSeries"/> every time, so the screen cannot drift from the stored series and a
/// restart between turns loses nothing.
///
/// Plain C# so the whole flow is testable without a scene. <see cref="LocalVersusController"/> is the
/// thin uGUI binding over it.
/// </summary>
public sealed class LocalVersusScreenModel
{
    private readonly Func<UnlockSnapshot> unlockProvider;
    private readonly Func<UnlockSnapshot, IReadOnlyList<CharacterSelectOption>> characterProvider;

    private List<CompetitiveRuleset> rulesets = new List<CompetitiveRuleset>();
    private List<SeriesSummary> summaries = new List<SeriesSummary>();
    private List<CharacterSelectOption> characters = new List<CharacterSelectOption>();
    private List<LevelDefinition> levels = new List<LevelDefinition>();
    private int rulesetIndex;
    private int formatIndex = 1;
    private int seriesIndex = -1;
    private int characterIndex;
    private int levelIndex;
    private ParticipantId characterFor;

    /// <param name="unlockProvider">Builds the current account's snapshot (see <c>UnlockSnapshotBuilder</c>).</param>
    /// <param name="characterProvider">Projects the loaded profiles into selectable characters.</param>
    public LocalVersusScreenModel(
        Func<UnlockSnapshot> unlockProvider,
        Func<UnlockSnapshot, IReadOnlyList<CharacterSelectOption>> characterProvider)
    {
        this.unlockProvider = unlockProvider ?? throw new ArgumentNullException(nameof(unlockProvider));
        this.characterProvider = characterProvider ?? throw new ArgumentNullException(nameof(characterProvider));
    }

    public IReadOnlyList<CompetitiveRuleset> Rulesets => rulesets;

    public IReadOnlyList<SeriesSummary> Summaries => summaries;

    public IReadOnlyList<CharacterSelectOption> Characters => characters;

    public IReadOnlyList<LevelDefinition> Levels => levels;

    public CompetitiveRuleset SelectedRuleset => rulesetIndex >= 0 && rulesetIndex < rulesets.Count ? rulesets[rulesetIndex] : null;

    public int SelectedGameCount => LocalVersusFlow.OfferedGameCounts[formatIndex];

    public SeriesSummary SelectedSummary => seriesIndex >= 0 && seriesIndex < summaries.Count ? summaries[seriesIndex] : null;

    /// <summary>The loaded series behind <see cref="SelectedSummary"/>; null when none is selected.</summary>
    public VersusSeries SelectedSeries { get; private set; }

    /// <summary>Whoever the loaded series says may attempt now, or none.</summary>
    public ParticipantId NextParticipant { get; private set; }

    public CharacterSelectOption SelectedCharacter => characterIndex >= 0 && characterIndex < characters.Count ? characters[characterIndex] : null;

    public LevelDefinition SelectedLevel => levelIndex >= 0 && levelIndex < levels.Count ? levels[levelIndex] : null;

    /// <summary>The last create/launch outcome, for the screen to show.</summary>
    public string CreateMessage { get; private set; } = string.Empty;

    /// <summary>The last launch outcome, for the screen to show under the turn controls.</summary>
    public string TurnMessage { get; private set; } = string.Empty;

    public bool CanPlayTurn => SelectedSeries != null
        && NextParticipant.HasValue
        && SelectedCharacter != null
        && SelectedLevel != null;

    // ------------------------------------------------------------------ opening

    /// <summary>
    /// Enters the screen: lists the stored series, opens on the preferred one if a turn just ended
    /// (consuming the transient navigation hint), and otherwise on the first unfinished one.
    /// </summary>
    public void Open()
    {
        rulesets = LocalVersusFlow.SelectableRulesets();
        rulesetIndex = 0;

        SeriesId preferred = LocalVersusNavigationState.Consume();
        Refresh(preferred);
    }

    /// <summary>Re-reads the stored list and the selected series. <paramref name="prefer"/> wins when it exists.</summary>
    public void Refresh(SeriesId prefer = default)
    {
        SeriesId keep = prefer.HasValue ? prefer : SelectedSummary != null ? SelectedSummary.Id : default;

        summaries = LocalVersusFlow.ListLocal(VersusRuntime.Coordinator);
        seriesIndex = summaries.Count == 0 ? -1 : 0;
        for (int index = 0; index < summaries.Count; index++)
        {
            if (keep.HasValue && summaries[index].Id.Equals(keep))
            {
                seriesIndex = index;
                break;
            }
        }

        LoadSelected();
    }

    // ------------------------------------------------------------------ create form

    public void CycleRuleset()
    {
        if (rulesets.Count > 0)
        {
            rulesetIndex = (rulesetIndex + 1) % rulesets.Count;
        }
    }

    public void CycleFormat()
    {
        formatIndex = (formatIndex + 1) % LocalVersusFlow.OfferedGameCounts.Length;
    }

    /// <summary>Creates a series through the coordinator and, on success, selects it.</summary>
    public SeriesOperation Create(string firstName, string secondName)
    {
        CompetitiveRuleset ruleset = SelectedRuleset;
        if (ruleset == null)
        {
            CreateMessage = "No ruleset supports local alternating play.";
            return SeriesOperation.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, CreateMessage));
        }

        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator,
            string.IsNullOrWhiteSpace(firstName) ? "Player 1" : firstName.Trim(),
            string.IsNullOrWhiteSpace(secondName) ? "Player 2" : secondName.Trim(),
            SelectedGameCount,
            ruleset.Id);

        if (!created.Succeeded)
        {
            // Exactly what the coordinator said; nothing was stored, so nothing is selected.
            CreateMessage = "Could not create the series: " + created.Validation;
            return created;
        }

        CreateMessage = "Series created.";
        Refresh(created.Series.Id);
        return created;
    }

    // ------------------------------------------------------------------ series + turn selection

    public void CycleSeries()
    {
        if (summaries.Count == 0)
        {
            return;
        }

        seriesIndex = (seriesIndex + 1) % summaries.Count;
        LoadSelected();
    }

    public void Select(SeriesId id)
    {
        for (int index = 0; index < summaries.Count; index++)
        {
            if (summaries[index].Id.Equals(id))
            {
                seriesIndex = index;
                LoadSelected();
                return;
            }
        }
    }

    public void CycleCharacter()
    {
        if (characters.Count > 0)
        {
            characterIndex = (characterIndex + 1) % characters.Count;
        }
    }

    public void CycleLevel()
    {
        if (levels.Count > 0)
        {
            levelIndex = (levelIndex + 1) % levels.Count;
        }
    }

    /// <summary>
    /// Hands the selected turn to <see cref="VersusLauncher"/> (through <see cref="LocalVersusFlow"/>).
    /// The snapshot passed is the one the offered options were filtered with; the launcher
    /// revalidates it regardless.
    /// </summary>
    public VersusLaunch PlayTurn()
    {
        if (!CanPlayTurn)
        {
            TurnMessage = SelectedSeries == null ? "Select or create a series first."
                : !NextParticipant.HasValue ? "No turn is available in this series."
                : SelectedCharacter == null ? "No unlocked character is available."
                : "No eligible arena is available for this game.";
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, TurnMessage));
        }

        UnlockSnapshot unlock = unlockProvider();
        VersusLaunch launch = LocalVersusFlow.LaunchTurn(
            SelectedSeries.Id,
            NextParticipant,
            SelectedLevel.LevelId,
            SelectedCharacter.ToSelection(LegacyCharacterVariantResolver.ResolveObjectName(SelectedCharacter)),
            unlock);

        TurnMessage = launch.Succeeded ? string.Empty : "Could not start the turn: " + launch.Validation;
        return launch;
    }

    // ------------------------------------------------------------------ text

    public string RulesetText => SelectedRuleset == null ? "No ruleset" : "Ruleset: " + SelectedRuleset.DisplayName;

    public string FormatText => "Format: " + SeriesFormat.FromGameCount(SelectedGameCount);

    public string SeriesSelectText => summaries.Count == 0
        ? "No series yet"
        : $"Series {seriesIndex + 1} of {summaries.Count}: {DescribeRow(summaries[seriesIndex])}";

    public string CharacterText => SelectedCharacter == null ? "No character" : "Character: " + SelectedCharacter.DisplayName;

    public string LevelText => SelectedLevel == null ? "No arena" : "Arena: " + SelectedLevel.DisplayName;

    public string PlayTurnText
    {
        get
        {
            if (SelectedSeries == null || !NextParticipant.HasValue)
            {
                return "Play Turn";
            }

            return "Play Turn: " + SelectedSeries.Participants.Find(NextParticipant).DisplayName;
        }
    }

    /// <summary>Every stored local series as a list: unfinished under one heading, finished under another.</summary>
    public string ListText
    {
        get
        {
            if (summaries.Count == 0)
            {
                return "No local series yet. Create one to start.";
            }

            StringBuilder builder = new StringBuilder();
            bool wroteActive = false;
            bool wroteFinished = false;
            foreach (SeriesSummary summary in summaries)
            {
                bool unfinished = LocalVersusFlow.IsUnfinished(summary);
                if (unfinished && !wroteActive)
                {
                    builder.AppendLine("Active");
                    wroteActive = true;
                }
                else if (!unfinished && !wroteFinished)
                {
                    if (wroteActive)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine("Finished");
                    wroteFinished = true;
                }

                builder.Append(summary.Id.Equals(SelectedSummary?.Id ?? default) ? "> " : "  ")
                    .AppendLine(DescribeRow(summary));
            }

            return builder.ToString().TrimEnd();
        }
    }

    /// <summary>The selected series read from the loaded domain object.</summary>
    public string DetailText
    {
        get
        {
            VersusSeries series = SelectedSeries;
            if (series == null)
            {
                return summaries.Count == 0 ? string.Empty : "That series could not be loaded.";
            }

            string first = series.Participants.First.DisplayName;
            string second = series.Participants.Second.DisplayName;
            SeriesScore score = series.Score;
            StringBuilder builder = new StringBuilder();
            builder.Append(first).Append(' ').Append(score.FirstWins).Append(" - ")
                .Append(score.SecondWins).Append(' ').Append(second)
                .Append("   (").Append(series.Snapshot.Format).AppendLine(")");

            if (series.IsOver)
            {
                builder.Append(DescribeOutcome(series));
                return builder.ToString();
            }

            VersusGame game = series.CurrentGame;
            if (game != null)
            {
                builder.Append("Game ").Append(game.Number).Append(" of ").Append(series.Snapshot.Format.GameCount)
                    .Append(": ").AppendLine(game.Ruleset.DisplayName);
            }

            builder.Append(NextParticipant.HasValue
                ? series.Participants.Find(NextParticipant).DisplayName + " is up."
                : "No turn is available right now.");
            return builder.ToString();
        }
    }

    public static string DescribeRow(SeriesSummary summary)
    {
        string names = $"{summary.FirstDisplayName} {summary.FirstWins} - {summary.SecondWins} {summary.SecondDisplayName}";
        string format = summary.Format.ToString();
        if (LocalVersusFlow.IsUnfinished(summary))
        {
            return $"{names} | game {summary.CurrentGameNumber} | {format}";
        }

        return $"{names} | {summary.Status} | {format}";
    }

    private static string DescribeOutcome(VersusSeries series)
    {
        SeriesResult result = series.Result;
        if (result == null)
        {
            return series.Status.ToString();
        }

        switch (result.Kind)
        {
            case SeriesOutcomeKind.Decided:
                return series.Participants.Find(result.WinnerId).DisplayName + " wins the series.";
            case SeriesOutcomeKind.Forfeit:
                return "Forfeited" + (result.HasWinner
                    ? ": " + series.Participants.Find(result.WinnerId).DisplayName + " wins."
                    : ".");
            default:
                return "The series ended level.";
        }
    }

    // ------------------------------------------------------------------ internals

    private void LoadSelected()
    {
        SelectedSeries = null;
        NextParticipant = default;
        characters = new List<CharacterSelectOption>();
        levels = new List<LevelDefinition>();

        SeriesSummary summary = SelectedSummary;
        if (summary == null)
        {
            return;
        }

        // Only a selected series is hydrated in full; the list rows came from summaries.
        SelectedSeries = VersusRuntime.Coordinator.Load(summary.Id);
        if (SelectedSeries == null)
        {
            return;
        }

        NextParticipant = LocalVersusFlow.NextParticipant(SelectedSeries);
        if (!NextParticipant.HasValue)
        {
            return;
        }

        UnlockSnapshot unlock = unlockProvider();
        CompetitiveRuleset ruleset = SelectedSeries.CurrentGame.Ruleset;

        levels = LocalVersusFlow.EligibleLevels(ruleset, unlock);
        levelIndex = 0;

        GameModeDefinition mode = MatchCatalogs.Modes.Find(ruleset.ModeId);
        IReadOnlyList<CharacterSelectOption> options = characterProvider(unlock);
        if (options != null)
        {
            foreach (CharacterSelectOption option in options)
            {
                if (option != null
                    && option.IsUnlocked
                    && GameModeCompatibility.CharacterCanPlay(mode, MatchModifiers.Default, option.ToSelection()))
                {
                    characters.Add(option);
                }
            }
        }

        // The character belongs to the turn, not the series: a new participant starts from the
        // remembered primary (if eligible) rather than inheriting the previous side's pick.
        if (!NextParticipant.Equals(characterFor))
        {
            characterIndex = 0;
            int? remembered = PlayerSelectionSession.PrimaryCharacterId;
            if (remembered.HasValue)
            {
                for (int index = 0; index < characters.Count; index++)
                {
                    if (characters[index].CharacterId == remembered.Value)
                    {
                        characterIndex = index;
                        break;
                    }
                }
            }

            characterFor = NextParticipant;
        }
        else if (characterIndex >= characters.Count)
        {
            characterIndex = 0;
        }
    }
}
