using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The first production local-simultaneous slice: Most Points, two local humans, one match, played
/// through <see cref="VersusLauncher.LaunchSimultaneous"/>, reported through
/// <see cref="VersusMatchReporter"/> as one atomic pair, and driven from the Local Versus screen model.
///
/// Participant identity is the thread through all of it: series first participant is roster slot 0 is
/// runtime Player1 is the first <see cref="GameStats"/>, and the second participant is slot 1 and
/// Player2. Every mapping assertion here is made with scores arranged so that mapping by score order
/// would get it wrong.
/// </summary>
public class Level5LocalSimultaneousVersusTests
{
    private const int ArenaId = 1;
    private const int NonMultiplayerArenaId = 2;
    private const int LockedArenaId = 3;
    private const int UnknownArenaId = 999;
    private const int FirstCharacterId = 1;
    private const int LockedCharacterId = 2;
    private const int SecondCharacterId = 3;

    private InMemoryVersusSeriesRepository repository;
    private List<string> loadedScenes;
    private List<int> preflightRequests;
    private string preflightFailure;
    private Dictionary<int, bool> characterUnlocks;
    private readonly List<GameObject> hosts = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        MatchCatalogs.Override(
            new GameModeCatalog(new[] { TestDefinitions.Mode(GameModeId.TotalPoints) }),
            new LevelDefinitionCatalog(new[]
            {
                TestDefinitions.Level(ArenaId),
                TestDefinitions.Level(NonMultiplayerArenaId, capabilities: ArenaCapability.Basketball),
                TestDefinitions.Level(LockedArenaId, locked: true)
            }));

        VersusCatalogs.Override(VersusTestFixtures.Catalog(
            MostPoints(),
            VersusTestFixtures.ScoreRuleset(
                "alternating-only",
                capabilities: VersusCapability.LocalAlternating | VersusCapability.Asynchronous),
            VersusTestFixtures.ScoreRuleset("async-only", capabilities: VersusCapability.Asynchronous)));

        repository = VersusTestFixtures.Repository();
        VersusRuntime.Override(repository, VersusCatalogs.Rulesets);

        characterUnlocks = new Dictionary<int, bool>
        {
            [FirstCharacterId] = true,
            [LockedCharacterId] = false,
            [SecondCharacterId] = true
        };

        loadedScenes = new List<string>();
        VersusLauncher.OverrideSceneLoader(scene => loadedScenes.Add(scene));

        preflightRequests = new List<int>();
        preflightFailure = null;
        VersusLauncher.OverrideDevicePreflight(count =>
        {
            preflightRequests.Add(count);
            return preflightFailure;
        });

        LocalVersusNavigationState.Clear();
        LocalVersusSimultaneousPicks.Clear();
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
        VersusLauncher.ResetDevicePreflight();
        LocalVersusNavigationState.Clear();
        LocalVersusSimultaneousPicks.Clear();
        ActiveMatch.Clear();
        ActiveVersusAttempt.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();
        MatchCatalogs.Reset();
        PlayerSelectionSession.Clear();
    }

    // ---------------------------------------------------------------- launch: roster and identity

    [Test]
    public void LaunchBuildsAnOrdinaryTwoHumanRosterInSeriesOrder()
    {
        SeriesId id = CreateSimultaneousSeries(3, out VersusSeries created);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "p1char"), Character(SecondCharacterId, "p2char"), ArenaId, Snapshot());

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        PlayerRoster roster = launch.Configuration.Roster;
        Assert.That(roster.Count, Is.EqualTo(2));
        Assert.That(roster.LocalHumanCount, Is.EqualTo(2));
        Assert.That(roster.CpuCount, Is.Zero);
        Assert.That(roster.Players[0].ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
        Assert.That(roster.Players[1].ControlType, Is.EqualTo(PlayerControlType.LocalHuman));
        Assert.That(roster.Players[0].ParticipantId, Is.EqualTo(created.Participants.First.Id.Value), "slot 0 is the first participant");
        Assert.That(roster.Players[1].ParticipantId, Is.EqualTo(created.Participants.Second.Id.Value), "slot 1 is the second participant");
        Assert.That(roster.Players[0].LocalInputSlot, Is.EqualTo(0));
        Assert.That(roster.Players[1].LocalInputSlot, Is.EqualTo(1));
        Assert.That(launch.Configuration.ModeId, Is.EqualTo(GameModeId.TotalPoints));
    }

    [Test]
    public void BothCharacterSelectionsSurviveIntoTheRuntimeRoster()
    {
        SeriesId id = CreateSimultaneousSeries(1, out _);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "p1char"), Character(SecondCharacterId, "p2char"), ArenaId, Snapshot());

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(launch.Configuration.Roster.Players[0].Character.CharacterId, Is.EqualTo(FirstCharacterId));
        Assert.That(launch.Configuration.Roster.Players[0].Character.ObjectName, Is.EqualTo("p1char"));
        Assert.That(launch.Configuration.Roster.Players[1].Character.CharacterId, Is.EqualTo(SecondCharacterId));
        Assert.That(launch.Configuration.Roster.Players[1].Character.ObjectName, Is.EqualTo("p2char"));
        Assert.That(GameOptions.characterObjectNames, Is.EqualTo(new[] { "p1char", "p2char" }),
            "the legacy bridge carries both slots");
        Assert.That(GameOptions.numPlayers, Is.EqualTo(2));
    }

    [Test]
    public void LaunchIssuesStartsAndBindsBothAttemptsToTheExactMatch()
    {
        SeriesId id = CreateSimultaneousSeries(3, out VersusSeries created);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot());

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(launch.Attempt.ParticipantId, Is.EqualTo(created.Participants.First.Id));
        Assert.That(launch.SecondAttempt.ParticipantId, Is.EqualTo(created.Participants.Second.Id));
        Assert.That(launch.Attempt.GameIndex, Is.EqualTo(launch.SecondAttempt.GameIndex), "the same current game");

        Assert.That(ActiveMatch.Configuration, Is.SameAs(launch.Configuration));
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.IsSimultaneous, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        Assert.That(ActiveVersusAttempt.AttemptId, Is.EqualTo(launch.Attempt.Id));
        Assert.That(ActiveVersusAttempt.ParticipantId, Is.EqualTo(created.Participants.First.Id));
        Assert.That(ActiveVersusAttempt.SecondAttemptId, Is.EqualTo(launch.SecondAttempt.Id));
        Assert.That(ActiveVersusAttempt.SecondParticipantId, Is.EqualTo(created.Participants.Second.Id));
        Assert.That(ActiveVersusAttempt.RulesetId.Value, Is.EqualTo("most-points"));
        Assert.That(loadedScenes, Has.Count.EqualTo(1));
        Assert.That(loadedScenes[0], Is.EqualTo(launch.Configuration.SceneName));

        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        Assert.That(stored.ViewFor(stored.Participants.First.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(stored.ViewFor(stored.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started),
            "both attempts are durable and started before the scene loads");
    }

    [Test]
    public void TheContextStopsBeingActiveWhenAnotherMatchBegins()
    {
        SeriesId id = CreateSimultaneousSeries(3, out _);
        Assert.That(VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot()).Succeeded, Is.True);
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);

        ActiveMatch.Begin(BuildOrdinaryMatch());

        Assert.That(ActiveVersusAttempt.IsActive, Is.False,
            "a player who abandoned the game to the menu must not have the next ordinary match submitted as it");
    }

    [Test]
    public void RelaunchingAnOutstandingGameGetsTheSameTwoAttempts()
    {
        SeriesId id = CreateSimultaneousSeries(3, out _);
        VersusLaunch first = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot());

        // the process dies mid-game
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();

        VersusLaunch again = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot());

        Assert.That(again.Succeeded, Is.True, again.Validation?.ToString());
        Assert.That(again.Attempt.Id, Is.EqualTo(first.Attempt.Id));
        Assert.That(again.SecondAttempt.Id, Is.EqualTo(first.SecondAttempt.Id));
        Assert.That(VersusSeriesDocumentReader.AttemptCount(repository.RawDocument(id), 0), Is.EqualTo(2));
    }

    // ---------------------------------------------------------------- launch gates

    [Test]
    public void AnArenaWithoutMultiplayerIsRefusedAndNothingIsSpent()
    {
        LogAssert.Expect(LogType.Warning, new Regex("could not be launched on level " + NonMultiplayerArenaId));
        SeriesId id = CreateSimultaneousSeries(3, out _);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), NonMultiplayerArenaId, Snapshot());

        AssertRefusedBeforeAnyAttempt(id, launch);
    }

    [Test]
    public void ALockedOrUnknownArenaIsRefusedBeforeAnAttemptIsIssued()
    {
        SeriesId id = CreateSimultaneousSeries(3, out _);

        AssertRefusedBeforeAnyAttempt(id, VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), LockedArenaId, Snapshot()));
        AssertRefusedBeforeAnyAttempt(id, VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), UnknownArenaId, Snapshot()));
    }

    [Test]
    public void ALockedOrMissingCharacterOnEitherSideIsRefusedBeforeAnAttemptIsIssued()
    {
        SeriesId id = CreateSimultaneousSeries(3, out _);

        AssertRefusedBeforeAnyAttempt(id, VersusLauncher.LaunchSimultaneous(
            id, Character(LockedCharacterId, "locked"), Character(SecondCharacterId, "b"), ArenaId, Snapshot()));
        AssertRefusedBeforeAnyAttempt(id, VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(LockedCharacterId, "locked"), ArenaId, Snapshot()));
        AssertRefusedBeforeAnyAttempt(id, VersusLauncher.LaunchSimultaneous(
            id, null, Character(SecondCharacterId, "b"), ArenaId, Snapshot()));
        AssertRefusedBeforeAnyAttempt(id, VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), CharacterSelection.None, ArenaId, Snapshot()));
    }

    [Test]
    public void ACharacterThatCannotPlayTheModeIsRefusedByTheOrdinaryBuilderAndNothingIsSpent()
    {
        LogAssert.Expect(LogType.Warning, new Regex("could not be launched on level " + ArenaId));
        SeriesId id = CreateSimultaneousSeries(3, out _);
        CharacterSelection nonShooter = new CharacterSelection(SecondCharacterId, "brawler", "Brawler", false, true);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), nonShooter, ArenaId, Snapshot());

        AssertRefusedBeforeAnyAttempt(id, launch);
    }

    [Test]
    public void ADevicePreflightFailureIsRefusedAndTheLauncherAskedForTwoHumans()
    {
        SeriesId id = CreateSimultaneousSeries(3, out _);
        preflightFailure = "Two local players need two gamepads, or a keyboard plus one gamepad.";

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot());

        AssertRefusedBeforeAnyAttempt(id, launch);
        Assert.That(preflightRequests, Is.EqualTo(new[] { 2 }), "the preflight is still required, for exactly two humans");
        Assert.That(launch.Validation.ToString(), Does.Contain("gamepad"));
    }

    [Test]
    public void ANullUnlockSnapshotFailsClosed()
    {
        SeriesId id = CreateSimultaneousSeries(3, out _);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, null);

        AssertRefusedBeforeAnyAttempt(id, launch);
    }

    [Test]
    public void AnAlternatingSeriesCannotBeLaunchedAsASimultaneousGame()
    {
        SeriesOperation alternating = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator, "A", "B", 3, new RulesetId("most-points"), VersusMode.LocalAlternating);
        Assert.That(alternating.Succeeded, Is.True);

        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            alternating.Series.Id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot());

        Assert.That(launch.Succeeded, Is.False);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(loadedScenes, Is.Empty);
    }

    [Test]
    public void ASimultaneousSeriesCannotBeLaunchedOneParticipantAtATime()
    {
        SeriesId id = CreateSimultaneousSeries(3, out VersusSeries created);

        VersusLaunch launch = VersusLauncher.Launch(
            id, created.Participants.First.Id, ArenaId, Character(FirstCharacterId, "a"), Snapshot());

        Assert.That(launch.Succeeded, Is.False, "there is no designated-first turn in a same-time game");
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
    }

    [Test]
    public void TheAlternatingLaunchIsUnchangedAndSetsNoSimultaneousState()
    {
        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator, "A", "B", 3, new RulesetId("most-points"), VersusMode.LocalAlternating);
        ParticipantId next = LocalVersusFlow.NextParticipant(created.Series);

        VersusLaunch launch = VersusLauncher.Launch(
            created.Series.Id, next, ArenaId, Character(FirstCharacterId, "a"), Snapshot());

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(launch.SecondAttempt, Is.Null);
        Assert.That(launch.Configuration.Roster.Count, Is.EqualTo(1));
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.IsSimultaneous, Is.False);
        Assert.That(ActiveVersusAttempt.SecondAttemptId.HasValue, Is.False);
        Assert.That(ActiveVersusAttempt.SecondParticipantId.HasValue, Is.False);
        Assert.That(preflightRequests, Is.Empty, "a one-human launch never asks for a two-human device plan");
    }

    // ---------------------------------------------------------------- match end: identity and atomic reporting

    [Test]
    public void TheFirstParticipantWinsWhenSlotZeroScoresMore()
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);

        Assert.That(VersusMatchReporter.TryReport(
            BuildStats(40), BuildStats(25), GameModeId.TotalPoints, 60f), Is.True);

        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(created.Participants.First.Id));
        Assert.That(stored.Score.FirstWins, Is.EqualTo(1));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "released once both results are durable");
    }

    [Test]
    public void TheSecondParticipantWinsWhenSlotOneScoresMoreEvenThoughSortedOrderPutsThemFirst()
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);

        // Sorted by score, slot 1 would be listed first. Identity comes from the slot, not the sort.
        Assert.That(VersusMatchReporter.TryReport(
            BuildStats(10), BuildStats(45), GameModeId.TotalPoints, 60f), Is.True);

        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(created.Participants.Second.Id));
        Assert.That(stored.Score.SecondWins, Is.EqualTo(1));
        Assert.That(stored.Score.FirstWins, Is.Zero);
    }

    [Test]
    public void EachParticipantsRecordedResultIsTheirOwnSlotsStats()
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);

        VersusMatchReporter.TryReport(BuildStats(18, made: 6, attempted: 9), BuildStats(33, made: 11, attempted: 12), GameModeId.TotalPoints, 60f);

        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        ParticipantGameView first = stored.Games[0].ViewFor(created.Participants.First.Id, stored.Participants);
        ParticipantGameView second = stored.Games[0].ViewFor(created.Participants.Second.Id, stored.Participants);
        Assert.That(first.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(18f));
        Assert.That(first.OwnResult.Get(AttemptMetric.ShotsMade), Is.EqualTo(6f));
        Assert.That(second.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(33f));
        Assert.That(second.OwnResult.Get(AttemptMetric.ShotsMade), Is.EqualTo(11f));
        Assert.That(first.OpponentResult.Get(AttemptMetric.Score), Is.EqualTo(33f), "revealed together once the game resolved");
    }

    [Test]
    public void ATieIsDecidedByTheExistingComparisonKeysOtherwiseItIsADraw()
    {
        SeriesId byAccuracy = LaunchGame(3, out VersusSeries accuracySeries);
        VersusMatchReporter.TryReport(
            BuildStats(30, made: 10, attempted: 20), BuildStats(30, made: 10, attempted: 12), GameModeId.TotalPoints, 60f);
        Assert.That(VersusRuntime.Coordinator.Load(byAccuracy).Games[0].Result.WinnerId,
            Is.EqualTo(accuracySeries.Participants.Second.Id), "level on points, better accuracy wins");

        ActiveMatch.Clear();
        SeriesId level = LaunchGame(3, out _);
        VersusMatchReporter.TryReport(
            BuildStats(30, made: 10, attempted: 12), BuildStats(30, made: 10, attempted: 12), GameModeId.TotalPoints, 60f);
        GameResult drawn = VersusRuntime.Coordinator.Load(level).Games[0].Result;
        Assert.That(drawn.Kind, Is.EqualTo(GameOutcomeKind.Draw));
        Assert.That(drawn.HasWinner, Is.False);
    }

    [Test]
    public void APersistenceFailureKeepsTheGameActiveAndTheRetryRecordsItOnce()
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);
        AttemptId first = ActiveVersusAttempt.AttemptId;
        AttemptId second = ActiveVersusAttempt.SecondAttemptId;

        repository.FailNextSave = true;
        Assert.That(VersusMatchReporter.TryReport(BuildStats(40), BuildStats(25), GameModeId.TotalPoints, 60f), Is.False,
            "GameRules' match-end retry loop will try again");

        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "not cleared on a failed save");
        Assert.That(ActiveVersusAttempt.AttemptId, Is.EqualTo(first));
        Assert.That(ActiveVersusAttempt.SecondAttemptId, Is.EqualTo(second));
        VersusSeries afterFailure = VersusRuntime.Coordinator.Load(id);
        Assert.That(afterFailure.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "neither result is durable");
        Assert.That(afterFailure.ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));
        Assert.That(afterFailure.ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));

        Assert.That(VersusMatchReporter.TryReport(BuildStats(40), BuildStats(25), GameModeId.TotalPoints, 60f), Is.True);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(VersusRuntime.Coordinator.Load(id).Score.FirstWins, Is.EqualTo(1), "counted exactly once");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AMissingSlotsStatsSubmitsNothingAndLeavesBothAttemptsReplayable(bool firstMissing)
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);
        AttemptId first = ActiveVersusAttempt.AttemptId;
        AttemptId second = ActiveVersusAttempt.SecondAttemptId;

        LogAssert.Expect(LogType.Error, new Regex("were not reported: no stats were available for roster slot " + (firstMissing ? "0" : "1")));
        Assert.That(VersusMatchReporter.TryReport(
            firstMissing ? null : BuildStats(40),
            firstMissing ? BuildStats(25) : null,
            GameModeId.TotalPoints,
            60f), Is.True, "the match must still be able to finish");

        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "no winner was invented for the missing slot");
        Assert.That(stored.Score.FirstWins, Is.Zero);
        Assert.That(stored.Score.SecondWins, Is.Zero);
        Assert.That(stored.ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));
        Assert.That(stored.ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));

        SimultaneousAttemptOperation reissued = VersusRuntime.Coordinator.IssueSimultaneousAttempts(id);
        Assert.That(reissued.Succeeded, Is.True);
        Assert.That(reissued.Attempts.First.Id, Is.EqualTo(first), "the same pair comes back");
        Assert.That(reissued.Attempts.Second.Id, Is.EqualTo(second));
    }

    [Test]
    public void AnOrdinaryMatchIsNotReported()
    {
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);

        Assert.That(VersusMatchReporter.TryReport(BuildStats(40), BuildStats(25), GameModeId.TotalPoints, 60f), Is.True);
        Assert.That(repository.Count, Is.Zero, "nothing was written for a match that is not a series game");
    }

    [Test]
    public void TheSingleAttemptReporterSignatureStillReportsAnAlternatingTurn()
    {
        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator, "A", "B", 3, new RulesetId("most-points"), VersusMode.LocalAlternating);
        ParticipantId next = LocalVersusFlow.NextParticipant(created.Series);
        Assert.That(VersusLauncher.Launch(
            created.Series.Id, next, ArenaId, Character(FirstCharacterId, "a"), Snapshot()).Succeeded, Is.True);

        Assert.That(VersusMatchReporter.TryReport(BuildStats(21), GameModeId.TotalPoints, 60f), Is.True);

        VersusSeries stored = VersusRuntime.Coordinator.Load(created.Series.Id);
        Assert.That(stored.ViewFor(next).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Completed));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
    }

    // ---------------------------------------------------------------- persistence / progression separation

    [Test]
    public void ASimultaneousGameIsCompetitionOnlyAtMatchEnd()
    {
        // The match-end pipeline is scene-bound, so the policy is pinned where it lives: GameRules
        // latches the simultaneous context once, and both primary-player-only pipelines stand down.
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts", "game manager", "GameRules.cs"));

        Assert.That(source, Does.Contain("matchIsCompetitionOnly = ActiveVersusAttempt.IsActive && ActiveVersusAttempt.IsSimultaneous"));
        AssertStandsDown(source, "private bool SaveMatchResults", "DBConnector.instance.savePlayerGameStats");
        AssertStandsDown(source, "private bool SaveMatchResults", "BackendV2MatchResultSubmission.TryQueue");
        AssertStandsDown(source, "private bool SaveMatchResults", "savePlayerAllTimeStats");
        AssertStandsDown(source, "private bool ApplyMatchProgressionResult", "progressionService.ApplyMatchResult");
        Assert.That(source, Does.Contain("GetSecondaryGameStats()"), "both slots' stats are handed over, resolved by slot");
        Assert.That(source, Does.Contain("GameLevelManager.instance.Player2.gameStats"),
            "slot 1 is runtime Player2, not an entry of the sorted list");
    }

    private static void AssertStandsDown(string source, string method, string sink)
    {
        int methodAt = source.IndexOf(method, StringComparison.Ordinal);
        Assert.That(methodAt, Is.GreaterThanOrEqualTo(0), method + " is missing");
        int guardAt = source.IndexOf("if (matchIsCompetitionOnly)", methodAt, StringComparison.Ordinal);
        int sinkAt = source.IndexOf(sink, methodAt, StringComparison.Ordinal);
        Assert.That(guardAt, Is.GreaterThan(methodAt), method + " has no competition-only guard");
        Assert.That(sinkAt, Is.GreaterThan(guardAt), method + " reaches " + sink + " before the competition-only guard");
    }

    // ---------------------------------------------------------------- exit and restart policy

    [Test]
    public void RestartIsRefusedWhileASimultaneousGameIsActive()
    {
        LaunchGame(3, out _);
        MatchController match = BeginPlayingMatch();
        Pause pause = NewBoundPause();
        MatchConfiguration configuration = ActiveMatch.Configuration;

        Assert.DoesNotThrow(() => pause.reloadScene());

        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveMatch.Configuration, Is.SameAs(configuration), "no new match was begun");
        Assert.That(match.IsPlaying, Is.True);
    }

    [Test]
    public void ALeaveRequestMidGameIsRefusedAndNobodyIsForfeited()
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);
        BeginPlayingMatch();
        string before = repository.RawDocument(id);

        Assert.That(VersusQuitPolicy.TurnInProgress, Is.True);
        Assert.That(VersusQuitPolicy.SimultaneousGameInProgress, Is.True);
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "no participant can be attributed the quit");
        Assert.That(VersusQuitPolicy.ForfeitActiveTurn(), Is.False, "slot 0 is not arbitrarily the forfeiting player");

        Assert.That(repository.RawDocument(id), Is.EqualTo(before), "nothing was written");
        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Active));
        Assert.That(stored.ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(stored.ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
    }

    [Test]
    public void TheMenuAndQuitCoroutinesStopBeforeAnyNavigationMidGame()
    {
        LaunchGame(3, out _);
        BeginPlayingMatch();
        Pause pause = NewBoundPause();

        Assert.That(pause.loadstartScreen().MoveNext(), Is.False, "Start/Menu stays put");
        Assert.That(pause.Quit().MoveNext(), Is.False, "Quit stays put");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
    }

    [Test]
    public void AnEndedGameWaitingOnItsResultCannotBeLeftAndOnceRecordedLeavingIsOrdinary()
    {
        SeriesId id = LaunchGame(3, out VersusSeries created);
        MatchController match = BeginPlayingMatch();
        match.RequestEnd(MatchEndReason.TimeExpired);

        Assert.That(VersusQuitPolicy.TurnInProgress, Is.False, "the match has ended");
        Assert.That(VersusQuitPolicy.SimultaneousGameInProgress, Is.False);
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "the result is earned but not yet durable");
        Assert.That(VersusRuntime.Coordinator.Load(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "and not forfeited");

        Assert.That(VersusMatchReporter.TryReport(BuildStats(30), BuildStats(20), GameModeId.TotalPoints, 60f), Is.True);

        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True, "after the result is durable, exit works");
        Assert.That(VersusRuntime.Coordinator.Load(id).Games[0].Result.WinnerId, Is.EqualTo(created.Participants.First.Id));
    }

    [Test]
    public void ACrashLeavesBothAttemptsRecoverableAndNobodyForfeited()
    {
        SeriesId id = LaunchGame(3, out _);
        AttemptId first = ActiveVersusAttempt.AttemptId;
        AttemptId second = ActiveVersusAttempt.SecondAttemptId;

        // the process dies: the quit policy never runs and all transient state is lost
        ActiveVersusAttempt.Clear();
        LocalVersusNavigationState.Clear();
        ActiveMatch.Clear();
        MatchController.instance = null;
        VersusRuntime.Override(repository, VersusCatalogs.Rulesets);

        Assert.That(VersusRuntime.Coordinator.Load(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active));
        SimultaneousAttemptOperation reissued = VersusRuntime.Coordinator.IssueSimultaneousAttempts(id);
        Assert.That(reissued.Succeeded, Is.True, reissued.Validation?.ToString());
        Assert.That(reissued.Attempts.First.Id, Is.EqualTo(first));
        Assert.That(reissued.Attempts.Second.Id, Is.EqualTo(second));
    }

    [Test]
    public void AnAlternatingTurnStillForfeitsOnAQuit()
    {
        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator, "A", "B", 3, new RulesetId("most-points"), VersusMode.LocalAlternating);
        ParticipantId next = LocalVersusFlow.NextParticipant(created.Series);
        Assert.That(VersusLauncher.Launch(
            created.Series.Id, next, ArenaId, Character(FirstCharacterId, "a"), Snapshot()).Succeeded, Is.True);
        BeginPlayingMatch();

        Assert.That(VersusQuitPolicy.SimultaneousGameInProgress, Is.False);
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True);

        Assert.That(VersusRuntime.Coordinator.Load(created.Series.Id).Games[0].Status, Is.EqualTo(VersusGameStatus.Forfeited));
    }

    [Test]
    public void ThePauseBindingCarriesTheSimultaneousReader()
    {
        string source = File.ReadAllText(Path.Combine(
            Application.dataPath, "Scripts", "game manager", "GameLevelManager.cs"));

        Assert.That(source, Does.Contain("VersusQuitPolicy.SimultaneousGameInProgress"));
    }

    // ---------------------------------------------------------------- Local Versus screen model

    [Test]
    public void TheModeSelectorDistinguishesAlternatingFromSimultaneous()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();

        Assert.That(model.CreateMode, Is.EqualTo(VersusMode.LocalAlternating), "the screen opens on the mode it always had");
        Assert.That(model.ModeText, Is.EqualTo("Mode: Local Alternating"));

        model.CycleMode();
        Assert.That(model.CreateMode, Is.EqualTo(VersusMode.LocalSimultaneous));
        Assert.That(model.ModeText, Is.EqualTo("Mode: Local Simultaneous"));

        model.CycleMode();
        Assert.That(model.CreateMode, Is.EqualTo(VersusMode.LocalAlternating));
    }

    [Test]
    public void RulesetsAreFilteredThroughTheCapabilityOfTheChosenMode()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        Assert.That(model.Rulesets.Select(ruleset => ruleset.Id.Value), Is.EqualTo(new[] { "most-points", "alternating-only" }));

        model.CycleMode();

        Assert.That(model.Rulesets.Select(ruleset => ruleset.Id.Value), Is.EqualTo(new[] { "most-points" }),
            "only a ruleset that declares LocalSimultaneous is offered for simultaneous play");
        Assert.That(model.RulesetText, Is.EqualTo("Ruleset: Most Points"));
    }

    [Test]
    public void TheShippedCatalogOffersOnlyMostPointsForSimultaneousPlay()
    {
        List<CompetitiveRuleset> shipped = LocalVersusFlow.SelectableRulesets(
            new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll()), VersusMode.LocalSimultaneous);

        Assert.That(shipped.Select(ruleset => ruleset.Id.Value), Is.EqualTo(new[] { "most-points" }));
        Assert.That(
            LocalVersusFlow.SelectableRulesets(new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll())).Count,
            Is.GreaterThan(1),
            "alternating play still offers its full list");
    }

    [Test]
    public void CreatingInSimultaneousModeStoresASealedLocalSimultaneousSeries()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();

        SeriesOperation created = model.Create("Ann", "Ben");

        Assert.That(created.Succeeded, Is.True, created.Validation?.ToString());
        VersusSeries stored = VersusRuntime.Coordinator.Load(created.Series.Id);
        Assert.That(stored.Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
        Assert.That(stored.Snapshot.InformationPolicy, Is.EqualTo(InformationPolicy.SealedAttempt));
        Assert.That(stored.Snapshot.GameAt(0).Id.Value, Is.EqualTo("most-points"));
        Assert.That(model.IsSimultaneousSelected, Is.True, "a created series is selected");
        Assert.That(model.SelectedSeries.Id, Is.EqualTo(created.Series.Id));
    }

    [Test]
    public void AnAlternatingSeriesKeepsItsNextParticipantSingleSelectorAndPlayTurn()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.Create("Ann", "Ben");

        Assert.That(model.IsSimultaneousSelected, Is.False);
        Assert.That(model.NextParticipant.HasValue, Is.True);
        Assert.That(model.CanPlayTurn, Is.True);
        Assert.That(model.PlayTurnText, Does.StartWith("Play Turn: "));
        Assert.That(model.CharacterText, Does.StartWith("Character: "));
        Assert.That(model.DetailText, Does.Contain(" is up."));

        VersusLaunch launch = model.PlayTurn();
        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(launch.Configuration.Roster.Count, Is.EqualTo(1));
        Assert.That(ActiveVersusAttempt.IsSimultaneous, Is.False);
    }

    [Test]
    public void ASimultaneousSeriesHasNoNextParticipantAndTwoCharacterSelections()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        model.Create("Ann", "Ben");

        Assert.That(model.IsSimultaneousSelected, Is.True);
        Assert.That(model.NextParticipant.HasValue, Is.False, "there is no turn order in a same-time game");
        Assert.That(model.HasPlayableTurn, Is.True);
        Assert.That(model.CanPlayTurn, Is.True);
        Assert.That(model.PlayTurnText, Is.EqualTo("Play Game"));
        Assert.That(model.SelectedCharacter, Is.Not.Null);
        Assert.That(model.SelectedSecondCharacter, Is.Not.Null);
        Assert.That(model.CharacterText, Does.StartWith("Player 1 (Ann): "));
        Assert.That(model.SecondCharacterText, Does.StartWith("Player 2 (Ben): "));
        Assert.That(model.DetailText, Does.Contain("Both players play at once."));
        Assert.That(model.DetailText, Does.Not.Contain(" is up."));
        Assert.That(model.Characters.All(option => option.IsUnlocked), Is.True, "locked characters are not offered");
    }

    [Test]
    public void EachPlayersCharacterCyclesIndependently()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        model.Create("Ann", "Ben");
        CharacterSelectOption firstBefore = model.SelectedCharacter;
        CharacterSelectOption secondBefore = model.SelectedSecondCharacter;

        model.CycleSecondCharacter();

        Assert.That(model.SelectedCharacter, Is.SameAs(firstBefore), "Player 1's pick is untouched");
        Assert.That(model.SelectedSecondCharacter, Is.Not.SameAs(secondBefore));

        model.CycleCharacter();
        Assert.That(model.SelectedCharacter, Is.Not.SameAs(firstBefore));
    }

    [Test]
    public void BothPlayersPicksSurviveTheScreenBeingRebuiltBetweenGames()
    {
        // Every gameplay scene returns to a freshly loaded Local Versus scene with a brand new model,
        // so a pick held only on the model would be lost after each game of a Best of N.
        LocalVersusScreenModel first = NewModel();
        first.Open();
        first.CycleMode();
        SeriesId id = first.Create("Ann", "Ben").Series.Id;
        first.CycleCharacter();
        first.CycleSecondCharacter();
        int onePick = first.SelectedCharacter.CharacterId;
        int twoPick = first.SelectedSecondCharacter.CharacterId;
        Assert.That(first.PlayTurn().Succeeded, Is.True);
        Assert.That(VersusMatchReporter.TryReport(BuildStats(40), BuildStats(20), GameModeId.TotalPoints, 60f), Is.True);

        LocalVersusScreenModel rebuilt = NewModel();
        rebuilt.Open();
        rebuilt.Select(id);

        Assert.That(rebuilt.SelectedSeries.CurrentGame.Index, Is.EqualTo(1));
        Assert.That(rebuilt.SelectedCharacter.CharacterId, Is.EqualTo(onePick), "Player 1's pick survived the rebuild");
        Assert.That(rebuilt.SelectedSecondCharacter.CharacterId, Is.EqualTo(twoPick), "Player 2's pick survived the rebuild");
    }

    [Test]
    public void AnotherSeriesStartsFromTheDefaultsNotTheLastSeriesPicks()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        model.Create("Ann", "Ben");
        model.CycleCharacter();
        model.CycleSecondCharacter();
        LocalVersusSimultaneousPicks.Remember(model.SelectedSeries.Id, model.SelectedCharacter.CharacterId, model.SelectedSecondCharacter.CharacterId);

        model.Create("Cy", "Di");

        Assert.That(model.SelectedCharacter.CharacterId, Is.EqualTo(FirstCharacterId));
        Assert.That(model.SelectedSecondCharacter.CharacterId, Is.EqualTo(SecondCharacterId));
    }

    [Test]
    public void ARecalledCharacterThatIsNoLongerOfferedKeepsTheDefault()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;
        LocalVersusSimultaneousPicks.Remember(id, LockedCharacterId, 12345);

        LocalVersusScreenModel rebuilt = NewModel();
        rebuilt.Open();
        rebuilt.Select(id);

        Assert.That(rebuilt.SelectedCharacter.IsUnlocked, Is.True);
        Assert.That(rebuilt.SelectedSecondCharacter.IsUnlocked, Is.True);
        Assert.That(rebuilt.CanPlayTurn, Is.True);
    }

    [Test]
    public void OnlyMultiplayerArenasAreOfferedForAGameOfTwoHumans()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.Create("Alt", "Alt2");
        Assert.That(model.Levels.Select(level => level.LevelId), Does.Contain(NonMultiplayerArenaId),
            "an alternating turn is one human, so any compatible arena will do");

        model.CycleMode();
        model.Create("Ann", "Ben");

        Assert.That(model.Levels.Select(level => level.LevelId), Is.EqualTo(new[] { ArenaId }));
    }

    [Test]
    public void PlayingASimultaneousSeriesLaunchesBothParticipantsWithTheirOwnCharacters()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;
        VersusSeries stored = VersusRuntime.Coordinator.Load(id);
        CharacterSelectOption one = model.SelectedCharacter;
        CharacterSelectOption two = model.SelectedSecondCharacter;
        Assert.That(one.CharacterId, Is.Not.EqualTo(two.CharacterId), "a fresh game does not default to two of the same");

        VersusLaunch launch = model.PlayTurn();

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        PlayerRoster roster = launch.Configuration.Roster;
        Assert.That(roster.Count, Is.EqualTo(2));
        Assert.That(roster.Players[0].ParticipantId, Is.EqualTo(stored.Participants.First.Id.Value));
        Assert.That(roster.Players[1].ParticipantId, Is.EqualTo(stored.Participants.Second.Id.Value));
        Assert.That(roster.Players[0].Character.CharacterId, Is.EqualTo(one.CharacterId));
        Assert.That(roster.Players[1].Character.CharacterId, Is.EqualTo(two.CharacterId));
        Assert.That(launch.Configuration.Level.LevelId, Is.EqualTo(ArenaId));
        Assert.That(ActiveVersusAttempt.IsSimultaneous, Is.True);
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True, "the summary returns to the Local Versus screen");
        Assert.That(LocalVersusNavigationState.PreferredSeriesId, Is.EqualTo(id));
    }

    [Test]
    public void AFailedSimultaneousLaunchIsReportedAndLeavesNoReturnHint()
    {
        preflightFailure = "Two local players need two gamepads, or a keyboard plus one gamepad.";
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        model.Create("Ann", "Ben");

        VersusLaunch launch = model.PlayTurn();

        Assert.That(launch.Succeeded, Is.False);
        Assert.That(model.TurnMessage, Does.Contain("Could not start the game"));
        Assert.That(model.TurnMessage, Does.Contain("gamepad"));
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
    }

    [Test]
    public void ABestOf3ResumesCorrectlyAfterEachGameAndSurvivesARestart()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        model.CycleMode();
        SeriesId id = model.Create("Ann", "Ben").Series.Id;
        VersusSeries stored = VersusRuntime.Coordinator.Load(id);

        // game 1: Player 1 wins
        Assert.That(model.PlayTurn().Succeeded, Is.True);
        CharacterSelectOption secondPick = model.SelectedSecondCharacter;
        Assert.That(VersusMatchReporter.TryReport(BuildStats(40), BuildStats(20), GameModeId.TotalPoints, 60f), Is.True);

        // back on the screen: the same series, now on game 2, with the picks kept
        model.Refresh(id);
        Assert.That(model.SelectedSeries.CurrentGame.Index, Is.EqualTo(1));
        Assert.That(model.CanPlayTurn, Is.True);
        Assert.That(model.SelectedSecondCharacter.CharacterId, Is.EqualTo(secondPick.CharacterId));
        Assert.That(model.DetailText, Does.Contain("Game 2 of 3"));
        Assert.That(model.DetailText, Does.Contain("Ann 1 - 0 Ben"));

        // process restart between games: nothing in memory survives but the stored series
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        LocalVersusNavigationState.Clear();
        VersusRuntime.Reset();
        VersusRuntime.Override(repository, VersusCatalogs.Rulesets);
        LocalVersusScreenModel reopened = NewModel();
        reopened.Open();
        Assert.That(reopened.SelectedSeries.Id, Is.EqualTo(id));
        Assert.That(reopened.IsSimultaneousSelected, Is.True);
        Assert.That(reopened.CanPlayTurn, Is.True);

        // game 2: Player 2 wins, so the series is level; game 3: Player 1 wins and takes it
        Assert.That(reopened.PlayTurn().Succeeded, Is.True);
        Assert.That(VersusMatchReporter.TryReport(BuildStats(10), BuildStats(50), GameModeId.TotalPoints, 60f), Is.True);
        reopened.Refresh(id);
        Assert.That(reopened.SelectedSeries.CurrentGame.Index, Is.EqualTo(2));
        Assert.That(reopened.PlayTurn().Succeeded, Is.True);
        Assert.That(VersusMatchReporter.TryReport(BuildStats(60), BuildStats(30), GameModeId.TotalPoints, 60f), Is.True);
        reopened.Refresh(id);

        Assert.That(reopened.SelectedSeries.IsOver, Is.True);
        Assert.That(reopened.SelectedSeries.Result.WinnerId, Is.EqualTo(stored.Participants.First.Id));
        Assert.That(reopened.CanPlayTurn, Is.False, "a finished series offers nothing to play");
        Assert.That(reopened.DetailText, Does.Contain("Ann wins the series."));

        // and the completed series and its history survive another restart
        VersusRuntime.Reset();
        VersusRuntime.Override(repository, VersusCatalogs.Rulesets);
        LocalVersusScreenModel afterRestart = NewModel();
        afterRestart.Open();
        Assert.That(afterRestart.Summaries.Select(summary => summary.Id), Does.Contain(id));
        Assert.That(afterRestart.SelectedSummary.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(afterRestart.SelectedSeries.Score.FirstWins, Is.EqualTo(2));
        Assert.That(afterRestart.SelectedSeries.Score.SecondWins, Is.EqualTo(1));
    }

    [Test]
    public void TheListShowsAlternatingAndSimultaneousSeriesTogether()
    {
        LocalVersusScreenModel model = NewModel();
        model.Open();
        SeriesId alternating = model.Create("Alt", "Two").Series.Id;
        model.CycleMode();
        SeriesId simultaneous = model.Create("Sim", "Two").Series.Id;

        model.Refresh();

        Assert.That(model.Summaries.Select(summary => summary.Id), Is.EquivalentTo(new[] { alternating, simultaneous }));
        Assert.That(
            model.Summaries.Select(summary => summary.Mode),
            Is.EquivalentTo(new[] { VersusMode.LocalAlternating, VersusMode.LocalSimultaneous }));
    }

    [Test]
    public void ACorrespondenceSeriesIsStillNotListedOnTheLocalScreen()
    {
        SeriesOperation asynchronous = VersusRuntime.Coordinator.CreateSeries(VersusTestFixtures.Request(
            SeriesFormat.BestOf1,
            VersusTestFixtures.Playlist(SeriesFormat.BestOf1, new RulesetId("most-points")),
            VersusMode.Asynchronous));
        Assert.That(asynchronous.Succeeded, Is.True);

        List<SeriesSummary> listed = LocalVersusFlow.ListLocal(VersusRuntime.Coordinator);

        Assert.That(listed, Is.Empty);
    }

    // ---------------------------------------------------------------- helpers

    private static CompetitiveRuleset MostPoints()
    {
        return new CompetitiveRuleset(
            new RulesetId("most-points"),
            1,
            GameModeId.TotalPoints,
            VersusCapability.LocalSimultaneous | VersusCapability.LocalAlternating | VersusCapability.Asynchronous,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Highest(AttemptMetric.Accuracy),
                ComparisonKey.Lowest(AttemptMetric.ShotsAttempted)
            },
            1,
            "Most Points");
    }

    private LocalVersusScreenModel NewModel()
    {
        return new LocalVersusScreenModel(Snapshot, _ => new List<CharacterSelectOption>
        {
            Option(FirstCharacterId, "Dr Blood", true),
            Option(LockedCharacterId, "Locked One", false),
            Option(SecondCharacterId, "Second", true)
        });
    }

    private UnlockSnapshot Snapshot()
    {
        return new UnlockSnapshot(
            characterUnlocks,
            new Dictionary<int, bool>
            {
                [ArenaId] = true,
                [NonMultiplayerArenaId] = true,
                [LockedArenaId] = false
            });
    }

    private static CharacterSelectOption Option(int id, string name, bool unlocked)
    {
        return new CharacterSelectOption(id, name, name.ToLowerInvariant().Replace(" ", string.Empty), true, true, unlocked, null);
    }

    private static CharacterSelection Character(int id, string objectName)
    {
        return new CharacterSelection(id, objectName, objectName, true, true);
    }

    private SeriesId CreateSimultaneousSeries(int gameCount, out VersusSeries series)
    {
        SeriesOperation created = LocalVersusFlow.CreateSeries(
            VersusRuntime.Coordinator, "Ann", "Ben", gameCount, new RulesetId("most-points"), VersusMode.LocalSimultaneous);
        Assert.That(created.Succeeded, Is.True, created.Validation?.ToString());
        series = created.Series;
        return created.Series.Id;
    }

    private SeriesId LaunchGame(int gameCount, out VersusSeries created)
    {
        SeriesId id = CreateSimultaneousSeries(gameCount, out created);
        VersusLaunch launch = VersusLauncher.LaunchSimultaneous(
            id, Character(FirstCharacterId, "a"), Character(SecondCharacterId, "b"), ArenaId, Snapshot());
        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        return id;
    }

    private void AssertRefusedBeforeAnyAttempt(SeriesId id, VersusLaunch launch)
    {
        Assert.That(launch.Succeeded, Is.False);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(ActiveMatch.IsActive, Is.False, "no match was begun");
        Assert.That(loadedScenes, Is.Empty, "no scene was loaded");
        Assert.That(VersusSeriesDocumentReader.AttemptCount(repository.RawDocument(id), 0), Is.Zero,
            "nothing was spent on an attempt for a game that could not start");
    }

    private static MatchConfiguration BuildOrdinaryMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(ArenaId);
        PlayerRoster roster = TestDefinitions.SoloRoster();
        return new MatchConfiguration(
            mode,
            level,
            roster,
            MatchModifiers.Default,
            MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
            CheerleaderSelection.None,
            "simultaneous versus test");
    }

    private GameStats BuildStats(int totalPoints, int made = 7, int attempted = 10)
    {
        GameObject host = new GameObject("simultaneous-stats");
        hosts.Add(host);
        GameStats stats = host.AddComponent<GameStats>();
        stats.TotalPoints = totalPoints;
        stats.ShotMade = made;
        stats.ShotAttempt = attempted;
        return stats;
    }

    private MatchController BeginPlayingMatch()
    {
        GameObject host = new GameObject("simultaneous-match");
        hosts.Add(host);
        MatchController match = host.AddComponent<MatchController>();
        MatchController.instance = match;
        match.BeginPlay();
        Assert.That(match.IsPlaying, Is.True);
        return match;
    }

    /// <summary>A <see cref="Pause"/> wired to the quit policy exactly as GameLevelManager wires it.</summary>
    private Pause NewBoundPause()
    {
        GameObject host = new GameObject("simultaneous-pause");
        hosts.Add(host);
        Pause pause = host.AddComponent<Pause>();
        pause.BindVersusContext(
            () => VersusQuitPolicy.AttemptOutstanding,
            () => VersusQuitPolicy.TurnInProgress,
            VersusQuitPolicy.TryPrepareForExplicitExit,
            () => VersusQuitPolicy.SimultaneousGameInProgress);
        return pause;
    }
}
