using System.Collections.Generic;
using System.Text;
using Level5.Core.Match;
using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// Drives a whole versus series without any production UI.
///
/// There are no versus menus yet, and authoring them is not something this change can do. Rather
/// than let that block the architecture from being exercised, this component does what a menu will
/// eventually do: create a series, show whose turn it is, launch that turn, and print the state
/// after each one. Everything it calls is the same coordinator a real screen would call, so if this
/// works, a screen built on the same API works.
///
/// It is also the correspondence simulation. "Player A plays now, player B answers later" is
/// <see cref="TakeTurn"/> twice with anything at all in between - including quitting the game, since
/// the series lives in a file and this component holds no state of its own beyond which series it is
/// looking at.
///
/// Development only. It reads nothing and writes nothing that a shipping build depends on.
/// </summary>
public class VersusDevConsole : MonoBehaviour
{
    [Header("Participants")]
    [SerializeField] private string participantAId = "local-a";
    [SerializeField] private string participantADisplayName = "Player A";
    [SerializeField] private string participantBId = "local-b";
    [SerializeField] private string participantBDisplayName = "Player B";

    [Header("Series")]
    [Tooltip("1, 3, 5 or 7.")]
    [SerializeField] private int gameCount = 3;
    [SerializeField] private VersusMode mode = VersusMode.LocalAlternating;
    [SerializeField] private InformationPolicy informationPolicy = InformationPolicy.SealedAttempt;

    [Tooltip("Ruleset ids, in playing order. Leave empty to repeat the first playable ruleset.")]
    [SerializeField] private List<string> playlist = new List<string>();

    [Header("Match")]
    [Tooltip("Arena to play every game on.")]
    [SerializeField] private int levelId = 1;
    [SerializeField] private string characterObjectName = "drblood";

    [Header("State")]
    [Tooltip("The series being driven. Set by Create, or paste an id to resume one.")]
    [SerializeField] private string currentSeriesId = string.Empty;

    [Tooltip("Keep everything in memory instead of writing to the save folder.")]
    [SerializeField] private bool useInMemoryStore;

    private void Awake()
    {
        if (useInMemoryStore)
        {
            VersusRuntime.Override(new Level5.Core.Versus.Persistence.InMemoryVersusSeriesRepository());
        }
    }

    /// <summary>Creates a series between the two configured participants and makes it current.</summary>
    [ContextMenu("Versus/Create series")]
    public void CreateSeries()
    {
        SeriesFormat format;
        try
        {
            format = SeriesFormat.FromGameCount(gameCount);
        }
        catch (VersusDomainException exception)
        {
            Debug.LogError(exception.Message, this);
            return;
        }

        List<RulesetId> chosen = ResolvePlaylist(format);
        if (chosen == null)
        {
            return;
        }

        SeriesRequest request = new SeriesRequest(
            new MatchParticipant(new ParticipantId(participantAId), participantADisplayName),
            new MatchParticipant(new ParticipantId(participantBId), participantBDisplayName),
            format,
            chosen,
            mode,
            informationPolicy,
            false,
            true,
            "dev console");

        SeriesOperation created = VersusRuntime.Coordinator.CreateSeries(request);
        if (!created.Succeeded)
        {
            Debug.LogError("Could not create the series: " + created.Validation, this);
            return;
        }

        currentSeriesId = created.Series.Id.Value;
        Report();
    }

    /// <summary>Plays the next outstanding turn, whoever it belongs to.</summary>
    [ContextMenu("Versus/Take next turn")]
    public void TakeTurn()
    {
        VersusSeries series = LoadCurrent();
        if (series == null)
        {
            return;
        }

        ParticipantId next = NextParticipant(series);
        if (!next.HasValue)
        {
            Debug.Log("There is no turn outstanding in this series.", this);
            return;
        }

        TakeTurnAs(next);
    }

    /// <summary>Plays player A's turn. The half of a correspondence exchange that happens now.</summary>
    [ContextMenu("Versus/Take turn as A")]
    public void TakeTurnAsA()
    {
        TakeTurnAs(new ParticipantId(participantAId));
    }

    /// <summary>Plays player B's turn. The half that happens later - possibly much later.</summary>
    [ContextMenu("Versus/Take turn as B")]
    public void TakeTurnAsB()
    {
        TakeTurnAs(new ParticipantId(participantBId));
    }

    /// <summary>Prints the series as each participant is entitled to see it.</summary>
    [ContextMenu("Versus/Report")]
    public void Report()
    {
        VersusSeries series = LoadCurrent();
        if (series == null)
        {
            return;
        }

        Debug.Log(Describe(series, new ParticipantId(participantAId)), this);
        Debug.Log(Describe(series, new ParticipantId(participantBId)), this);
    }

    /// <summary>Lists every stored series, so a resumed session can find one to carry on.</summary>
    [ContextMenu("Versus/List series")]
    public void ListSeries()
    {
        IReadOnlyList<SeriesSummary> summaries = VersusRuntime.Coordinator.ListSeries();
        if (summaries.Count == 0)
        {
            Debug.Log("No versus series are stored.", this);
            return;
        }

        StringBuilder builder = new StringBuilder("Stored versus series:");
        foreach (SeriesSummary summary in summaries)
        {
            builder.AppendLine().Append("  ").Append(summary);
        }

        Debug.Log(builder.ToString(), this);
    }

    private void TakeTurnAs(ParticipantId participantId)
    {
        VersusSeries series = LoadCurrent();
        if (series == null)
        {
            return;
        }

        CharacterSelection character = new CharacterSelection(
            0,
            characterObjectName,
            characterObjectName,
            true,
            true);

        VersusLaunch launch = VersusLauncher.Launch(series.Id, participantId, levelId, character);
        if (!launch.Succeeded)
        {
            Debug.LogError($"Could not start {participantId}'s turn: {launch.Validation}", this);
        }
    }

    private VersusSeries LoadCurrent()
    {
        if (string.IsNullOrEmpty(currentSeriesId))
        {
            Debug.LogWarning("No series is selected. Create one, or paste an id into the inspector.", this);
            return null;
        }

        VersusSeries series = VersusRuntime.Coordinator.Load(new SeriesId(currentSeriesId));
        if (series == null)
        {
            Debug.LogError($"There is no stored series '{currentSeriesId}'.", this);
        }

        return series;
    }

    /// <summary>Whoever can attempt right now, preferring the participant the game designates first.</summary>
    private ParticipantId NextParticipant(VersusSeries series)
    {
        VersusGame game = series.CurrentGame;
        if (game == null)
        {
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

    private List<RulesetId> ResolvePlaylist(SeriesFormat format)
    {
        List<RulesetId> chosen = new List<RulesetId>();

        if (playlist != null && playlist.Count > 0)
        {
            foreach (string id in playlist)
            {
                chosen.Add(new RulesetId(id));
            }

            return chosen;
        }

        VersusCapability required = VersusModes.RequiredCapability(mode);
        List<CompetitiveRuleset> playable = VersusCatalogs.Rulesets.Supporting(required);
        if (playable.Count == 0)
        {
            Debug.LogError($"No ruleset supports {mode}, so no series can be created.", this);
            return null;
        }

        for (int index = 0; index < format.GameCount; index++)
        {
            chosen.Add(playable[index % playable.Count].Id);
        }

        return chosen;
    }

    /// <summary>
    /// Renders the series through one participant's view.
    ///
    /// Note what this cannot print: the opponent's score before the game resolves. Not because it
    /// chooses not to, but because the view it is given does not contain it.
    /// </summary>
    private static string Describe(VersusSeries series, ParticipantId viewerId)
    {
        ParticipantSeriesView view = series.ViewFor(viewerId);
        StringBuilder builder = new StringBuilder();
        builder.Append("[versus] as ").Append(view.You.DisplayName).Append(": ").Append(view).AppendLine();

        foreach (ParticipantGameView game in view.Games)
        {
            builder.Append("   game ").Append(game.GameNumber).Append(' ').Append(game.RulesetId.Value)
                .Append(" - ").Append(game.Status)
                .Append("; you ").Append(game.OwnAttemptState)
                .Append(", ").Append(view.Opponent.DisplayName).Append(' ').Append(game.OpponentAttemptState);

            if (game.Target.HasValue)
            {
                builder.Append("; beat ").Append(game.Target.Value);
            }

            if (game.OwnResult != null)
            {
                builder.Append("; your score ").Append(game.OwnResult.Get(AttemptMetric.Score));
            }

            if (game.OpponentResult != null)
            {
                builder.Append("; their score ").Append(game.OpponentResult.Get(AttemptMetric.Score));
            }

            builder.AppendLine();
        }

        if (view.Result != null)
        {
            builder.Append("   final: ").Append(view.Result);
        }

        return builder.ToString();
    }
}
