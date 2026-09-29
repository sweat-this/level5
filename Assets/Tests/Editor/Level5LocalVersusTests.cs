using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.BackendV2;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The production Local Versus screen's composition layer: <see cref="LocalVersusScreenModel"/> over
/// <see cref="LocalVersusFlow"/>, <see cref="VersusRuntime"/>'s coordinator and <see cref="VersusLauncher"/>.
///
/// These drive the same seam the uGUI buttons do. Every assertion about whose turn it is, what the
/// score is or whether a series is over is checked against the series the coordinator loads - the
/// point of the screen is that it keeps none of that itself.
/// </summary>
public class Level5LocalVersusTests
{
    private const int EligibleLevelId = 1;
    private const int IncompatibleLevelId = 2;
    private const int LockedLevelId = 3;
    private const int NonSelectableLevelId = 4;
    private const int UnlockedCharacterId = 1;
    private const int SecondUnlockedCharacterId = 3;
    private const int LockedCharacterId = 2;

    private string tempRoot;
    private List<string> loadedScenes;
    private Dictionary<int, bool> characterUnlocks;
    private readonly List<GameObject> hosts = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "level5-local-versus-" + Guid.NewGuid().ToString("N"));

        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        MatchCatalogs.Override(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[]
            {
                TestDefinitions.Level(EligibleLevelId),
                TestDefinitions.Level(IncompatibleLevelId, capabilities: ArenaCapability.None),
                TestDefinitions.Level(LockedLevelId, locked: true),
                TestDefinitions.Level(NonSelectableLevelId, selectable: false)
            }));

        VersusCatalogs.Override(VersusTestFixtures.Catalog(
            VersusTestFixtures.ScoreRuleset(),
            VersusTestFixtures.ScoreRuleset("async-only", capabilities: VersusCapability.Asynchronous),
            VersusTestFixtures.ScoreRuleset("no-capability", capabilities: VersusCapability.None)));

        UseFileRepository();

        characterUnlocks = new Dictionary<int, bool>
        {
            [UnlockedCharacterId] = true,
            [LockedCharacterId] = false,
            [SecondUnlockedCharacterId] = true
        };

        loadedScenes = new List<string>();
        VersusLauncher.OverrideSceneLoader(scene => loadedScenes.Add(scene));
        LocalVersusNavigationState.Clear();
        PlayerSelectionSession.Clear();
        MatchController.instance = null;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject host in hosts)
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        hosts.Clear();
        MatchController.instance = null;
        VersusLauncher.ResetSceneLoader();
        LocalVersusNavigationState.Clear();
        ActiveMatch.Clear();
        ActiveVersusAttempt.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();
        MatchCatalogs.Reset();
        PlayerSelectionSession.Clear();
        BackendV2SessionStore.Clear();

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, true);
        }
    }

    // ---------------------------------------------------------------- creation

    [Test]
    public void CreatingASeriesUsesTheFixedLocalAlternatingContract()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();

        SeriesOperation created = model.Create("Alice", "Bob");

        Assert.That(created.Succeeded, Is.True, created.Validation?.ToString());
        VersusSeries series = VersusRuntime.Coordinator.Load(created.Series.Id);
        Assert.That(series.Mode, Is.EqualTo(VersusMode.LocalAlternating));
        Assert.That(series.Snapshot.InformationPolicy, Is.EqualTo(InformationPolicy.SealedAttempt));
        Assert.That(series.Snapshot.AlternatesFirstAttempt, Is.True);
        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active), "no invitation: a local series starts active");
        Assert.That(series.Participants.First.DisplayName, Is.EqualTo("Alice"));
        Assert.That(series.Participants.Second.DisplayName, Is.EqualTo("Bob"));
        Assert.That(model.SelectedSeries.Id, Is.EqualTo(created.Series.Id), "a created series is selected");
    }

    [Test]
    public void TheRequestCarriesTheFixedSemanticsAndADiagnosticSource()
    {
        SeriesRequest request = LocalVersusFlow.BuildRequest("A", "B", 3, new RulesetId("most-points"));

        Assert.That(request.Mode, Is.EqualTo(VersusMode.LocalAlternating));
        Assert.That(request.InformationPolicy, Is.EqualTo(InformationPolicy.SealedAttempt));
        Assert.That(request.RequiresInvitation, Is.False);
        Assert.That(request.AlternatesFirstAttempt, Is.True);
        Assert.That(request.Source, Is.EqualTo("local versus UI"));
    }

    [Test]
    public void EachSeriesGetsTwoDistinctOpaqueParticipantIdsUnrelatedToAnyAccount()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();

        VersusSeries first = model.Create("Alice", "Bob").Series;
        VersusSeries second = model.Create("Alice", "Bob").Series;

        Assert.That(first.Participants.First.Id, Is.Not.EqualTo(first.Participants.Second.Id));
        Assert.That(first.Participants.First.Id, Is.Not.EqualTo(second.Participants.First.Id), "ids are series-local, not name-derived");
        Assert.That(first.Participants.First.Id.Value, Does.Match("^[0-9a-f]{32}$"), "Guid.NewGuid().ToString(\"N\")");
        Assert.That(first.Participants.First.DisplayName, Is.EqualTo("Alice"), "the name is presentation, not identity");
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(7)]
    public void TheChosenFormatResolvesThroughSeriesFormatAndRepeatsTheSelectedRuleset(int gameCount)
    {
        SeriesRequest request = LocalVersusFlow.BuildRequest("A", "B", gameCount, new RulesetId("most-points"));

        Assert.That(request.Format, Is.EqualTo(SeriesFormat.FromGameCount(gameCount)));
        Assert.That(request.Playlist, Has.Count.EqualTo(gameCount));
        Assert.That(request.Playlist.All(id => id.Value == "most-points"), Is.True);
    }

    [Test]
    public void TheFormatButtonOffersExactlyTheLengthsSeriesFormatAccepts()
    {
        foreach (int gameCount in LocalVersusFlow.OfferedGameCounts)
        {
            Assert.DoesNotThrow(() => SeriesFormat.FromGameCount(gameCount));
        }

        Assert.That(LocalVersusFlow.OfferedGameCounts, Is.EqualTo(new[] { 1, 3, 5, 7 }));
    }

    [Test]
    public void ACreatedBestOf3StoresTheRepeatedPlaylistInTheFrozenSnapshot()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleFormat(); // default is best of 3; 3 -> 5
        model.CycleFormat(); // 5 -> 7
        model.CycleFormat(); // 7 -> 1
        model.CycleFormat(); // 1 -> 3
        Assert.That(model.SelectedGameCount, Is.EqualTo(3));

        VersusSeries series = model.Create("A", "B").Series;

        Assert.That(series.Snapshot.GameCount, Is.EqualTo(3));
        Assert.That(series.Snapshot.Games.All(game => game.Id.Value == "most-points"), Is.True);
    }

    [Test]
    public void APersistenceFailureIsShownAndNothingIsSelected()
    {
        VersusRuntime.Override(new UnsavableRepository(), VersusCatalogs.Rulesets);
        LocalVersusScreenModel model = NewModel();
        model.Open();

        SeriesOperation created = model.Create("A", "B");

        Assert.That(created.Succeeded, Is.False);
        Assert.That(created.Validation.HasError(VersusValidationCode.PersistenceFailed), Is.True);
        Assert.That(model.CreateMessage, Does.Contain("could not be saved"));
        Assert.That(model.SelectedSeries, Is.Null, "a series that was not stored must not appear selected");
        Assert.That(model.Summaries, Is.Empty);
    }

    // ---------------------------------------------------------------- rulesets and lists

    [Test]
    public void OnlyRulesetsSupportingLocalAlternatingAreOffered()
    {
        List<CompetitiveRuleset> offered = LocalVersusFlow.SelectableRulesets();

        Assert.That(offered.Select(ruleset => ruleset.Id.Value), Is.EqualTo(new[] { "most-points" }));
        Assert.That(offered[0].DisplayName, Is.EqualTo("Most Points"));

        LocalVersusScreenModel model = NewModel();
        model.Open();
        Assert.That(model.Rulesets.Select(ruleset => ruleset.Id.Value), Is.EqualTo(new[] { "most-points" }));
        Assert.That(model.RulesetText, Is.EqualTo("Ruleset: Most Points"));
    }

    [Test]
    public void TheListShowsOnlyUnarchivedLocalSeriesWithUnfinishedOnesFirst()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId finished = model.Create("Done", "Done2").Series.Id;
        PlayWholeSeries(finished, participantOneWinsEveryGame: true);
        SeriesId archived = model.Create("Old", "Old2").Series.Id;
        PlayWholeSeries(archived, participantOneWinsEveryGame: true);
        Assert.That(VersusRuntime.Coordinator.Archive(archived), Is.True);
        SeriesId active = model.Create("Live", "Live2").Series.Id;

        // an asynchronous series (correspondence-shaped) stored in the same repository
        SeriesOperation asynchronous = VersusRuntime.Coordinator.CreateSeries(VersusTestFixtures.Request(
            SeriesFormat.BestOf1,
            VersusTestFixtures.Playlist(SeriesFormat.BestOf1, new RulesetId("most-points")),
            VersusMode.Asynchronous));
        Assert.That(asynchronous.Succeeded, Is.True);

        List<SeriesSummary> listed = LocalVersusFlow.ListLocal(VersusRuntime.Coordinator);

        Assert.That(listed.Select(summary => summary.Id), Is.EqualTo(new[] { active, finished }));
        Assert.That(listed.All(summary => summary.Mode == VersusMode.LocalAlternating && !summary.Archived), Is.True);
    }

    [Test]
    public void TheListTextRendersNamesScoreCurrentGameFormatAndFinalStatus()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId finished = model.Create("Ann", "Ben").Series.Id;
        PlayWholeSeries(finished, participantOneWinsEveryGame: true);
        model.Create("Cy", "Di");

        model.Refresh();

        Assert.That(model.ListText, Does.Contain("Active"));
        Assert.That(model.ListText, Does.Contain("Cy 0 - 0 Di | game 1 | best of 3"));
        Assert.That(model.ListText, Does.Contain("Finished"));
        Assert.That(model.ListText, Does.Contain("Ann 2 - 0 Ben | Completed | best of 3"));
    }

    // ---------------------------------------------------------------- restart / resume

    [Test]
    public void ARecomposedRuntimeStillListsAndCanResumeAStoredSeries()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;

        // process restart: nothing in memory survives except the folder
        VersusRuntime.Reset();
        UseFileRepository();
        LocalVersusNavigationState.Clear();

        LocalVersusScreenModel reopened = NewModel();
        reopened.Open();

        Assert.That(reopened.Summaries.Select(summary => summary.Id), Does.Contain(id));
        Assert.That(reopened.SelectedSeries.Id, Is.EqualTo(id));
        Assert.That(reopened.NextParticipant.HasValue, Is.True, "the stored series is resumable");
        Assert.That(reopened.CanPlayTurn, Is.True);
    }

    [Test]
    public void ARestartMidSeriesResumesTheSameParticipantsTurn()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;
        ParticipantId firstUp = model.NextParticipant;
        model.PlayTurn();
        ParticipantId launchedAs = ActiveVersusAttempt.ParticipantId;
        Assert.That(launchedAs, Is.EqualTo(firstUp));

        // the app dies mid-turn: static state gone, attempt outstanding on disk
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        LocalVersusNavigationState.Clear();
        VersusRuntime.Reset();
        UseFileRepository();

        LocalVersusScreenModel reopened = NewModel();
        reopened.Open();
        Assert.That(reopened.SelectedSeries.Id, Is.EqualTo(id));
        Assert.That(reopened.NextParticipant, Is.EqualTo(firstUp));

        AttemptId outstanding = VersusRuntime.Coordinator.Load(id)
            .ViewFor(firstUp).CurrentGame.OwnAttemptId;
        reopened.PlayTurn();

        Assert.That(ActiveVersusAttempt.AttemptId, Is.EqualTo(outstanding), "the existing attempt is handed back, not replaced");
    }

    // ---------------------------------------------------------------- turn ownership

    [Test]
    public void ABestOf3IsDrivenEntirelyByWhatTheLoadedSeriesSays()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;
        List<ParticipantId> observedOrder = new List<ParticipantId>();

        for (int guard = 0; guard < 6 && model.SelectedSeries.IsActive; guard++)
        {
            VersusSeries stored = VersusRuntime.Coordinator.Load(id);
            VersusGame game = stored.CurrentGame;
            ParticipantId first = stored.Participants.At(game.FirstAttemptParticipantIndex).Id;
            ParticipantId second = stored.Participants.Opponent(first).Id;
            ParticipantId expected = stored.ViewFor(first).CurrentGame.OwnAttemptState == AttemptState.Completed
                ? second
                : first;

            model.Refresh();
            Assert.That(model.NextParticipant, Is.EqualTo(expected), "the screen asks the series, not a counter");

            VersusLaunch launch = model.PlayTurn();
            Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
            Assert.That(ActiveVersusAttempt.ParticipantId, Is.EqualTo(expected));
            observedOrder.Add(expected);

            // the participant who designated first (participant one) always outscores
            SubmitActiveAttempt(id, score: expected == stored.Participants.First.Id ? 10 : 5);
            model.Refresh();
        }

        Assert.That(model.SelectedSeries.IsOver, Is.True);
        Assert.That(observedOrder, Has.Count.EqualTo(4), "two games, two turns each - the third game is never needed");
        Assert.That(model.NextParticipant.HasValue, Is.False);
        Assert.That(model.CanPlayTurn, Is.False);
        Assert.That(model.DetailText, Does.Contain("Ann 2 - 0 Ben"));
        Assert.That(model.DetailText, Does.Contain("Ann wins the series."));
        Assert.That(model.PlayTurn().Succeeded, Is.False, "a finished series offers no turn");
    }

    [Test]
    public void AfterASubmittedResultTheScreenShowsTheNewScoreAndTheOtherSidesTurn()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;
        ParticipantId firstUp = model.NextParticipant;

        model.PlayTurn();
        SubmitActiveAttempt(id, score: 10);
        model.Refresh(id);
        Assert.That(model.NextParticipant, Is.Not.EqualTo(firstUp), "the opponent is up after the first attempt");
        Assert.That(model.DetailText, Does.Contain("is up."));

        model.PlayTurn();
        SubmitActiveAttempt(id, score: 5);
        model.Refresh(id);

        Assert.That(model.DetailText, Does.Contain("Ann 1 - 0 Ben").Or.Contain("Ann 0 - 1 Ben"), "the game resolved and the score moved");
        Assert.That(model.SelectedSeries.Score.PlayedGames, Is.EqualTo(1));
    }

    // ---------------------------------------------------------------- character / level

    [Test]
    public void OnlyEligibleArenasAndUnlockedCharactersAreOffered()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.Create("A", "B");

        Assert.That(model.Levels.Select(level => level.LevelId), Is.EqualTo(new[] { EligibleLevelId }),
            "locked, non-selectable and mode-incompatible arenas are filtered before IssueAttempt");
        Assert.That(model.Characters.Select(character => character.CharacterId),
            Is.EqualTo(new[] { UnlockedCharacterId, SecondUnlockedCharacterId }));
    }

    [Test]
    public void ALockedCharacterCannotLaunchAndTheTurnIsNotLost()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("A", "B").Series.Id;
        ParticipantId next = model.NextParticipant;

        VersusLaunch launch = LocalVersusFlow.LaunchTurn(
            id,
            next,
            EligibleLevelId,
            new CharacterSelection(LockedCharacterId, "locked", "Locked", true, true),
            Snapshot());

        Assert.That(launch.Succeeded, Is.False);
        Assert.That(launch.Validation.ToString(), Does.Contain("not unlocked"));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(
            VersusRuntime.Coordinator.Load(id).ViewFor(next).CurrentGame.OwnAttemptId,
            Is.EqualTo(AttemptId.None),
            "refused before IssueAttempt, so the turn is untouched");
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False, "a failed launch leaves no return hint behind");
        Assert.That(loadedScenes, Is.Empty);
    }

    [TestCase(LockedLevelId)]
    [TestCase(NonSelectableLevelId)]
    [TestCase(999)]
    public void AnIneligibleLevelCannotLaunchAndNoAttemptIsIssued(int levelId)
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("A", "B").Series.Id;
        ParticipantId next = model.NextParticipant;

        VersusLaunch launch = LocalVersusFlow.LaunchTurn(id, next, levelId, Character(UnlockedCharacterId), Snapshot());

        Assert.That(launch.Succeeded, Is.False);
        Assert.That(loadedScenes, Is.Empty);
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False);
        Assert.That(
            VersusRuntime.Coordinator.Load(id).ViewFor(next).CurrentGame.OwnAttemptId,
            Is.EqualTo(AttemptId.None));
    }

    [Test]
    public void AValidCharacterAndLevelLaunchThroughTheLauncher()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("A", "B").Series.Id;
        ParticipantId next = model.NextParticipant;

        VersusLaunch launch = model.PlayTurn();

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(ActiveMatch.Configuration, Is.Not.Null);
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        Assert.That(ActiveMatch.Configuration.Roster.Count, Is.EqualTo(1));
        Assert.That(ActiveMatch.Configuration.Roster.LocalHumanCount, Is.EqualTo(1));
        PlayerSlot slot = ActiveMatch.Configuration.Roster.Players[0];
        Assert.That(slot.ParticipantId, Is.EqualTo(next.Value));
        Assert.That(slot.Character.CharacterId, Is.EqualTo(model.SelectedCharacter.CharacterId));
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(model.SelectedLevel.LevelId));
        Assert.That(loadedScenes, Has.Count.EqualTo(1));
    }

    [Test]
    public void CyclingTheCharacterChangesWhoPlaysTheTurnNotTheSeries()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.Create("A", "B");
        int before = model.SelectedCharacter.CharacterId;

        model.CycleCharacter();
        VersusLaunch launch = model.PlayTurn();

        Assert.That(model.SelectedCharacter.CharacterId, Is.Not.EqualTo(before));
        Assert.That(launch.Succeeded, Is.True);
        Assert.That(ActiveMatch.Configuration.Roster.Players[0].Character.CharacterId,
            Is.EqualTo(model.SelectedCharacter.CharacterId));
    }

    // ---------------------------------------------------------------- navigation

    [Test]
    public void ALocalVersusLaunchMarksTheReturnAndRoutesTheLoadingSceneBackToTheSeriesScreen()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("A", "B").Series.Id;
        Assert.That(LoadManager.ResolvePostLoadScene(), Is.EqualTo(Constants.SCENE_NAME_level_00_start));

        model.PlayTurn();

        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True);
        Assert.That(LocalVersusNavigationState.PreferredSeriesId, Is.EqualTo(id));
        Assert.That(LoadManager.ResolvePostLoadScene(), Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus));
    }

    [Test]
    public void AnOrdinaryMatchKeepsTheStartScreenNavigationEvenAfterAnAbandonedVersusTurn()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.Create("A", "B");
        model.PlayTurn();
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True);

        // the player walks away and starts an ordinary match: a new configuration replaces the launched one
        MatchConfiguration ordinary = ActiveMatch.Configuration;
        ActiveMatch.Begin(MatchCatalogs.Builder.Build(
            new MatchRequest(
                GameModeId.TotalPoints,
                EligibleLevelId,
                PlayerRoster.SingleLocalHuman(Character(UnlockedCharacterId)),
                MatchModifiers.Default,
                CheerleaderSelection.None,
                "ordinary"),
            Snapshot()).Configuration);
        Assert.That(ActiveMatch.Configuration, Is.Not.SameAs(ordinary));

        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False);
        Assert.That(LoadManager.ResolvePostLoadScene(), Is.EqualTo(Constants.SCENE_NAME_level_00_start));
    }

    [Test]
    public void OpeningTheScreenSelectsThePreferredSeriesAndConsumesTheHint()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId older = model.Create("Older", "Older2").Series.Id;
        model.Create("Newer", "Newer2");
        model.Select(older);
        model.PlayTurn();

        LocalVersusScreenModel reopened = NewModel();
        reopened.Open();

        Assert.That(reopened.SelectedSeries.Id, Is.EqualTo(older), "the series the turn belonged to, not just the newest");
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False);
        Assert.That(LocalVersusNavigationState.PreferredSeriesId.HasValue, Is.False, "consumed");
    }

    [Test]
    public void WithoutTheNavigationStateTheFirstUnfinishedSeriesIsStillShown()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId finished = model.Create("Done", "Done2").Series.Id;
        PlayWholeSeries(finished, participantOneWinsEveryGame: true);
        SeriesId active = model.Create("Live", "Live2").Series.Id;
        LocalVersusNavigationState.Clear();
        VersusRuntime.Reset();
        UseFileRepository();

        LocalVersusScreenModel reopened = NewModel();
        reopened.Open();

        Assert.That(reopened.SelectedSeries.Id, Is.EqualTo(active));
    }

    // ---------------------------------------------------------------- backend v2 independence

    [Test]
    public void EverythingWorksWithNoBackendV2Session()
    {
        BackendV2SessionStore.Clear();
        LocalVersusScreenModel model = NewModel();
        model.Open();

        Assert.That(model.Create("A", "B").Succeeded, Is.True);
        Assert.That(model.Summaries, Has.Count.EqualTo(1));
        Assert.That(model.PlayTurn().Succeeded, Is.True);
        Assert.That(BackendV2SessionStore.Current, Is.Null);
    }

    [Test]
    public void LocalVersusLeavesAnExistingBackendV2SessionExactlyAsItFoundIt()
    {
        BackendV2Session session = new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30));
        BackendV2SessionStore.Set(session);
        int changes = 0;
        Action<BackendV2Session> counter = _ => changes++;
        BackendV2SessionStore.Changed += counter;
        try
        {
            LocalVersusScreenModel model = NewModel();
            model.Open();
            SeriesId id = model.Create("A", "B").Series.Id;
            model.PlayTurn();
            SubmitActiveAttempt(id, 10);
            model.Refresh(id);
            model.Open();
        }
        finally
        {
            BackendV2SessionStore.Changed -= counter;
        }

        Assert.That(BackendV2SessionStore.Current, Is.SameAs(session));
        Assert.That(BackendV2SessionStore.Current.AccessToken, Is.EqualTo("access-token"));
        Assert.That(BackendV2SessionStore.Current.RefreshToken, Is.EqualTo("refresh-token"));
        Assert.That(changes, Is.Zero, "no local-versus operation touches the session");
    }

    [Test]
    public void ALocalVersusAttemptIsExcludedFromGeneralMatchResultSubmission()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.Create("A", "B");
        model.PlayTurn();

        Assert.That(ActiveVersusAttempt.IsActive, Is.True,
            "the competitive-attempt marker that keeps a versus match out of POST /api/v2/match-results is set");
    }

    // ---------------------------------------------------------------- explicit exit
    //
    // A deliberate exit (pause menu Start/Menu or Quit) is only allowed once nothing is owed to the
    // stored series: a live turn must have its forfeit durably saved, and a finished run must have its
    // result durably saved by VersusMatchReporter. The durable side is always read back through a fresh
    // FileVersusSeriesRepository, so an in-memory change that never reached disk cannot pass.

    [Test]
    public void ExplicitExitIsAllowedWithNoActiveAttemptAndWritesNothing()
    {
        SaveGateRepository gate = UseGatedRepository();
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId id = model.Create("A", "B").Series.Id;
        int savesBefore = gate.SaveCount;

        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True);

        Assert.That(gate.SaveCount, Is.EqualTo(savesBefore), "leaving an ordinary match touches no series");
        Assert.That(DurableSeries(id).Games[0].Status, Is.Not.EqualTo(VersusGameStatus.Forfeited));
    }

    [Test]
    public void AQuitDuringALiveTurnDurablyForfeitsThatGameThenMayLeave()
    {
        UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out ParticipantId quitter, out _);
        Assert.That(VersusQuitPolicy.TurnInProgress, Is.True);

        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True);

        VersusSeries durable = DurableSeries(id);
        Assert.That(durable.Games[0].Status, Is.EqualTo(VersusGameStatus.Forfeited));
        Assert.That(durable.Games[0].Result.WinnerId, Is.EqualTo(durable.Participants.Opponent(quitter).Id),
            "the opponent is awarded the game");
        Assert.That(durable.Status, Is.EqualTo(SeriesStatus.Active), "one forfeited game does not end a best of three");
        Assert.That(durable.CurrentGame.Index, Is.EqualTo(1), "the series advanced through the normal domain rules");
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "released once the forfeit is stored");
    }

    [Test]
    public void AQuitCanCompleteTheSeriesThroughTheNormalDomainRules()
    {
        UseGatedRepository();
        StartLiveTurn(1, out SeriesId id, out ParticipantId quitter, out _);

        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True);

        VersusSeries durable = DurableSeries(id);
        Assert.That(durable.Status, Is.EqualTo(SeriesStatus.Completed), "a single game decides a best of one");
        Assert.That(durable.Result.WinnerId, Is.EqualTo(durable.Participants.Opponent(quitter).Id));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
    }

    [Test]
    public void AFailedForfeitSaveKeepsThePlayerInTheMatchAndTheAttemptOutstanding()
    {
        SaveGateRepository gate = UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out ParticipantId quitter, out _);
        AttemptId attemptId = ActiveVersusAttempt.AttemptId;

        gate.FailSaves = true;
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "navigation must not start");

        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "the attempt is not cleared on a failed save");
        Assert.That(ActiveVersusAttempt.AttemptId, Is.EqualTo(attemptId));
        VersusSeries durable = DurableSeries(id);
        Assert.That(durable.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "still the last durable state");
        Assert.That(durable.ViewFor(quitter).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));

        // nothing retried behind the player's back; a later, explicit action does
        int failedSaves = gate.SaveCount;
        gate.FailSaves = false;
        Assert.That(gate.SaveCount, Is.EqualTo(failedSaves));
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True);
        Assert.That(DurableSeries(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Forfeited));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
    }

    [Test]
    public void AnEndedMatchAwaitingItsResultSaveCannotBeLeftAndIsNotForfeited()
    {
        SaveGateRepository gate = UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out ParticipantId participant, out MatchController match);

        Assert.That(match.RequestEnd(MatchEndReason.TimeExpired), Is.True);
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.Ending), "ended, but the end work has not completed");

        Assert.That(VersusQuitPolicy.TurnInProgress, Is.False);
        Assert.That(VersusQuitPolicy.AttemptOutstanding, Is.True);
        int savesBefore = gate.SaveCount;

        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False);

        Assert.That(gate.SaveCount, Is.EqualTo(savesBefore), "no forfeit was written for an already-played run");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        VersusSeries durable = DurableSeries(id);
        Assert.That(durable.Games[0].Status, Is.EqualTo(VersusGameStatus.Active));
        Assert.That(durable.ViewFor(participant).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
    }

    [Test]
    public void AFailedResultSaveBlocksLeavingUntilTheReporterRetrySucceeds()
    {
        SaveGateRepository gate = UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out ParticipantId participant, out MatchController match);
        match.RequestEnd(MatchEndReason.TimeExpired);
        GameStats stats = BuildStats(21);

        gate.FailSaves = true;
        Assert.That(VersusMatchReporter.TryReport(stats, GameModeId.TotalPoints, 60f), Is.False, "GameRules will retry");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "Continue Series cannot leave yet");
        Assert.That(DurableSeries(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "and nothing was forfeited");

        gate.FailSaves = false;
        Assert.That(VersusMatchReporter.TryReport(stats, GameModeId.TotalPoints, 60f), Is.True, "the retry stores the result");
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True, "now Continue Series can leave");

        VersusSeries durable = DurableSeries(id);
        Assert.That(durable.Games[0].Status, Is.Not.EqualTo(VersusGameStatus.Forfeited), "the earned result stands");
        Assert.That(durable.ViewFor(participant).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Completed));
    }

    [Test]
    public void ACrashOrLoadInterruptionIsNotAForfeitAndTheTurnIsReissued()
    {
        UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out ParticipantId participant, out _);
        AttemptId interrupted = ActiveVersusAttempt.AttemptId;

        // the process dies (or the scene never loads): the quit policy never runs, all transient state is lost
        ActiveVersusAttempt.Clear();
        LocalVersusNavigationState.Clear();
        MatchController.instance = null;
        VersusRuntime.Override(new FileVersusSeriesRepository(tempRoot), VersusCatalogs.Rulesets);

        Assert.That(DurableSeries(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "nothing was forfeited");
        AttemptOperation reissued = VersusRuntime.Coordinator.IssueAttempt(id, participant);
        Assert.That(reissued.Succeeded, Is.True, reissued.Validation?.ToString());
        Assert.That(reissued.Attempt.Id, Is.EqualTo(interrupted), "the same outstanding attempt comes back");
    }

    [Test]
    public void ExplicitExitCoroutinesStopBeforeAnyNavigationWhenTheExitIsNotDurable()
    {
        SaveGateRepository gate = UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out _, out MatchController match);
        Pause pause = NewBoundPause();

        // Each coroutine must finish on its first step: no database wait, no scene load, no quit.
        gate.FailSaves = true;
        Assert.That(pause.loadstartScreen().MoveNext(), Is.False, "Start/Menu stays put when the forfeit cannot be saved");
        Assert.That(pause.Quit().MoveNext(), Is.False, "Quit stays put when the forfeit cannot be saved");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(DurableSeries(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active));

        gate.FailSaves = false;
        match.RequestEnd(MatchEndReason.TimeExpired);
        int savesBefore = gate.SaveCount;
        Assert.That(pause.loadstartScreen().MoveNext(), Is.False, "Continue Series waits for the result save");
        Assert.That(pause.Quit().MoveNext(), Is.False, "so does Quit");
        Assert.That(gate.SaveCount, Is.EqualTo(savesBefore), "and neither forfeits the finished run");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
    }

    [Test]
    public void RestartIsRefusedWhileAnAttemptIsOutstanding()
    {
        UseGatedRepository();
        StartLiveTurn(3, out SeriesId id, out _, out _);
        Pause pause = NewBoundPause();
        MatchConfiguration configuration = ActiveMatch.Configuration;

        // a restart that went ahead would begin a new match and load the scene, which cannot happen in EditMode
        Assert.DoesNotThrow(() => pause.reloadScene());

        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveMatch.Configuration, Is.SameAs(configuration), "no new match was begun");
        Assert.That(DurableSeries(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active));
    }

    [Test]
    public void ThePauseBindingIsTheRealQuitPolicy()
    {
        // Level5.Match cannot reference Level5.Versus, so GameLevelManager is the only place the
        // policy reaches Pause. If this wiring were dropped, Pause's "no series" defaults would silently
        // let every deliberate exit skip the durability gate.
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts", "game manager", "GameLevelManager.cs"));

        Assert.That(source, Does.Contain("BindVersusContext"));
        Assert.That(source, Does.Contain("VersusQuitPolicy.TryPrepareForExplicitExit"));
        Assert.That(source, Does.Contain("VersusQuitPolicy.AttemptOutstanding"));
        Assert.That(source, Does.Contain("VersusQuitPolicy.TurnInProgress"));
    }

    [Test]
    public void TheNavigationHintResetsAtSubsystemRegistration()
    {
        LocalVersusNavigationState.Begin(new SeriesId("left-over"));
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True);

        MethodInfo reset = typeof(LocalVersusNavigationState).GetMethod(
            "ResetOnLoad", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(reset, Is.Not.Null);
        RuntimeInitializeOnLoadMethodAttribute hook = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
        Assert.That(hook, Is.Not.Null);
        Assert.That(hook.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration),
            "static state survives with domain reload disabled, so it must be cleared before the first scene");

        reset.Invoke(null, null);

        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False);
        Assert.That(LocalVersusNavigationState.PreferredSeriesId.HasValue, Is.False);
    }

    // ---------------------------------------------------------------- helpers

    private LocalVersusScreenModel NewModel()
    {
        return new LocalVersusScreenModel(Snapshot, _ => new List<CharacterSelectOption>
        {
            Option(UnlockedCharacterId, "Dr Blood", true),
            Option(LockedCharacterId, "Locked One", false),
            Option(SecondUnlockedCharacterId, "Second", true)
        });
    }

    private UnlockSnapshot Snapshot()
    {
        return new UnlockSnapshot(
            characterUnlocks,
            new Dictionary<int, bool>
            {
                [EligibleLevelId] = true,
                [IncompatibleLevelId] = true,
                [LockedLevelId] = false,
                [NonSelectableLevelId] = true
            });
    }

    private static CharacterSelectOption Option(int id, string name, bool unlocked)
    {
        return new CharacterSelectOption(id, name, name.ToLowerInvariant().Replace(" ", string.Empty), true, true, unlocked, null);
    }

    private static CharacterSelection Character(int id)
    {
        return new CharacterSelection(id, "drblood", "Dr Blood", true, true);
    }

    private void UseFileRepository()
    {
        VersusRuntime.Override(new FileVersusSeriesRepository(tempRoot), VersusCatalogs.Rulesets);
    }

    /// <summary>
    /// Points the game at the same on-disk store, behind a wrapper that can be told to fail saves.
    /// Loads always come from disk, so a failed save leaves exactly the last durable series behind.
    /// </summary>
    private SaveGateRepository UseGatedRepository()
    {
        SaveGateRepository gate = new SaveGateRepository(new FileVersusSeriesRepository(tempRoot));
        VersusRuntime.Override(gate, VersusCatalogs.Rulesets);
        return gate;
    }

    /// <summary>The series as a freshly started process would read it from disk.</summary>
    private VersusSeries DurableSeries(SeriesId id)
    {
        VersusSeries series = new FileVersusSeriesRepository(tempRoot).Load(id);
        Assert.That(series, Is.Not.Null, "the series is not on disk");
        return series;
    }

    /// <summary>
    /// A real local series, launched through <see cref="LocalVersusFlow.LaunchTurn"/> (which issues and
    /// starts the attempt and begins <see cref="ActiveVersusAttempt"/>), with a match that is playing.
    /// </summary>
    private void StartLiveTurn(int gameCount, out SeriesId id, out ParticipantId participant, out MatchController match)
    {
        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator, "A", "B", gameCount, LocalVersusFlow.SelectableRulesets()[0].Id);
        Assert.That(created.Succeeded, Is.True, created.Validation?.ToString());
        id = created.Series.Id;
        participant = LocalVersusFlow.NextParticipant(created.Series);

        VersusLaunch launch = LocalVersusFlow.LaunchTurn(
            id, participant, EligibleLevelId, Character(UnlockedCharacterId), Snapshot());
        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);

        GameObject host = new GameObject("quit-policy-match");
        hosts.Add(host);
        match = host.AddComponent<MatchController>();
        MatchController.instance = match; // what Awake does in play mode
        match.BeginPlay();
        Assert.That(match.IsPlaying, Is.True);
    }

    /// <summary>A <see cref="Pause"/> wired to the quit policy exactly as GameLevelManager wires it.</summary>
    private Pause NewBoundPause()
    {
        GameObject host = new GameObject("quit-policy-pause");
        hosts.Add(host);
        Pause pause = host.AddComponent<Pause>();
        pause.BindVersusContext(
            () => VersusQuitPolicy.AttemptOutstanding,
            () => VersusQuitPolicy.TurnInProgress,
            VersusQuitPolicy.TryPrepareForExplicitExit);
        return pause;
    }

    private GameStats BuildStats(int totalPoints)
    {
        GameObject host = new GameObject("quit-policy-stats");
        hosts.Add(host);
        GameStats stats = host.AddComponent<GameStats>();
        stats.TotalPoints = totalPoints;
        stats.ShotMade = 7;
        stats.ShotAttempt = 10;
        return stats;
    }

    /// <summary>Submits the outstanding attempt the launcher just started, as the match-end reporter would.</summary>
    private static void SubmitActiveAttempt(SeriesId seriesId, float score)
    {
        CompetitiveRuleset ruleset = VersusRuntime.Coordinator.Catalog.Find(ActiveVersusAttempt.RulesetId);
        SubmissionOperation submitted = VersusRuntime.Coordinator.SubmitResult(
            seriesId,
            ActiveVersusAttempt.AttemptId,
            ActiveVersusAttempt.ParticipantId,
            VersusTestFixtures.Result(ruleset, score));
        Assert.That(submitted.Succeeded, Is.True, submitted.Validation?.ToString());
        ActiveVersusAttempt.Clear();
    }

    /// <summary>Plays a series to its end straight through the coordinator (no UI), participant one winning or losing every game.</summary>
    private static void PlayWholeSeries(SeriesId seriesId, bool participantOneWinsEveryGame)
    {
        VersusMatchCoordinator coordinator = VersusRuntime.Coordinator;
        for (int guard = 0; guard < 12; guard++)
        {
            VersusSeries series = coordinator.Load(seriesId);
            if (!series.IsActive)
            {
                return;
            }

            ParticipantId next = LocalVersusFlow.NextParticipant(series);
            AttemptOperation issued = coordinator.IssueAttempt(seriesId, next);
            Assert.That(issued.Succeeded, Is.True, issued.Validation?.ToString());
            coordinator.StartAttempt(seriesId, issued.Attempt.Id);

            bool isOne = next == series.Participants.First.Id;
            float score = isOne == participantOneWinsEveryGame ? 10 : 5;
            CompetitiveRuleset ruleset = series.Snapshot.GameAt(issued.Attempt.GameIndex);
            SubmissionOperation submitted = coordinator.SubmitResult(
                seriesId, issued.Attempt.Id, next, VersusTestFixtures.Result(ruleset, score));
            Assert.That(submitted.Succeeded, Is.True, submitted.Validation?.ToString());
        }
    }

    /// <summary>Delegates to a real store, but can be told to refuse saves, and counts the saves it accepts.</summary>
    private sealed class SaveGateRepository : IVersusSeriesRepository
    {
        private readonly IVersusSeriesRepository inner;

        public SaveGateRepository(IVersusSeriesRepository inner)
        {
            this.inner = inner;
        }

        public bool FailSaves { get; set; }

        public int SaveCount { get; private set; }

        public bool Save(VersusSeries series)
        {
            if (FailSaves)
            {
                return false;
            }

            SaveCount++;
            return inner.Save(series);
        }

        public VersusSeries Load(SeriesId id) => inner.Load(id);

        public bool Exists(SeriesId id) => inner.Exists(id);

        public IReadOnlyList<SeriesSummary> ListSummaries() => inner.ListSummaries();

        public bool Delete(SeriesId id) => inner.Delete(id);

        public bool Archive(SeriesId id) => inner.Archive(id);
    }

    /// <summary>A store that cannot make anything durable.</summary>
    private sealed class UnsavableRepository : IVersusSeriesRepository
    {
        public bool Save(VersusSeries series) => false;

        public VersusSeries Load(SeriesId id) => null;

        public bool Exists(SeriesId id) => false;

        public IReadOnlyList<SeriesSummary> ListSummaries() => new List<SeriesSummary>();

        public bool Delete(SeriesId id) => false;

        public bool Archive(SeriesId id) => false;
    }
}
