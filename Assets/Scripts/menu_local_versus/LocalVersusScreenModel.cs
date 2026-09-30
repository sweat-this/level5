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
    /// <summary>
    /// How many lines of the series list fit the scene's list box (<c>seriesList</c> in
    /// <c>level_00_local_versus</c>: 28pt text in a 210px box). Measured against the rendered screen: even a finished Best-of-7 row between two
    /// 16-character names of the widest glyphs stays on one line (about 23px to spare), so a row is one line.
    /// The opt-in layout fixture (<c>LocalVersusProcessCertificationPlayModeTests</c>) re-measures this; change
    /// the list box, its font size or the name limit and it must be rerun.
    /// </summary>
    public const int ListLineBudget = 7;

    private readonly Func<UnlockSnapshot> unlockProvider;
    private readonly Func<UnlockSnapshot, IReadOnlyList<CharacterSelectOption>> characterProvider;

    private List<CompetitiveRuleset> rulesets = new List<CompetitiveRuleset>();
    private List<SeriesSummary> summaries = new List<SeriesSummary>();
    private List<CharacterSelectOption> characters = new List<CharacterSelectOption>();
    private List<LevelDefinition> levels = new List<LevelDefinition>();
    private int rulesetIndex;
    private int modeIndex;
    private int formatIndex = 1;
    private int seriesIndex = -1;
    private int characterIndex;
    private int secondCharacterIndex;
    private int levelIndex;
    private ParticipantId characterFor;
    private SeriesId secondCharacterFor;

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

    /// <summary>Which kind of series the create form is set to make. Not the selected series' mode.</summary>
    public VersusMode CreateMode => LocalVersusFlow.OfferedModes[modeIndex];

    /// <summary>
    /// True when the loaded series is a simultaneous one: no next participant, two character
    /// selections, and a game to play rather than a turn.
    /// </summary>
    public bool IsSimultaneousSelected => SelectedSeries != null
        && SelectedSeries.Mode == VersusMode.LocalSimultaneous;

    /// <summary>
    /// Player 2's character for a simultaneous game. <see cref="SelectedCharacter"/> is Player 1's
    /// (roster slot 0, the series' first participant); this is slot 1, the second participant's.
    /// </summary>
    public CharacterSelectOption SelectedSecondCharacter => secondCharacterIndex >= 0 && secondCharacterIndex < characters.Count
        ? characters[secondCharacterIndex]
        : null;

    public LevelDefinition SelectedLevel => levelIndex >= 0 && levelIndex < levels.Count ? levels[levelIndex] : null;

    /// <summary>The last create/launch outcome, for the screen to show.</summary>
    public string CreateMessage { get; private set; } = string.Empty;

    /// <summary>The last launch outcome, for the screen to show under the turn controls.</summary>
    public string TurnMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the selected series can be played right now: a next participant with a character and
    /// an arena for alternating play, or both characters and an arena for a simultaneous game.
    /// </summary>
    public bool CanPlayTurn => IsSimultaneousSelected
        ? simultaneousPlayable && SelectedCharacter != null && SelectedSecondCharacter != null && SelectedLevel != null
        : SelectedSeries != null
            && NextParticipant.HasValue
            && SelectedCharacter != null
            && SelectedLevel != null;

    private bool simultaneousPlayable;

    /// <summary>
    /// Whether the selected series has something to play and choose for: a next participant for
    /// alternating play, a playable game for simultaneous play. Gates the character and arena selectors.
    /// </summary>
    public bool HasPlayableTurn => IsSimultaneousSelected ? simultaneousPlayable : NextParticipant.HasValue;

    // ------------------------------------------------------------------ opening

    /// <summary>
    /// Enters the screen: lists the stored series, opens on the preferred one if a turn just ended
    /// (consuming the transient navigation hint), and otherwise on the first unfinished one.
    /// </summary>
    public void Open()
    {
        rulesets = LocalVersusFlow.SelectableRulesets(mode: CreateMode);
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

    /// <summary>
    /// Switches the create form between alternating and simultaneous play. The offered rulesets are
    /// re-read through <see cref="VersusCapability"/> for the new mode, so the list only ever holds
    /// rulesets that can actually be created as it.
    /// </summary>
    public void CycleMode()
    {
        modeIndex = (modeIndex + 1) % LocalVersusFlow.OfferedModes.Length;
        rulesets = LocalVersusFlow.SelectableRulesets(mode: CreateMode);
        rulesetIndex = 0;
        CreateMessage = string.Empty;
    }

    /// <summary>Creates a series through the coordinator and, on success, selects it.</summary>
    public SeriesOperation Create(string firstName, string secondName)
    {
        CompetitiveRuleset ruleset = SelectedRuleset;
        if (ruleset == null)
        {
            CreateMessage = LocalVersusFlow.IsSimultaneous(CreateMode)
                ? "No ruleset supports local simultaneous play."
                : "No ruleset supports local alternating play.";
            return SeriesOperation.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, CreateMessage));
        }

        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator,
            string.IsNullOrWhiteSpace(firstName) ? "Player 1" : firstName.Trim(),
            string.IsNullOrWhiteSpace(secondName) ? "Player 2" : secondName.Trim(),
            SelectedGameCount,
            ruleset.Id,
            CreateMode);

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
            RememberSimultaneousPicks();
        }
    }

    /// <summary>Cycles Player 2's character for a simultaneous game.</summary>
    public void CycleSecondCharacter()
    {
        if (characters.Count > 0)
        {
            secondCharacterIndex = (secondCharacterIndex + 1) % characters.Count;
            RememberSimultaneousPicks();
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
                : IsSimultaneousSelected && !simultaneousPlayable ? "No game is available in this series."
                : !IsSimultaneousSelected && !NextParticipant.HasValue ? "No turn is available in this series."
                : SelectedCharacter == null || (IsSimultaneousSelected && SelectedSecondCharacter == null)
                    ? "No unlocked character is available."
                : IsSimultaneousSelected ? "No eligible arena is available for this two-player game."
                : "No eligible arena is available for this game.";
            return VersusLaunch.Failure(VersusValidationResult.Invalid(
                VersusValidationCode.SeriesNotPlayable, TurnMessage));
        }

        if (IsSimultaneousSelected)
        {
            return PlayGame();
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

    /// <summary>
    /// Launches the selected simultaneous game: Player 1's character (series first participant,
    /// roster slot 0) and Player 2's (second participant, slot 1). No participant is chosen here -
    /// there is no turn - and nothing but <see cref="LocalVersusFlow.LaunchGame"/> starts it.
    /// </summary>
    private VersusLaunch PlayGame()
    {
        RememberSimultaneousPicks();
        UnlockSnapshot unlock = unlockProvider();
        VersusLaunch launch = LocalVersusFlow.LaunchGame(
            SelectedSeries.Id,
            SelectedCharacter.ToSelection(LegacyCharacterVariantResolver.ResolveObjectName(SelectedCharacter)),
            SelectedSecondCharacter.ToSelection(LegacyCharacterVariantResolver.ResolveObjectName(SelectedSecondCharacter)),
            SelectedLevel.LevelId,
            unlock);

        TurnMessage = launch.Succeeded ? string.Empty : "Could not start the game: " + launch.Validation;
        return launch;
    }

    // ------------------------------------------------------------------ text

    public string RulesetText => SelectedRuleset == null ? "No ruleset" : "Ruleset: " + SelectedRuleset.DisplayName;

    public string FormatText => "Format: " + SeriesFormat.FromGameCount(SelectedGameCount);

    public string SeriesSelectText => summaries.Count == 0
        ? "No series yet"
        : $"Series {seriesIndex + 1} of {summaries.Count}: {DescribeRow(summaries[seriesIndex])}";

    public string ModeText => "Mode: " + (LocalVersusFlow.IsSimultaneous(CreateMode) ? "Local Simultaneous" : "Local Alternating");

    /// <summary>
    /// The character selector's label: "Character:" for alternating play (one selector, whoever is up),
    /// "Player 1:" when a simultaneous series is selected (this selector is slot 0's).
    /// </summary>
    public string CharacterText
    {
        get
        {
            string label = IsSimultaneousSelected
                ? "Player 1 (" + SelectedSeries.Participants.First.DisplayName + "): "
                : "Character: ";
            return SelectedCharacter == null ? "No character" : label + SelectedCharacter.DisplayName;
        }
    }

    /// <summary>Player 2's selector label. Only meaningful for a simultaneous series.</summary>
    public string SecondCharacterText
    {
        get
        {
            string label = IsSimultaneousSelected
                ? "Player 2 (" + SelectedSeries.Participants.Second.DisplayName + "): "
                : "Player 2: ";
            return SelectedSecondCharacter == null ? "No character" : label + SelectedSecondCharacter.DisplayName;
        }
    }

    public string LevelText => SelectedLevel == null ? "No arena" : "Arena: " + SelectedLevel.DisplayName;

    public string PlayTurnText
    {
        get
        {
            if (IsSimultaneousSelected)
            {
                return "Play Game";
            }

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

            // The scene's list box holds ListLineBudget lines. What does not fit is windowed around the
            // selected series rather than cut off, so the selection marker is always on screen and the
            // hidden rows are counted.
            int selected = Math.Max(seriesIndex, 0);
            int first = selected;
            int last = selected;
            bool grew = true;
            while (grew)
            {
                grew = false;
                if (last + 1 < summaries.Count && ListLines(first, last + 1) <= ListLineBudget)
                {
                    last++;
                    grew = true;
                }

                if (first > 0 && ListLines(first - 1, last) <= ListLineBudget)
                {
                    first--;
                    grew = true;
                }
            }

            StringBuilder builder = new StringBuilder();
            bool wroteActive = false;
            bool wroteFinished = false;
            if (first > 0)
            {
                builder.AppendLine("  ... " + first + " more above");
            }

            for (int index = first; index <= last; index++)
            {
                SeriesSummary summary = summaries[index];
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

            if (last < summaries.Count - 1)
            {
                builder.AppendLine("  ... " + (summaries.Count - 1 - last) + " more below");
            }

            return builder.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Lines <see cref="ListText"/> takes to show rows <paramref name="first"/>..<paramref name="last"/>:
    /// the rows, a heading per group present, the blank line between two groups, and one line for each
    /// side that has rows left out.
    /// </summary>
    private int ListLines(int first, int last)
    {
        bool active = false;
        bool finished = false;
        for (int index = first; index <= last; index++)
        {
            if (LocalVersusFlow.IsUnfinished(summaries[index]))
            {
                active = true;
            }
            else
            {
                finished = true;
            }
        }

        int lines = last - first + 1;
        lines += (active ? 1 : 0) + (finished ? 1 : 0) + (active && finished ? 1 : 0);
        lines += (first > 0 ? 1 : 0) + (last < summaries.Count - 1 ? 1 : 0);
        return lines;
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

            if (IsSimultaneousSelected)
            {
                builder.Append(simultaneousPlayable
                    ? "Both players play at once."
                    : "No game is available right now.");
                return builder.ToString();
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

        simultaneousPlayable = false;
        bool simultaneous = SelectedSeries.Mode == VersusMode.LocalSimultaneous;
        if (simultaneous)
        {
            // No next participant exists for a same-time series; what matters is whether both
            // participants can attempt the current game together.
            simultaneousPlayable = SelectedSeries.CanIssueSimultaneousAttempts(out _);
            if (!simultaneousPlayable)
            {
                return;
            }
        }
        else
        {
            NextParticipant = LocalVersusFlow.NextParticipant(SelectedSeries);
            if (!NextParticipant.HasValue)
            {
                return;
            }
        }

        UnlockSnapshot unlock = unlockProvider();
        CompetitiveRuleset ruleset = SelectedSeries.CurrentGame.Ruleset;

        // A two-human game is only offered arenas that declare Multiplayer; the builder would refuse
        // the others at launch.
        levels = LocalVersusFlow.EligibleLevels(ruleset, unlock, requiresMultiplayerArena: simultaneous);
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

        if (simultaneous)
        {
            SelectSimultaneousCharacters();
            return;
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
            secondCharacterFor = default;
        }
        else if (characterIndex >= characters.Count)
        {
            characterIndex = 0;
        }
    }

    /// <summary>
    /// Chooses the two characters a simultaneous game starts from. Both belong to the series rather
    /// than to a turn. For a series the model has not chosen for yet they are recalled from
    /// <see cref="LocalVersusSimultaneousPicks"/> - the screen is rebuilt after every game, so the model
    /// alone cannot carry them - and otherwise default: Player 1 to the remembered primary (if eligible),
    /// Player 2 to the next character along, so a fresh series is not two of the same one. After that they
    /// stay as the players cycled them.
    /// </summary>
    private void SelectSimultaneousCharacters()
    {
        if (!SelectedSeries.Id.Equals(secondCharacterFor))
        {
            characterIndex = 0;
            int? remembered = PlayerSelectionSession.PrimaryCharacterId;
            if (remembered.HasValue)
            {
                characterIndex = IndexOfCharacter(remembered.Value, characterIndex);
            }

            secondCharacterIndex = characters.Count > 1 ? (characterIndex + 1) % characters.Count : characterIndex;

            if (LocalVersusSimultaneousPicks.TryRecall(SelectedSeries.Id, out int firstId, out int secondId))
            {
                // A recalled character that is no longer offered (locked, or cannot play the mode) keeps
                // the default rather than failing.
                characterIndex = IndexOfCharacter(firstId, characterIndex);
                secondCharacterIndex = IndexOfCharacter(secondId, secondCharacterIndex);
            }

            secondCharacterFor = SelectedSeries.Id;
            characterFor = default;
            return;
        }

        if (characterIndex >= characters.Count)
        {
            characterIndex = 0;
        }

        if (secondCharacterIndex >= characters.Count)
        {
            secondCharacterIndex = 0;
        }
    }

    private int IndexOfCharacter(int characterId, int fallback)
    {
        for (int index = 0; index < characters.Count; index++)
        {
            if (characters[index].CharacterId == characterId)
            {
                return index;
            }
        }

        return fallback;
    }

    private void RememberSimultaneousPicks()
    {
        if (IsSimultaneousSelected && SelectedCharacter != null && SelectedSecondCharacter != null)
        {
            LocalVersusSimultaneousPicks.Remember(
                SelectedSeries.Id, SelectedCharacter.CharacterId, SelectedSecondCharacter.CharacterId);
        }
    }
}
