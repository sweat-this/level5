#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Opt-in, process-level certification of the production Local Versus loop (docs/versus-architecture.md,
/// "Local Versus certification"): what survives a genuine process boundary, and the difference between
/// leaving a match on purpose and the process dying under it.
///
/// <see cref="LocalVersusProductionSmokePlayModeTests"/> proves the same behaviors by resetting runtime
/// statics and rebuilding a <see cref="FileVersusSeriesRepository"/> inside one process. That is not a
/// restart. Each session here is its own <c>Unity.exe -runTests</c> invocation, so nothing survives
/// between two sessions except what the repository put on disk.
///
/// Skipped (<c>Assert.Ignore</c>) unless <c>LEVEL5_LOCAL_VERSUS_CERTIFICATION=1</c>, so ordinary
/// <c>-runTests</c> runs never depend on it. It also refuses to run unless the project's product name is
/// the isolated certification one (see <see cref="RequireIsolatedPersistence"/>): the real
/// <c>GameRules</c> match-end path writes progression and score state under
/// <c>Application.persistentDataPath</c>, and a certification run must never do that to a developer's
/// ordinary data.
///
/// Environment (read at runtime):
/// <list type="bullet">
/// <item><c>LEVEL5_LOCAL_VERSUS_CERT_ROOT</c> - the controlled temporary repository directory shared only by
/// the sessions of one certification run. The series documents in it are the sole authority.</item>
/// <item><c>LEVEL5_LOCAL_VERSUS_CERT_HANDOFF</c> - a small text file outside that directory. It carries only
/// what session 2 needs to cross-check against (a series id, display names, an attempt id) - never a
/// serialized series, and no session reads competitive state from it.</item>
/// </list>
///
/// Run each method as its own process, in order. Before the first one, set the project's product name to
/// <c>level5-local-versus-cert-editor</c> (<c>ProjectSettings/ProjectSettings.asset</c> <c>productName</c>; restore it with
/// <c>git checkout</c> afterwards) so <c>Application.persistentDataPath</c> is not a developer's data directory, and
/// set the two environment variables above (and <c>LEVEL5_LOCAL_VERSUS_CERT_ALLOW_KILL=1</c> for <c>SessionC1</c> only):
/// <code>
/// Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode ^
///     -testFilter LocalVersusProcessCertificationPlayModeTests.Session1_CreateBestOf3AndCompleteOneAttempt ...
/// </code>
/// <c>SessionC1</c> ends by killing its own process and therefore never writes a results file; that is the
/// point of it.
///
/// Local simultaneous (Most Points, two humans, one shared match) has its own sessions, with the same rules:
/// <c>SessionS1</c> / <c>S2</c> / <c>S3</c> (create, play one shared game and exit; restore in a fresh process,
/// verify game one and finish the series; restore the completed series in a third), and <c>SessionSC1</c> (kills
/// its own process with a shared game live and both attempts Started; also needs the kill opt-in) / <c>SC2</c>
/// (fresh process: both original attempts reused, nobody forfeited, exactly one durable result). They seat two
/// virtual gamepads for the launcher's two-human preflight; that is not physical-device certification.
/// <c>ASimultaneousMatchEndRetriesAtomicallyAndWritesOnlyTheSeries</c> is a single-process test of the real
/// <c>GameRules</c> match end and its persistence exclusions, and <c>Layout_SimultaneousStatesStayOnScreenAndUntruncated</c>
/// is the simultaneous counterpart of the rendered layout check.
///
/// Drives the production Start and Local Versus scenes exactly as the smoke fixture does. The default-
/// assembly types (<c>Pause</c>, the controller, <c>GameRules</c>) are reached by name for the same reason.
/// </summary>
public class LocalVersusProcessCertificationPlayModeTests
{
    private const string EnabledEnvVar = "LEVEL5_LOCAL_VERSUS_CERTIFICATION";
    private const string RootEnvVar = "LEVEL5_LOCAL_VERSUS_CERT_ROOT";
    private const string HandoffEnvVar = "LEVEL5_LOCAL_VERSUS_CERT_HANDOFF";
    private const string AllowKillEnvVar = "LEVEL5_LOCAL_VERSUS_CERT_ALLOW_KILL";

    /// <summary>Same prefix as <c>LocalVersusCertificationBuild.CertificationProductName</c> (an Editor-only type).</summary>
    private const string IsolatedProductNamePrefix = "level5-local-versus-cert";

    private const float SceneTimeoutSeconds = 90f;

    private string root;
    private InputTestFixture input;
    private Gamepad padA;
    private Gamepad padB;

    [SetUp]
    public void SetUp()
    {
        if (Environment.GetEnvironmentVariable(EnabledEnvVar) != "1")
        {
            Assert.Ignore("Set " + EnabledEnvVar + "=1 (and " + RootEnvVar + ") to run the Local Versus process certification.");
        }

        RequireIsolatedPersistence();

        root = Environment.GetEnvironmentVariable(RootEnvVar);
        Assert.That(root, Is.Not.Null.And.Not.Empty, RootEnvVar + " must name the shared repository directory");

        // Constructed for this process only: a fresh process starts with VersusRuntime unset, and this
        // is the whole of its initialization - the coordinator, catalog and screens are the production ones.
        VersusRuntime.Override(new FileVersusSeriesRepository(root));
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();

        if (input != null)
        {
            // the gameplay scene reads the virtual gamepads every frame: it goes before they do
            yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("local-versus-cert-blank");
            input.TearDown();
            input = null;
        }
    }

    // ================================================================= process restart between turns

    /// <summary>
    /// Process 1: open Local Versus, create a Best-of-3, play one participant's attempt through the real
    /// turn launch, and confirm the series document holds it before this process ends.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator Session1_CreateBestOf3AndCompleteOneAttempt()
    {
        Assert.That(Directory.Exists(root) ? Directory.GetFiles(root).Length : 0, Is.EqualTo(0), "session 1 needs an empty repository");

        yield return OpenLocalVersusFromStart();
        SetInput("player1NameInputField", "Alice");
        SetInput("player2NameInputField", "Bob");
        FindButton("createButton").onClick.Invoke();
        yield return null;

        Assert.That(new FileVersusSeriesRepository(root).ListSummaries(), Has.Count.EqualTo(1), "Create stored exactly one series");
        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;
        VersusSeries created = Durable(id);
        Assert.That(created.Snapshot.GameCount, Is.EqualTo(3));
        Assert.That(created.Mode, Is.EqualTo(VersusMode.LocalAlternating));

        ParticipantId player = ExpectedNext(created);
        string playerName = created.Participants.Find(player).DisplayName;
        string otherName = created.Participants.Opponent(player).DisplayName;

        yield return LaunchTurn(id, player);
        AttemptId attempt = ActiveVersusAttempt.AttemptId;
        Assert.That(
            Durable(id).ViewFor(player).CurrentGame.OwnAttemptState,
            Is.EqualTo(AttemptState.Started),
            "the launch was durable before gameplay began");

        ReportResult(score: 30);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "the stored result cleared the attempt");

        VersusSeries stored = Durable(id);
        Assert.That(stored.ViewFor(player).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Completed));
        Assert.That(stored.IsActive, Is.True, "one attempt does not decide a game");

        WriteHandoff(new Dictionary<string, string>
        {
            ["series"] = id.Value,
            ["played"] = playerName,
            ["expectedNext"] = otherName,
            ["attempt"] = attempt.Value
        });
        Log("SESSION1 OK series=" + id + " played=" + playerName + " expectedNext=" + otherName + " gameIndex=" + stored.CurrentGame.Index);
    }

    /// <summary>
    /// Process 2 (fresh): the repository is the only thing that carries over. The screen must list the
    /// stored series, name the correct next participant, and let the series continue.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator Session2_ResumeInFreshProcessAndContinueTheSeries()
    {
        Dictionary<string, string> handoff = ReadHandoff();

        // fresh process: nothing was created or played here yet
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1), "the series survived the process boundary on disk");
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        Assert.That(id.Value, Is.EqualTo(handoff["series"]), "the located series is the one session 1 made (cross-check only)");

        VersusSeries stored = Durable(id);
        ParticipantId next = ExpectedNext(stored);
        Assert.That(stored.Participants.Find(next).DisplayName, Is.EqualTo(handoff["expectedNext"]));
        Assert.That(stored.CurrentGame.Index, Is.EqualTo(0), "still game 1");
        Assert.That(stored.ViewFor(next).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));

        yield return OpenLocalVersusFromStart();
        Assert.That(TextOf("seriesDetail"), Does.Contain(handoff["expectedNext"] + " is up."), "the restored screen names the same next side");
        Assert.That(FindButton("playTurnButton").interactable, Is.True);
        Assert.That(TextOf("playTurnButton"), Does.Contain(handoff["expectedNext"]));

        yield return LaunchTurn(id, next);
        ReportResult(score: 10);

        VersusSeries after = Durable(id);
        Assert.That(after.Games[0].IsResolved, Is.True, "both attempts are in, so game 1 resolved");
        Assert.That(after.Score.FirstWins + after.Score.SecondWins, Is.EqualTo(1));
        Assert.That(after.Games[0].Result.WinnerId, Is.EqualTo(after.Participants.Find(after.Games[0].Result.WinnerId).Id));

        yield return ContinueSeries();
        Assert.That(TextOf("seriesDetail"), Does.Contain("Game 2 of 3"), "Continue Series lands on the next game");
        Log("SESSION2 OK series=" + id + " score=" + after.Score.FirstWins + "-" + after.Score.SecondWins + " nowGame=" + (after.CurrentGame.Index + 1));
    }

    // ================================================================= deliberate quit (player-side verification)

    /// <summary>
    /// After a real standalone player quit a live turn from its pause menu (driven from outside with real
    /// input), reads the player's own repository and checks the forfeit is durable and final.
    /// <c>LEVEL5_LOCAL_VERSUS_CERT_ROOT</c> names the player's <c>versus</c> folder.
    /// </summary>
    [UnityTest]
    [Timeout(300000)]
    public IEnumerator SessionQ2_VerifyAPlayerQuitLeftADurableForfeitAndNoFreeRetake()
    {
        Dictionary<string, string> handoff = ReadHandoff();
        Assert.That(new FileVersusSeriesRepository(root).ListSummaries(), Has.Count.EqualTo(1));
        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;
        Assert.That(id.Value, Is.EqualTo(handoff["series"]));

        VersusSeries stored = Durable(id);
        ParticipantId quitter = FindParticipant(stored, handoff["quitter"]);
        ParticipantId opponent = stored.Participants.Opponent(quitter).Id;

        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Forfeited), "the quit game is forfeited");
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(opponent), "the opponent wins the game the player quit");
        Assert.That(stored.Score.FirstWins + stored.Score.SecondWins, Is.EqualTo(1));
        Assert.That(stored.Status, Is.EqualTo(SeriesStatus.Active), "one quit game does not end a best of three");
        Assert.That(stored.CurrentGame.Index, Is.EqualTo(1), "the series advanced to game 2");

        ParticipantSeriesView quitterView = stored.ViewFor(quitter);
        Assert.That(quitterView.Games[0].OwnAttemptState, Is.Not.EqualTo(AttemptState.Started), "no outstanding attempt on the forfeited game");
        Assert.That(quitterView.Games[0].OwnAttemptState, Is.Not.EqualTo(AttemptState.Ready));
        foreach (ParticipantGameView game in stored.ViewFor(stored.Participants.First.Id).Games)
        {
            Assert.That(game.OwnAttemptState, Is.Not.EqualTo(AttemptState.Started));
        }

        foreach (ParticipantGameView game in stored.ViewFor(stored.Participants.Second.Id).Games)
        {
            Assert.That(game.OwnAttemptState, Is.Not.EqualTo(AttemptState.Started));
        }

        // and the relaunched screen cannot hand the forfeited game back: it offers game 2
        yield return OpenLocalVersusFromStart();
        Assert.That(TextOf("seriesDetail"), Does.Contain("Game 2 of 3"));
        Assert.That(
            TextOf("seriesDetail"),
            Does.Contain(stored.Participants.First.DisplayName + " " + stored.Score.FirstWins + " - "
                + stored.Score.SecondWins + " " + stored.Participants.Second.DisplayName));
        Log("SESSIONQ2 OK series=" + id + " game1=" + stored.Games[0].Status + " winner=" + stored.Participants.Find(stored.Games[0].Result.WinnerId).DisplayName
            + " score=" + stored.Score.FirstWins + "-" + stored.Score.SecondWins + " nowGame=" + (stored.CurrentGame.Index + 1));
    }

    // ================================================================= unexpected termination

    /// <summary>
    /// Process 1: start a live turn from the real screen, then die without going through any pause exit.
    /// This method never returns: it kills its own process once the started attempt is confirmed durable.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SessionC1_StartATurnThenTheProcessIsKilled()
    {
        // This session kills the process running it, so it needs its own opt-in on top of the fixture's: running
        // the whole fixture in one process must never end that way.
        if (Environment.GetEnvironmentVariable(AllowKillEnvVar) != "1")
        {
            Assert.Ignore("Set " + AllowKillEnvVar + "=1 to run the session that kills its own process.");
        }

        Assert.That(Directory.Exists(root) ? Directory.GetFiles(root).Length : 0, Is.EqualTo(0), "session C1 needs an empty repository");

        yield return OpenLocalVersusFromStart();
        SetInput("player1NameInputField", "Alice");
        SetInput("player2NameInputField", "Bob");
        FindButton("createButton").onClick.Invoke();
        yield return null;

        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;
        ParticipantId player = ExpectedNext(Durable(id));
        string playerName = Durable(id).Participants.Find(player).DisplayName;

        yield return LaunchTurn(id, player);
        Assert.That(VersusQuitPolicy.TurnInProgress, Is.True, "a live turn, not yet finished");

        VersusSeries stored = Durable(id);
        Assert.That(stored.ViewFor(player).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        AttemptId attempt = stored.ViewFor(player).CurrentGame.OwnAttemptId;

        WriteHandoff(new Dictionary<string, string>
        {
            ["series"] = id.Value,
            ["participant"] = playerName,
            ["attempt"] = attempt.Value
        });
        Log("SESSIONC1 turn live series=" + id + " participant=" + playerName + " attempt=" + attempt + " - killing process now, no pause action taken");

        yield return new WaitForSecondsRealtime(1f);
        Process.GetCurrentProcess().Kill();
        yield return new WaitForSecondsRealtime(30f);
        Assert.Fail("the process should have been killed");
    }

    /// <summary>
    /// Process 2 (fresh): the killed process must not have cost the participant their turn - and must not
    /// have handed the game to the opponent either.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SessionC2_RelaunchKeepsTheOutstandingAttemptRetryableWithNoForfeit()
    {
        Dictionary<string, string> handoff = ReadHandoff();

        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1));
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        Assert.That(id.Value, Is.EqualTo(handoff["series"]));

        VersusSeries stored = Durable(id);
        ParticipantId player = FindParticipant(stored, handoff["participant"]);

        Assert.That(stored.CurrentGame.Index, Is.EqualTo(0), "same game");
        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "no forfeit was recorded");
        Assert.That(stored.Score.FirstWins + stored.Score.SecondWins + stored.Score.Draws, Is.EqualTo(0), "no game was awarded");
        Assert.That(stored.ViewFor(player).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started), "the same Started attempt is still outstanding");
        Assert.That(stored.ViewFor(player).CurrentGame.OwnAttemptId.Value, Is.EqualTo(handoff["attempt"]));
        Assert.That(ExpectedNext(stored), Is.EqualTo(player), "the same participant is still up");

        yield return OpenLocalVersusFromStart();
        Assert.That(TextOf("seriesDetail"), Does.Contain(handoff["participant"] + " is up."));
        Assert.That(FindButton("playTurnButton").interactable, Is.True, "retry is permitted");

        yield return LaunchTurn(id, player);
        Assert.That(ActiveVersusAttempt.AttemptId.Value, Is.EqualTo(handoff["attempt"]), "the retry is the outstanding attempt, not a fresh one");
        Assert.That(Durable(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active));

        ReportResult(score: 30);
        VersusSeries after = Durable(id);
        Assert.That(after.ViewFor(player).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Completed), "the retried turn completes normally");
        Assert.That(after.Games[0].Status, Is.Not.EqualTo(VersusGameStatus.Forfeited));
        Log("SESSIONC2 OK series=" + id + " attempt=" + handoff["attempt"] + " retried and completed, game1=" + after.Games[0].Status);
    }

    // ================================================================= real GameRules retry loop

    /// <summary>
    /// The real match-end retry loop, with a repository that refuses saves: the production
    /// <c>GameRules.HandleMatchEnded</c> pass calls <see cref="VersusMatchReporter.TryReport"/> itself, sees
    /// it fail, and comes back a second later until the save lands. No production timing or structure is
    /// changed; the only test seam is the versus repository the game already lets a test replace.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator TheRealGameRulesMatchEndLoopRetriesTheVersusResultUntilItIsDurable()
    {
        FailableRepository repository = new FailableRepository(new FileVersusSeriesRepository(root));
        VersusRuntime.Override(repository);

        yield return OpenLocalVersusFromStart();
        FindButton("createButton").onClick.Invoke();
        yield return null;

        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        ParticipantId player = ExpectedNext(Durable(id));
        yield return LaunchTurn(id, player);

        GameStats stats = UnityEngine.Object.FindAnyObjectByType<GameStats>();
        Assert.That(stats, Is.Not.Null);
        stats.TotalPoints = 30;
        stats.ShotMade = 5;
        stats.ShotAttempt = 10;

        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        Assert.That(rules, Is.Not.Null, "the gameplay scene has no GameRules");

        repository.FailSaves = true;
        int failuresBefore = repository.RefusedSaves;
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);

        // GameRules retries every second (Time.unscaledTime); two refusals means the loop, not a one-off
        float deadline = Time.realtimeSinceStartup + 30f;
        while (repository.RefusedSaves < failuresBefore + 2 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(
            repository.RefusedSaves,
            Is.GreaterThanOrEqualTo(failuresBefore + 2),
            "GameRules did not retry the versus result while the save was failing");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "a refused save leaves the attempt outstanding");
        Assert.That(VersusQuitPolicy.TurnInProgress, Is.False, "the match has ended");
        Assert.That(VersusQuitPolicy.AttemptOutstanding, Is.True);
        Assert.That(
            Durable(id).ViewFor(player).CurrentGame.OwnAttemptState,
            Is.EqualTo(AttemptState.Started),
            "the series file is untouched while saves fail");
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "leaving is refused while the earned result is pending, and nothing is forfeited");
        Assert.That(Durable(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active));

        repository.FailSaves = false;
        deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "the next GameRules pass saved the result and cleared the attempt");
        VersusSeries stored = Durable(id);
        Assert.That(stored.ViewFor(player).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Completed));
        Assert.That(stored.Games[0].Status, Is.Not.EqualTo(VersusGameStatus.Forfeited), "the earned result was not converted into a forfeit");
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True, "with the result durable, Continue Series may leave");
        Log("GAMERULES-RETRY OK refusedSaves=" + repository.RefusedSaves + " completed=" + stored.ViewFor(player).CurrentGame.OwnAttemptState);
    }

    // ================================================================= local simultaneous (Most Points, two humans, one shared match)

    // The simultaneous slice's own process sessions (docs/versus-architecture.md, "Local simultaneous play").
    // Same rules as the alternating sessions above: each is its own Unity.exe, the repository is the only
    // competitive-state authority, and the handoff file carries identifiers and display names only. Two
    // virtual gamepads stand in for the two humans so the launcher's own two-human device preflight and the
    // gameplay scene's own device plan run for real; they are not physical-device certification.

    /// <summary>
    /// Process S1: create a Best-of-3 Most Points series through the real screens, launch the shared match,
    /// end it through the real <c>GameRules</c> match-end path, and confirm the pair is durable on disk.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SessionS1_CreateSimultaneousBestOf3AndCompleteOneSharedGame()
    {
        Assert.That(Directory.Exists(root) ? Directory.GetFiles(root).Length : 0, Is.EqualTo(0), "session S1 needs an empty repository");
        SeatTwoVirtualGamepads();

        yield return OpenLocalVersusFromStart();
        SetInput("player1NameInputField", "Alice");
        SetInput("player2NameInputField", "Bob");
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        FindButton("createButton").onClick.Invoke();
        yield return null;

        Assert.That(new FileVersusSeriesRepository(root).ListSummaries(), Has.Count.EqualTo(1), "Create stored exactly one series");
        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;
        VersusSeries created = Durable(id);
        Assert.That(created.Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
        Assert.That(created.Snapshot.GameCount, Is.EqualTo(3));
        Assert.That(created.Snapshot.GameAt(0).Id.Value, Is.EqualTo("most-points"));

        yield return LaunchSharedGame(id);
        AttemptId first = ActiveVersusAttempt.AttemptId;
        AttemptId second = ActiveVersusAttempt.SecondAttemptId;
        Assert.That(first.Value, Is.Not.EqualTo(second.Value));
        Assert.That(Durable(id).ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(Durable(id).ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));

        // Bob (slot 1) outscores Alice (slot 0): the winner is decided by slot identity, not by who is ahead first
        yield return EndSharedGame(firstScore: 12, secondScore: 30);

        VersusSeries stored = Durable(id);
        AssertGameRecorded(stored, 0, first, second);
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(stored.Participants.Second.Id));
        Assert.That(stored.Score.FirstWins, Is.EqualTo(0));
        Assert.That(stored.Score.SecondWins, Is.EqualTo(1));
        Assert.That(stored.IsActive, Is.True, "one game does not decide a Best-of-3");
        Assert.That(stored.CurrentGame.Index, Is.EqualTo(1), "the series advanced to game 2");

        WriteHandoff(new Dictionary<string, string>
        {
            ["series"] = id.Value,
            ["first"] = created.Participants.First.DisplayName,
            ["second"] = created.Participants.Second.DisplayName,
            ["game1FirstAttempt"] = first.Value,
            ["game1SecondAttempt"] = second.Value
        });
        Log("SESSIONS1 OK series=" + id + " game1 winner=" + stored.Participants.Second.DisplayName + " attempts=" + first + "," + second + " score=0-1");
    }

    /// <summary>
    /// Process S2 (fresh): locate the series from the repository alone, verify game 1, play the remaining
    /// games until the series completes.
    /// </summary>
    [UnityTest]
    [Timeout(1200000)]
    public IEnumerator SessionS2_RestoreInFreshProcessVerifyGameOneAndFinishTheSeries()
    {
        Dictionary<string, string> handoff = ReadHandoff();
        SeatTwoVirtualGamepads();

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "a fresh process has no live attempt");
        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1), "the series survived the process boundary on disk");
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        Assert.That(id.Value, Is.EqualTo(handoff["series"]), "the located series is the one session S1 made (cross-check only)");

        VersusSeries restored = Durable(id);
        Assert.That(restored.Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
        AssertGameRecorded(restored, 0, new AttemptId(handoff["game1FirstAttempt"]), new AttemptId(handoff["game1SecondAttempt"]));
        Assert.That(restored.Games[0].Result.WinnerId, Is.EqualTo(restored.Participants.Second.Id), "the original game-one result");
        Assert.That(restored.Score.FirstWins + restored.Score.SecondWins, Is.EqualTo(1));
        Assert.That(restored.CurrentGame.Index, Is.EqualTo(1), "still game 2");

        yield return OpenLocalVersusFromStart();
        Assert.That(TextOf("seriesDetail"), Does.Contain(handoff["first"] + " 0 - 1 " + handoff["second"]));
        Assert.That(TextOf("seriesDetail"), Does.Contain("Game 2 of 3"));
        Assert.That(TextOf("playTurnButton"), Is.EqualTo("Play Game"));
        Assert.That(FindButton("playTurnButton").interactable, Is.True);

        // game 2 and 3: the first participant wins both, so the series ends 2-1 on game 3
        yield return PlaySharedGame(id, firstScore: 40, secondScore: 15);
        VersusSeries afterTwo = Durable(id);
        Assert.That(afterTwo.IsActive, Is.True, "level at one game each");
        Assert.That(afterTwo.CurrentGame.Index, Is.EqualTo(2));
        AssertGameRecorded(afterTwo, 1, default, default);

        yield return PlaySharedGame(id, firstScore: 33, secondScore: 21);
        VersusSeries finished = Durable(id);
        AssertGameRecorded(finished, 2, default, default);
        Assert.That(finished.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(finished.Result.WinnerId, Is.EqualTo(finished.Participants.First.Id));
        Assert.That(finished.Score.FirstWins, Is.EqualTo(2));
        Assert.That(finished.Score.SecondWins, Is.EqualTo(1));
        Assert.That(TextOf("seriesDetail"), Does.Contain("wins the series."));
        Assert.That(FindButton("playTurnButton").interactable, Is.False, "a finished series offers no game");

        // the remaining games' attempt ids are what session S3 must find again
        WriteHandoff(new Dictionary<string, string>
        {
            ["series"] = id.Value,
            ["first"] = handoff["first"],
            ["second"] = handoff["second"],
            ["game1FirstAttempt"] = handoff["game1FirstAttempt"],
            ["game1SecondAttempt"] = handoff["game1SecondAttempt"],
            ["game2FirstAttempt"] = finished.ViewFor(finished.Participants.First.Id).Games[1].OwnAttemptId.Value,
            ["game3SecondAttempt"] = finished.ViewFor(finished.Participants.Second.Id).Games[2].OwnAttemptId.Value,
            ["winner"] = finished.Participants.First.DisplayName
        });
        Log("SESSIONS2 OK series=" + id + " completed " + finished.Score.FirstWins + "-" + finished.Score.SecondWins + " winner=" + handoff["first"]);
    }

    /// <summary>Process S3 (fresh): the completed series and its history survive another restart, and offer no further game.</summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SessionS3_TheCompletedSeriesSurvivesAnotherRestart()
    {
        Dictionary<string, string> handoff = ReadHandoff();
        SeatTwoVirtualGamepads();

        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1));
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        Assert.That(id.Value, Is.EqualTo(handoff["series"]));

        VersusSeries stored = Durable(id);
        Assert.That(stored.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(stored.Participants.Find(stored.Result.WinnerId).DisplayName, Is.EqualTo(handoff["winner"]), "cross-check only");
        Assert.That(stored.Score.FirstWins, Is.EqualTo(2));
        Assert.That(stored.Score.SecondWins, Is.EqualTo(1));
        AssertGameRecorded(stored, 0, new AttemptId(handoff["game1FirstAttempt"]), new AttemptId(handoff["game1SecondAttempt"]));
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(stored.Participants.Second.Id));
        AssertGameRecorded(stored, 1, default, default);
        Assert.That(stored.Games[1].Result.WinnerId, Is.EqualTo(stored.Participants.First.Id));
        Assert.That(stored.ViewFor(stored.Participants.First.Id).Games[1].OwnAttemptId.Value, Is.EqualTo(handoff["game2FirstAttempt"]));
        AssertGameRecorded(stored, 2, default, default);
        Assert.That(stored.Games[2].Result.WinnerId, Is.EqualTo(stored.Participants.First.Id));
        Assert.That(stored.ViewFor(stored.Participants.Second.Id).Games[2].OwnAttemptId.Value, Is.EqualTo(handoff["game3SecondAttempt"]));

        yield return OpenLocalVersusFromStart();
        Assert.That(TextOf("seriesDetail"), Does.Contain("wins the series."));
        Assert.That(TextOf("seriesDetail"), Does.Contain(handoff["winner"]));
        Assert.That(FindButton("playTurnButton").interactable, Is.False, "a finished series offers no game");
        Log("SESSIONS3 OK series=" + id + " completed series restored, winner=" + handoff["winner"]);
    }

    /// <summary>
    /// Process SC1: launch a shared match from the real screen, confirm both attempts are durably Started,
    /// record their ids, then die without any pause exit. Never returns.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SessionSC1_StartASharedGameThenTheProcessIsKilled()
    {
        if (Environment.GetEnvironmentVariable(AllowKillEnvVar) != "1")
        {
            Assert.Ignore("Set " + AllowKillEnvVar + "=1 to run the session that kills its own process.");
        }

        Assert.That(Directory.Exists(root) ? Directory.GetFiles(root).Length : 0, Is.EqualTo(0), "session SC1 needs an empty repository");
        SeatTwoVirtualGamepads();

        yield return OpenLocalVersusFromStart();
        SetInput("player1NameInputField", "Alice");
        SetInput("player2NameInputField", "Bob");
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        FindButton("createButton").onClick.Invoke();
        yield return null;

        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;
        VersusSeries created = Durable(id);

        yield return LaunchSharedGame(id);
        Assert.That(VersusQuitPolicy.SimultaneousGameInProgress, Is.True, "a live shared game, not yet finished");

        VersusSeries stored = Durable(id);
        ParticipantGameView one = stored.ViewFor(stored.Participants.First.Id).CurrentGame;
        ParticipantGameView two = stored.ViewFor(stored.Participants.Second.Id).CurrentGame;
        Assert.That(one.OwnAttemptState, Is.EqualTo(AttemptState.Started), "the first participant's attempt is durably Started");
        Assert.That(two.OwnAttemptState, Is.EqualTo(AttemptState.Started), "the second participant's attempt is durably Started");
        Assert.That(one.OwnAttemptId.Value, Is.EqualTo(ActiveVersusAttempt.AttemptId.Value));
        Assert.That(two.OwnAttemptId.Value, Is.EqualTo(ActiveVersusAttempt.SecondAttemptId.Value));

        WriteHandoff(new Dictionary<string, string>
        {
            ["series"] = id.Value,
            ["first"] = created.Participants.First.DisplayName,
            ["second"] = created.Participants.Second.DisplayName,
            ["firstAttempt"] = one.OwnAttemptId.Value,
            ["secondAttempt"] = two.OwnAttemptId.Value
        });
        Log("SESSIONSC1 shared game live series=" + id + " attempts=" + one.OwnAttemptId + "," + two.OwnAttemptId + " - killing process now, no pause action taken");

        yield return new WaitForSecondsRealtime(1f);
        Process.GetCurrentProcess().Kill();
        yield return new WaitForSecondsRealtime(30f);
        Assert.Fail("the process should have been killed");
    }

    /// <summary>
    /// Process SC2 (fresh): the kill cost nobody the game and advanced nothing; relaunching reuses the two
    /// original attempts, and completing the match records exactly one durable game result.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator SessionSC2_RelaunchReusesBothAttemptsWithNoForfeitAndRecordsOneResult()
    {
        Dictionary<string, string> handoff = ReadHandoff();
        SeatTwoVirtualGamepads();

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "a fresh process has no live attempt");
        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1));
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        Assert.That(id.Value, Is.EqualTo(handoff["series"]));

        VersusSeries stored = Durable(id);
        Assert.That(stored.Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(stored.CurrentGame.Index, Is.EqualTo(0), "the game did not advance");
        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "nobody was forfeited and nothing resolved");
        Assert.That(stored.Score.FirstWins + stored.Score.SecondWins + stored.Score.Draws, Is.EqualTo(0), "no game was awarded");
        ParticipantGameView one = stored.ViewFor(stored.Participants.First.Id).CurrentGame;
        ParticipantGameView two = stored.ViewFor(stored.Participants.Second.Id).CurrentGame;
        Assert.That(one.OwnAttemptState, Is.EqualTo(AttemptState.Started), "the first participant's attempt is still outstanding");
        Assert.That(two.OwnAttemptState, Is.EqualTo(AttemptState.Started), "the second participant's attempt is still outstanding");
        Assert.That(one.OwnAttemptId.Value, Is.EqualTo(handoff["firstAttempt"]));
        Assert.That(two.OwnAttemptId.Value, Is.EqualTo(handoff["secondAttempt"]));

        yield return OpenLocalVersusFromStart();
        Assert.That(TextOf("seriesDetail"), Does.Contain("Game 1 of 3"));
        Assert.That(FindButton("playTurnButton").interactable, Is.True, "relaunch is permitted");

        yield return LaunchSharedGame(id);
        Assert.That(ActiveVersusAttempt.AttemptId.Value, Is.EqualTo(handoff["firstAttempt"]), "the first attempt is reused, not a fresh one");
        Assert.That(ActiveVersusAttempt.SecondAttemptId.Value, Is.EqualTo(handoff["secondAttempt"]), "the second attempt is reused, not a fresh one");
        Assert.That(Durable(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active));

        yield return EndSharedGame(firstScore: 30, secondScore: 12);

        VersusSeries after = Durable(id);
        AssertGameRecorded(after, 0, new AttemptId(handoff["firstAttempt"]), new AttemptId(handoff["secondAttempt"]));
        Assert.That(after.Games[0].Result.WinnerId, Is.EqualTo(after.Participants.First.Id));
        Assert.That(after.Score.FirstWins, Is.EqualTo(1));
        Assert.That(after.Score.SecondWins + after.Score.Draws, Is.EqualTo(0), "exactly one durable game result");
        int resolved = 0;
        foreach (VersusGame game in after.Games)
        {
            resolved += game.IsResolved ? 1 : 0;
        }

        Assert.That(resolved, Is.EqualTo(1), "exactly one game resolved");
        Log("SESSIONSC2 OK series=" + id + " attempts reused " + handoff["firstAttempt"] + "," + handoff["secondAttempt"] + " one result, score=1-0");
    }

    /// <summary>
    /// The real <c>GameRules</c> match-end path for a simultaneous game, in an isolated persistence directory:
    /// a refused save leaves both attempts outstanding and the game unadvanced, the retry after the disk
    /// recovers records the pair once and advances the game once, and - unlike an ordinary alternating turn,
    /// which is run first as the positive control - nothing outside the series document is written (no local
    /// high score, all-time stats, pending result or character progression).
    /// The existing suites pin the exclusion by reading <c>GameRules</c>'s source; this observes it.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator ASimultaneousMatchEndRetriesAtomicallyAndWritesOnlyTheSeries()
    {
        // seated before any gameplay scene runs: InputTestFixture restores the state it saw at Setup
        SeatTwoVirtualGamepads();
        FailableRepository repository = new FailableRepository(new FileVersusSeriesRepository(root));
        VersusRuntime.Override(repository);

        // control: an ordinary alternating turn does persist general results, and the probe sees it
        yield return OpenLocalVersusFromStart();
        FindButton("createButton").onClick.Invoke();
        yield return null;
        SeriesId alternating = VersusRuntime.Coordinator.ListSeries()[0].Id;
        yield return LaunchTurn(alternating, ExpectedNext(Durable(alternating)));
        Dictionary<string, string> beforeControl = SnapshotPersistentData();
        UnityEngine.Object.FindAnyObjectByType<GameStats>().TotalPoints = 30;
        yield return EndMatchThroughGameRules();
        List<string> controlWrites = ChangesSince(beforeControl);
        Log("COMPETITION-ONLY control writes: " + string.Join(", ", controlWrites));
        Assert.That(controlWrites, Is.Not.Empty, "control: an ordinary turn's end writes general results, so this probe can see writes");

        // the simultaneous match
        yield return OpenLocalVersusFromStart();
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        FindButton("createButton").onClick.Invoke();
        yield return null;
        SeriesId id = default;
        foreach (SeriesSummary summary in VersusRuntime.Coordinator.ListSeries())
        {
            if (summary.Mode == VersusMode.LocalSimultaneous)
            {
                id = summary.Id;
            }
        }

        Assert.That(id.HasValue, Is.True, "the simultaneous series was created");
        VersusSeries created = Durable(id);
        yield return LaunchSharedGame(id);
        AttemptId first = ActiveVersusAttempt.AttemptId;
        AttemptId second = ActiveVersusAttempt.SecondAttemptId;

        Dictionary<string, string> before = SnapshotPersistentData();
        PlayerRegistry players = LevelRuntimeContext.instance.Players;
        players.GetBySlot(0).gameStats.TotalPoints = 12;
        players.GetBySlot(1).gameStats.TotalPoints = 30;

        repository.FailSaves = true;
        int refusedBefore = repository.RefusedSaves;
        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);
        float deadline = Time.realtimeSinceStartup + 30f;
        while (repository.RefusedSaves < refusedBefore + 2 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(repository.RefusedSaves, Is.GreaterThanOrEqualTo(refusedBefore + 2), "GameRules retried the pair while saves failed");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "the competitive context stays available for the retry");
        Assert.That(ActiveVersusAttempt.AttemptId.Value, Is.EqualTo(first.Value));
        Assert.That(ActiveVersusAttempt.SecondAttemptId.Value, Is.EqualTo(second.Value));
        VersusSeries pending = Durable(id);
        Assert.That(pending.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "the game did not advance");
        Assert.That(pending.CurrentGame.Index, Is.EqualTo(0));
        Assert.That(pending.ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started), "neither result is durable");
        Assert.That(pending.ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started), "neither result is durable");
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "leaving waits for the pair");

        repository.FailSaves = false;
        deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "the retry recorded the pair and cleared the context");
        VersusSeries stored = Durable(id);
        AssertGameRecorded(stored, 0, first, second);
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(stored.Participants.Second.Id), "slot 1 outscored slot 0");
        Assert.That(stored.Score.FirstWins + stored.Score.SecondWins + stored.Score.Draws, Is.EqualTo(1), "the game advanced exactly once");
        Assert.That(stored.CurrentGame.Index, Is.EqualTo(1));

        List<string> writes = ChangesSince(before);
        Assert.That(writes, Is.Empty, "a simultaneous match wrote outside the series document: " + string.Join(", ", writes));
        Log("COMPETITION-ONLY OK refusedSaves=" + repository.RefusedSaves + " nonSeriesWrites=" + writes.Count + " (control saw " + controlWrites.Count + ")");
    }

    /// <summary>Sets the running match over through the real <c>GameRules</c> and waits for its result to be recorded.</summary>
    private static IEnumerator EndMatchThroughGameRules()
    {
        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        Assert.That(rules, Is.Not.Null, "the gameplay scene has no GameRules");
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);
        float deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "GameRules never recorded the result");
        yield return null;
    }

    private static Dictionary<string, string> SnapshotPersistentData()
    {
        Dictionary<string, string> files = new Dictionary<string, string>();
        if (Directory.Exists(Application.persistentDataPath))
        {
            foreach (string path in Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories))
            {
                FileInfo info = new FileInfo(path);
                files[path] = info.Length + "@" + info.LastWriteTimeUtc.Ticks;
            }
        }

        return files;
    }

    /// <summary>Files under the persistent data path created or modified since <paramref name="before"/>.</summary>
    private static List<string> ChangesSince(Dictionary<string, string> before)
    {
        List<string> changed = new List<string>();
        foreach (KeyValuePair<string, string> now in SnapshotPersistentData())
        {
            if (!before.TryGetValue(now.Key, out string then) || then != now.Value)
            {
                changed.Add(Path.GetFileName(now.Key));
            }
        }

        return changed;
    }

    // ================================================================= rendered layout

    /// <summary>
    /// Renders the real Local Versus screen at 1920x1080 in the states that stress its layout - empty,
    /// a fresh series with the longest names the field accepts, a Best-of-7 in progress, a completed
    /// series, and many stored series - saving a screenshot of each to
    /// <c>LEVEL5_LOCAL_VERSUS_CERT_SHOTS</c> and failing on any text or control that leaves the screen
    /// or whose text is being truncated. Needs a graphics device: run without <c>-nographics</c>.
    /// The states are built through the coordinator (forfeits stand in for played games); what is being
    /// certified here is how the shipped scene renders them, not how they were reached.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator Layout_RenderedStatesStayOnScreenAndUntruncated()
    {
        string shots = Environment.GetEnvironmentVariable("LEVEL5_LOCAL_VERSUS_CERT_SHOTS");
        Assert.That(shots, Is.Not.Null.And.Not.Empty, "LEVEL5_LOCAL_VERSUS_CERT_SHOTS must name the screenshot directory");
        Directory.CreateDirectory(shots);

        yield return UseCertificationResolution();
        Log("LAYOUT screen=" + Screen.width + "x" + Screen.height);
        List<string> problems = new List<string>();

        yield return CaptureState("1-empty", shots, problems);

        CreateSeries("Alice", "Bob", 3);
        yield return CaptureState("2-fresh-best-of-3", shots, problems);

        // the input field's own limit is 16 characters; the widest glyphs make the worst case
        SeriesId longNames = CreateSeries("WWWWWWWWWWWWWWWW", "MMMMMMMMMMMMMMMM", 7);
        yield return CaptureState("3-longest-names-best-of-7", shots, problems);

        // a Best-of-7 partway through: alternating forfeits give 2-2 and leave game 5 to play
        for (int game = 0; game < 4; game++)
        {
            VersusSeries series = VersusRuntime.Coordinator.Load(longNames);
            ParticipantId forfeiter = game % 2 == 0 ? series.Participants.First.Id : series.Participants.Second.Id;
            Assert.That(ForfeitGame(longNames, forfeiter), Is.True);
        }

        yield return CaptureState("4-best-of-7-in-progress", shots, problems);

        SeriesId done = CreateSeries("Alice", "Bob", 3);
        for (int game = 0; game < 2; game++)
        {
            Assert.That(ForfeitGame(done, VersusRuntime.Coordinator.Load(done).Participants.Second.Id), Is.True);
        }

        Assert.That(VersusRuntime.Coordinator.Load(done).IsOver, Is.True);
        yield return CaptureState("5-completed-series-selected", shots, problems, done);

        for (int index = 0; index < 5; index++)
        {
            CreateSeries("Team " + (index + 1) + " Home", "Team " + (index + 1) + " Away", 3);
        }

        yield return CaptureState("6-seven-stored-series", shots, problems);

        // the widest row the list can hold: a finished Best-of-7 between the longest names
        SeriesId finishedLongest = CreateSeries("WWWWWWWWWWWWWWWW", "MMMMMMMMMMMMMMMM", 7);
        for (int game = 0; game < 4; game++)
        {
            Assert.That(ForfeitGame(finishedLongest, VersusRuntime.Coordinator.Load(finishedLongest).Participants.Second.Id), Is.True);
        }

        Assert.That(VersusRuntime.Coordinator.Load(finishedLongest).IsOver, Is.True);
        yield return CaptureState("7-finished-longest-best-of-7-selected", shots, problems, finishedLongest);
        Assert.That(problems, Is.Empty, "layout problems:\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// The simultaneous counterpart of <see cref="Layout_RenderedStatesStayOnScreenAndUntruncated"/>: the states
    /// only a same-time series has - the create form set to Local Simultaneous, both participants' character
    /// selectors (swept through every character, then held on the longest), the longest permitted names in a
    /// Best-of-7 (fresh, in progress and completed), a completed Best-of-3, the list mixing alternating and
    /// simultaneous series, and the two error paths the screen can show (a create that cannot be saved and a
    /// launch that cannot start). Each state is screenshotted and rendered-boundary checked, and its selection,
    /// navigation and mode-dependent visibility are checked too. Needs a graphics device.
    /// </summary>
    [UnityTest]
    [Timeout(900000)]
    public IEnumerator Layout_SimultaneousStatesStayOnScreenAndUntruncated()
    {
        string shots = Environment.GetEnvironmentVariable("LEVEL5_LOCAL_VERSUS_CERT_SHOTS");
        Assert.That(shots, Is.Not.Null.And.Not.Empty, "LEVEL5_LOCAL_VERSUS_CERT_SHOTS must name the screenshot directory");
        Assert.That(Directory.Exists(root) ? Directory.GetFiles(root).Length : 0, Is.EqualTo(0), "the layout run needs an empty repository");
        Directory.CreateDirectory(shots);

        yield return UseCertificationResolution();
        SeatTwoVirtualGamepads();
        List<string> problems = new List<string>();
        const string longFirst = "WWWWWWWWWWWWWWWW";
        const string longSecond = "MMMMMMMMMMMMMMMM";

        // the create form, switched to Local Simultaneous, with nothing stored
        yield return OpenLocalVersusFromStart();
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        Assert.That(TextOf("modeButton"), Does.Contain("Local Simultaneous"));
        yield return CaptureCurrent("sim-1-create-form-empty", shots, problems);

        // a fresh Best-of-3: both participants' selectors, swept through every character
        SeriesId fresh = CreateSimultaneousSeries("Alice", "Bob", 3);
        yield return CaptureState("sim-2-fresh-best-of-3", shots, problems, fresh);
        Assert.That(TextOf("playTurnButton"), Is.EqualTo("Play Game"));
        Assert.That(FindButton("character2Button").gameObject.activeInHierarchy, Is.True, "Player 2's selector is shown");
        yield return SweepCharacterSelectors("sim-3-both-selectors", shots, problems);

        // the longest permitted names in a Best-of-7 (fresh, then partway, then done)
        SeriesId longNames = CreateSimultaneousSeries(longFirst, longSecond, 7);
        yield return CaptureState("sim-4-longest-names-best-of-7", shots, problems, longNames);
        yield return SweepCharacterSelectors("sim-5-longest-names-longest-characters", shots, problems);

        for (int game = 0; game < 4; game++)
        {
            // alternating winners: the first participant takes games 1 and 3, the second games 2 and 4 -> 2-2
            PlaySimultaneousGameThroughTheCoordinator(longNames, game % 2 == 0 ? 30 : 10, game % 2 == 0 ? 10 : 30);
        }

        VersusSeries partway = VersusRuntime.Coordinator.Load(longNames);
        Assert.That(partway.Score.FirstWins, Is.EqualTo(2));
        Assert.That(partway.Score.SecondWins, Is.EqualTo(2));
        yield return CaptureState("sim-6-best-of-7-in-progress-2-2", shots, problems, longNames);

        SeriesId finishedLongest = CreateSimultaneousSeries(longFirst, longSecond, 7);
        for (int game = 0; game < 4; game++)
        {
            PlaySimultaneousGameThroughTheCoordinator(finishedLongest, 30, 10);
        }

        Assert.That(VersusRuntime.Coordinator.Load(finishedLongest).IsOver, Is.True);
        yield return CaptureState("sim-7-completed-best-of-7-longest-names", shots, problems, finishedLongest);
        Assert.That(FindButton("playTurnButton").interactable, Is.False, "a finished series offers no game");

        SeriesId finishedShort = CreateSimultaneousSeries("Alice", "Bob", 3);
        for (int game = 0; game < 2; game++)
        {
            PlaySimultaneousGameThroughTheCoordinator(finishedShort, 10, 30);
        }

        Assert.That(VersusRuntime.Coordinator.Load(finishedShort).IsOver, Is.True);
        yield return CaptureState("sim-8-completed-best-of-3", shots, problems, finishedShort);

        // a list holding both kinds at once, the selection moving between them
        SeriesId alternating = CreateSeries("Carol", "Dave", 3);
        CreateSeries(longFirst, longSecond, 7);
        SeriesId alternatingDone = CreateSeries("Erin", "Frank", 3);
        for (int game = 0; game < 2; game++)
        {
            Assert.That(ForfeitGame(alternatingDone, VersusRuntime.Coordinator.Load(alternatingDone).Participants.Second.Id), Is.True);
        }

        yield return CaptureState("sim-9-mixed-list-simultaneous-selected", shots, problems, fresh);
        yield return CaptureState("sim-10-mixed-list-alternating-selected", shots, problems, alternating);
        Assert.That(FindButton("character2Button").gameObject.activeInHierarchy, Is.False, "an alternating series has one selector");
        Assert.That(TextOf("playTurnButton"), Does.StartWith("Play Turn"));
        yield return CaptureState("sim-11-mixed-list-alternating-finished-selected", shots, problems, alternatingDone);
        yield return CaptureState("sim-12-mixed-list-longest-simultaneous-selected", shots, problems, finishedLongest);

        // create error: the repository refuses the save, so the coordinator's refusal is what the screen says
        FailableRepository failing = new FailableRepository(new FileVersusSeriesRepository(root));
        VersusRuntime.Override(failing);
        LocalVersusNavigationState.Clear();
        yield return OpenLocalVersusFromStart();
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        SetInput("player1NameInputField", longFirst);
        SetInput("player2NameInputField", longSecond);
        failing.FailSaves = true;
        int seriesBefore = new FileVersusSeriesRepository(root).ListSummaries().Count;
        FindButton("createButton").onClick.Invoke();
        yield return null;
        Assert.That(TextOf("createMessage"), Does.Contain("Could not create the series"), "the refused create says so");
        Assert.That(new FileVersusSeriesRepository(root).ListSummaries().Count, Is.EqualTo(seriesBefore), "nothing was stored");
        yield return CaptureCurrent("sim-13-create-error", shots, problems);
        failing.FailSaves = false;

        // launch error 1: only one gamepad is attached, so the two-human device preflight refuses
        LocalVersusNavigationState.Clear();
        LocalVersusNavigationState.Begin(fresh);
        yield return OpenLocalVersusFromStart();
        InputSystem.RemoveDevice(padB);
        FindButton("playTurnButton").onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus), "a refused launch stays on the screen");
        Assert.That(TextOf("turnMessage"), Does.StartWith("Could not start the game"));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "nothing was issued");
        yield return CaptureCurrent("sim-14-launch-error-device-preflight", shots, problems);
        padB = InputSystem.AddDevice<Gamepad>();

        // launch error 2: the pair of attempts cannot be saved, so nothing is spent and the screen says why
        failing.FailSaves = true;
        FindButton("playTurnButton").onClick.Invoke();
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus));
        Assert.That(TextOf("turnMessage"), Does.StartWith("Could not start the game"));
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        yield return CaptureCurrent("sim-15-launch-error-save-refused", shots, problems);
        failing.FailSaves = false;

        Assert.That(problems, Is.Empty, "layout problems:\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Cycles both participants' selectors through every character on the open screen, layout-checking each
    /// label, then holds each on the longest label and screenshots that state.
    /// </summary>
    private IEnumerator SweepCharacterSelectors(string name, string directory, List<string> problems)
    {
        string[] longest = new string[2];
        string[] buttons = { "characterButton", "character2Button" };
        for (int selector = 0; selector < 2; selector++)
        {
            string start = TextOf(buttons[selector]);
            int guard = 0;
            do
            {
                string label = TextOf(buttons[selector]);
                if (longest[selector] == null || label.Length > longest[selector].Length)
                {
                    longest[selector] = label;
                }

                FindButton(buttons[selector]).onClick.Invoke();
                yield return null;
                List<string> found = new List<string>();
                CheckLayout(name + " " + buttons[selector] + " '" + Short(TextOf(buttons[selector])) + "'", found);
                problems.AddRange(found);
            }
            while (TextOf(buttons[selector]) != start && ++guard < 400);

            Assert.That(guard, Is.LessThan(400), buttons[selector] + " never cycled back");
        }

        for (int selector = 0; selector < 2; selector++)
        {
            int guard = 0;
            while (TextOf(buttons[selector]) != longest[selector] && ++guard < 400)
            {
                FindButton(buttons[selector]).onClick.Invoke();
                yield return null;
            }

            Assert.That(TextOf(buttons[selector]), Is.EqualTo(longest[selector]));
        }

        yield return CaptureCurrent(name, directory, problems);
    }

    private static SeriesId CreateSimultaneousSeries(string first, string second, int games)
    {
        SeriesId id = CreateSeries(first, second, games, VersusMode.LocalSimultaneous);
        Assert.That(VersusRuntime.Coordinator.Load(id).Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
        return id;
    }

    /// <summary>Resolves one simultaneous game through the coordinator's own pair operations (no scene; layout states only).</summary>
    private static void PlaySimultaneousGameThroughTheCoordinator(SeriesId id, float firstScore, float secondScore)
    {
        VersusMatchCoordinator coordinator = VersusRuntime.Coordinator;
        CompetitiveRuleset ruleset = VersusCatalogs.Rulesets.Supporting(VersusModes.RequiredCapability(VersusMode.LocalSimultaneous))[0];
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        Assert.That(issued.Succeeded, Is.True, issued.Validation?.ToString());
        AttemptResult first = new AttemptResult.Builder(ruleset.Id, ruleset.Version).Set(AttemptMetric.Score, firstScore).SetShooting(5, 10).Build();
        AttemptResult second = new AttemptResult.Builder(ruleset.Id, ruleset.Version).Set(AttemptMetric.Score, secondScore).SetShooting(5, 10).Build();
        SubmissionOperation submitted = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, first),
            new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, second));
        Assert.That(submitted.Succeeded, Is.True, submitted.Validation?.ToString());
    }

    /// <summary>The 1920x1080 render the layout certification is specified at (the Editor Game view; a player is sized by its own window).</summary>
    private static IEnumerator UseCertificationResolution()
    {
#if UNITY_EDITOR
        UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "lv-cert 1920x1080");
        yield return null;
        yield return null;
#else
        yield break;
#endif
    }

    private static SeriesId CreateSeries(string first, string second, int games)
    {
        return CreateSeries(first, second, games, VersusMode.LocalAlternating);
    }

    private static SeriesId CreateSeries(string first, string second, int games, VersusMode mode)
    {
        RulesetId ruleset = VersusCatalogs.Rulesets.Supporting(VersusModes.RequiredCapability(mode))[0].Id;
        List<RulesetId> playlist = new List<RulesetId>();
        for (int game = 0; game < games; game++)
        {
            playlist.Add(ruleset);
        }

        SeriesOperation created = VersusRuntime.Coordinator.CreateSeries(new SeriesRequest(
            new MatchParticipant(new ParticipantId(Guid.NewGuid().ToString("N")), first),
            new MatchParticipant(new ParticipantId(Guid.NewGuid().ToString("N")), second),
            SeriesFormat.FromGameCount(games),
            playlist,
            mode,
            InformationPolicy.SealedAttempt,
            false,
            true,
            "local versus layout certification"));
        Assert.That(created.Succeeded, Is.True, created.Validation.ToString());
        return created.Series.Id;
    }

    private static bool ForfeitGame(SeriesId id, ParticipantId forfeiter)
    {
        AttemptOperation issued = VersusRuntime.Coordinator.IssueAttempt(id, forfeiter);
        Assert.That(issued.Succeeded, Is.True, issued.Validation.ToString());
        VersusRuntime.Coordinator.StartAttempt(id, issued.Attempt.Id);
        return VersusRuntime.Coordinator.ForfeitGame(id, forfeiter).Succeeded;
    }

    private IEnumerator CaptureState(string name, string directory, List<string> problems, SeriesId select = default)
    {
        LocalVersusNavigationState.Clear();
        if (select.HasValue)
        {
            LocalVersusNavigationState.Begin(select);
        }

        yield return OpenLocalVersusFromStart();
        yield return CaptureCurrent(name, directory, problems);
    }

    /// <summary>Screenshots and checks whatever the open Local Versus screen is showing now.</summary>
    private IEnumerator CaptureCurrent(string name, string directory, List<string> problems)
    {
        yield return new WaitForEndOfFrame();
        yield return null;
        yield return new WaitForEndOfFrame();

        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);

        List<string> found = new List<string>();
        CheckLayout(name, found);
        CheckScreenBehavior(name, found);
        problems.AddRange(found);
        Log("LAYOUT state=" + name + " problems=" + found.Count);
    }

    /// <summary>Reports text that overflows its box or a control that leaves the screen, by reading the rendered rects.</summary>
    private static void CheckLayout(string state, List<string> problems)
    {
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Component component in sceneRoot.GetComponentsInChildren<Component>(false))
            {
                if (component == null)
                {
                    continue;
                }

                string type = component.GetType().Name;
                bool isText = type.StartsWith("TextMeshPro");
                bool isControl = component is Button || type == "TMP_InputField";
                if (!isText && !isControl)
                {
                    continue;
                }

                RectTransform rect = component.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float minX = Mathf.Min(corners[0].x, corners[2].x);
                float maxX = Mathf.Max(corners[0].x, corners[2].x);
                float minY = Mathf.Min(corners[0].y, corners[2].y);
                float maxY = Mathf.Max(corners[0].y, corners[2].y);

                if (isText && component.gameObject.name == "seriesList")
                {
                    string listText = (string)component.GetType().GetProperty("text").GetValue(component);
                    MethodInfo measure = component.GetType().GetMethod("GetPreferredValues", new[] { typeof(string), typeof(float), typeof(float) });
                    foreach (string line in listText.Split('\n'))
                    {
                        float width = ((Vector2)measure.Invoke(component, new object[] { line.TrimEnd('\r'), 100000f, 0f })).x * rect.lossyScale.x;
                        if (width > (maxX - minX) + 0.5f)
                        {
                            problems.Add(state + ": a series-list row would wrap (" + width.ToString("0") + "px in a " + (maxX - minX).ToString("0") + "px box), which breaks the line budget: \"" + Short(line) + "\"");
                        }
                    }
                }
                bool hasContent = true;
                if (isText)
                {
                    string text = (string)component.GetType().GetProperty("text").GetValue(component);
                    hasContent = !string.IsNullOrEmpty(text);
                    if (hasContent)
                    {
                        // Auto-sized text is fitted by TMP at render time, but preferredHeight is measured at its
                        // maximum size, so it would call a fitted label too tall. Measure what was rendered instead,
                        // and ask TMP itself whether an Ellipsis/Truncate overflow mode cut anything off.
                        component.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) })
                            .Invoke(component, new object[] { false, false });
                        bool autoSized = (bool)component.GetType().GetProperty("enableAutoSizing").GetValue(component);
                        float measured = (float)component.GetType().GetProperty(autoSized ? "renderedHeight" : "preferredHeight").GetValue(component);
                        float boxHeight = rect.rect.height * rect.lossyScale.y;
                        float needed = measured * rect.lossyScale.y;
                        bool cutOff = (bool)component.GetType().GetProperty("isTextTruncated").GetValue(component);
                        if (needed > boxHeight + 1f || cutOff)
                        {
                            problems.Add(state + ": '" + component.gameObject.name + "' text needs " + needed.ToString("0")
                                + "px but its box is " + boxHeight.ToString("0") + "px (truncated" + (cutOff ? ", cut off by its overflow mode" : string.Empty)
                                + "): \"" + Short(text) + "\"");
                        }
                    }
                }

                if (hasContent && (minX < screen.xMin - 1f || maxX > screen.xMax + 1f || minY < screen.yMin - 1f || maxY > screen.yMax + 1f))
                {
                    problems.Add(state + ": '" + component.gameObject.name + "' leaves the screen: x " + minX.ToString("0") + ".."
                        + maxX.ToString("0") + ", y " + minY.ToString("0") + ".." + maxY.ToString("0") + " of " + Screen.width + "x" + Screen.height);
                }
            }
        }
    }

    private static string Short(string text)
    {
        text = text.Replace("\n", " / ");
        return text.Length > 70 ? text.Substring(0, 70) + "..." : text;
    }

    // ------------------------------------------------------------------ simultaneous steps

    /// <summary>
    /// Two virtual gamepads and no other device, so the launcher's device preflight and the gameplay scene's
    /// device plan see exactly two humans. Removes the machine's real devices for this process (as
    /// <see cref="InputTestFixture"/> always does); undone in <see cref="TearDown"/>.
    /// </summary>
    private void SeatTwoVirtualGamepads()
    {
        input = new InputTestFixture();
        input.Setup();
        padA = InputSystem.AddDevice<Gamepad>();
        padB = InputSystem.AddDevice<Gamepad>();
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
    }

    /// <summary>Presses Play Game and waits for the live gameplay scene with both humans seated and both attempts outstanding.</summary>
    private IEnumerator LaunchSharedGame(SeriesId id)
    {
        Button play = FindButton("playTurnButton");
        Assert.That(play.interactable, Is.True, "Play Game is not available: " + TextOf("turnMessage"));
        play.onClick.Invoke();

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        bool seated = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus
                && LevelRuntimeContext.instance != null
                && LevelRuntimeContext.instance.Players != null
                && LevelRuntimeContext.instance.Players.Count == 2)
            {
                seated = true;
                break;
            }

            yield return null;
        }

        Assert.That(seated, Is.True, "gameplay never started two humans from Play Game");
        yield return null;

        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.IsSimultaneous, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        Assert.That(ActiveMatch.Configuration.Roster.LocalHumanCount, Is.EqualTo(2));
    }

    /// <summary>
    /// Sets the two slots' scores and ends the match through the real <c>GameRules.RequestGameOver</c> and its
    /// match-end reporting, then waits for the pair to be recorded (the active attempt clears only then).
    /// </summary>
    private static IEnumerator EndSharedGame(int firstScore, int secondScore)
    {
        PlayerRegistry players = LevelRuntimeContext.instance.Players;
        players.GetBySlot(0).gameStats.TotalPoints = firstScore;
        players.GetBySlot(0).gameStats.ShotMade = 5;
        players.GetBySlot(0).gameStats.ShotAttempt = 10;
        players.GetBySlot(1).gameStats.TotalPoints = secondScore;
        players.GetBySlot(1).gameStats.ShotMade = 5;
        players.GetBySlot(1).gameStats.ShotAttempt = 10;

        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        Assert.That(rules, Is.Not.Null, "the gameplay scene has no GameRules");
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);

        float deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "GameRules never recorded the simultaneous result");
    }

    /// <summary>One whole game from the series screen: Play Game, end it through GameRules, Continue Series.</summary>
    private IEnumerator PlaySharedGame(SeriesId id, int firstScore, int secondScore)
    {
        yield return LaunchSharedGame(id);
        yield return EndSharedGame(firstScore, secondScore);
        yield return ContinueSeries();
    }

    /// <summary>
    /// Both attempts of <paramref name="gameIndex"/> are durably Completed and the game resolved. When ids are
    /// given they must be the very attempts named; without them, only that each participant has one.
    /// </summary>
    private static void AssertGameRecorded(VersusSeries series, int gameIndex, AttemptId firstAttempt, AttemptId secondAttempt)
    {
        Assert.That(series.Games[gameIndex].IsResolved, Is.True, "game " + (gameIndex + 1) + " is resolved");
        ParticipantGameView one = series.ViewFor(series.Participants.First.Id).Games[gameIndex];
        ParticipantGameView two = series.ViewFor(series.Participants.Second.Id).Games[gameIndex];
        Assert.That(one.OwnAttemptState, Is.EqualTo(AttemptState.Completed), "the first participant's attempt is durable");
        Assert.That(two.OwnAttemptState, Is.EqualTo(AttemptState.Completed), "the second participant's attempt is durable");
        Assert.That(one.OwnAttemptId.HasValue && two.OwnAttemptId.HasValue && one.OwnAttemptId.Value != two.OwnAttemptId.Value, Is.True, "two distinct attempts");
        if (firstAttempt.HasValue)
        {
            Assert.That(one.OwnAttemptId.Value, Is.EqualTo(firstAttempt.Value), "the first participant's original attempt");
        }

        if (secondAttempt.HasValue)
        {
            Assert.That(two.OwnAttemptId.Value, Is.EqualTo(secondAttempt.Value), "the second participant's original attempt");
        }
    }

    // ------------------------------------------------------------------ rendered-screen checks beyond the boundary check

    /// <summary>
    /// Every active control is on the screen, none overlaps another, no other text sits on top of one, a
    /// control is selected, every enabled control is reachable from it through the screen's own navigation
    /// graph (what a d-pad or arrow keys walk), and the mode-dependent controls match the selected series.
    /// </summary>
    private static void CheckScreenBehavior(string state, List<string> problems)
    {
        List<Selectable> controls = new List<Selectable>();
        List<Component> texts = new List<Component>();
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Selectable selectable in sceneRoot.GetComponentsInChildren<Selectable>(false))
            {
                controls.Add(selectable);
            }

            foreach (Component component in sceneRoot.GetComponentsInChildren<Component>(false))
            {
                if (component != null && component.GetType().Name.StartsWith("TextMeshPro"))
                {
                    texts.Add(component);
                }
            }
        }

        // control overlap, and text drawn over a control it does not belong to
        for (int a = 0; a < controls.Count; a++)
        {
            Rect one = ScreenRect(controls[a].transform as RectTransform);
            for (int b = a + 1; b < controls.Count; b++)
            {
                if (Overlap(one, ScreenRect(controls[b].transform as RectTransform)) > 1f)
                {
                    problems.Add(state + ": controls '" + controls[a].name + "' and '" + controls[b].name + "' overlap");
                }
            }

            foreach (Component text in texts)
            {
                string content = (string)text.GetType().GetProperty("text").GetValue(text);
                if (string.IsNullOrEmpty(content) || text.transform.IsChildOf(controls[a].transform))
                {
                    continue;
                }

                if (Overlap(one, ScreenRect(text.transform as RectTransform)) > 1f)
                {
                    problems.Add(state + ": text '" + text.gameObject.name + "' (\"" + Short(content) + "\") overlaps control '" + controls[a].name + "'");
                }
            }
        }

        // selection and navigation: something is selected, and everything enabled can be reached from it
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Selectable start = selected != null ? selected.GetComponent<Selectable>() : null;
        if (start == null || !start.IsActive() || !start.IsInteractable())
        {
            problems.Add(state + ": nothing usable is selected (" + (selected != null ? selected.name : "none") + ")");
        }
        else
        {
            HashSet<Selectable> reached = new HashSet<Selectable> { start };
            Queue<Selectable> frontier = new Queue<Selectable>();
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                Selectable current = frontier.Dequeue();
                foreach (Selectable next in new[] { current.FindSelectableOnUp(), current.FindSelectableOnDown(), current.FindSelectableOnLeft(), current.FindSelectableOnRight() })
                {
                    if (next != null && reached.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }

            foreach (Selectable control in controls)
            {
                if (control.IsInteractable() && !reached.Contains(control))
                {
                    problems.Add(state + ": '" + control.name + "' is not reachable by navigation from '" + start.name + "'");
                }
            }
        }

        // the controls that exist only for one kind of series
        MonoBehaviour controller = FindBehaviourByTypeName("LocalVersusController");
        object model = controller == null ? null : controller.GetType().GetProperty("Model").GetValue(controller);
        if (model != null)
        {
            bool simultaneous = (bool)model.GetType().GetProperty("IsSimultaneousSelected").GetValue(model);
            bool hasSeries = model.GetType().GetProperty("SelectedSeries").GetValue(model) != null;
            if (FindButton("character2Button").gameObject.activeInHierarchy != simultaneous)
            {
                problems.Add(state + ": Player 2's selector visibility (" + !simultaneous + ") does not match the selected series (simultaneous=" + simultaneous + ")");
            }

            if (!FindButton("modeButton").gameObject.activeInHierarchy)
            {
                problems.Add(state + ": the Mode selector is hidden");
            }

            string play = TextOf("playTurnButton");
            if (hasSeries && simultaneous && play != "Play Game")
            {
                problems.Add(state + ": a simultaneous series offers '" + play + "'");
            }

            if (hasSeries && !simultaneous && !play.StartsWith("Play Turn"))
            {
                problems.Add(state + ": an alternating series offers '" + play + "'");
            }

            if (hasSeries && !simultaneous && TextOf("seriesDetail").Contains("Both players play at once."))
            {
                problems.Add(state + ": an alternating series describes a same-time game");
            }
        }
    }

    private static Rect ScreenRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(
            Mathf.Min(corners[0].x, corners[2].x),
            Mathf.Min(corners[0].y, corners[2].y),
            Mathf.Max(corners[0].x, corners[2].x),
            Mathf.Max(corners[0].y, corners[2].y));
    }

    /// <summary>Area of the intersection, in pixels squared.</summary>
    private static float Overlap(Rect a, Rect b)
    {
        float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return width > 0f && height > 0f ? width * height : 0f;
    }

    // ------------------------------------------------------------------ steps

    private IEnumerator OpenLocalVersusFromStart()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return null;

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
        {
            if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start && EventSystem.current != null)
            {
                GameObject entry = GameObject.Find("local_versus_menu");
                if (entry != null)
                {
                    ExecuteEvents.Execute(entry, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                }
            }

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
        }

        Assert.That(
            SceneManager.GetActiveScene().name,
            Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus),
            "the Start screen's Local Versus entry never opened the Local Versus screen");

        yield return null;
        yield return null;

        MonoBehaviour controller = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "LocalVersusController");
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.enabled, Is.True, "LocalVersusController disabled itself");
    }

    /// <summary>Presses Play Turn and waits for the live gameplay scene, with the attempt outstanding.</summary>
    private IEnumerator LaunchTurn(SeriesId id, ParticipantId expected)
    {
        Button play = FindButton("playTurnButton");
        Assert.That(play.interactable, Is.True, "Play Turn is not available: " + TextOf("turnMessage"));
        play.onClick.Invoke();

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        PlayerController player = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    break;
                }
            }

            yield return null;
        }

        Assert.That(player, Is.Not.Null, "gameplay never started from Play Turn");
        yield return null;

        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        Assert.That(ActiveVersusAttempt.ParticipantId, Is.EqualTo(expected));
    }

    /// <summary>
    /// Reports the finished run through <see cref="VersusMatchReporter"/> - the exact call
    /// <c>GameRules.HandleMatchEnded</c> makes - since there is no player to score.
    /// </summary>
    private static void ReportResult(int score)
    {
        GameStats stats = UnityEngine.Object.FindAnyObjectByType<GameStats>();
        Assert.That(stats, Is.Not.Null, "the gameplay scene has no GameStats to report from");
        stats.TotalPoints = score;
        stats.ShotMade = 5;
        stats.ShotAttempt = 10;
        Assert.That(VersusMatchReporter.TryReport(stats, ActiveMatch.Configuration.ModeId, 90f), Is.True, "the turn's result was not stored");
    }

    /// <summary>"Continue Series": the pause menu's start-screen action, run to the point the series screen shows again.</summary>
    private IEnumerator ContinueSeries()
    {
        MonoBehaviour pause = FindBehaviourByTypeName("Pause");
        Assert.That(pause, Is.Not.Null, "the gameplay scene has no Pause component");
        MethodInfo method = pause.GetType().GetMethod("loadstartScreen", BindingFlags.Public | BindingFlags.Instance);
        pause.StartCoroutine((IEnumerator)method.Invoke(pause, null));

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo(Constants.SCENE_NAME_level_00_start));
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus));
        yield return null;
        yield return null;
    }

    // ------------------------------------------------------------------ helpers

    private static void RequireIsolatedPersistence()
    {
        if (!Application.productName.StartsWith(IsolatedProductNamePrefix, StringComparison.Ordinal))
        {
            Assert.Fail(
                "Refusing to run: the product name is '" + Application.productName + "', so Application.persistentDataPath ("
                + Application.persistentDataPath + ") is a developer's ordinary data directory. Run with the product name set to '"
                + IsolatedProductNamePrefix + "*' so the real GameRules match-end path cannot touch it.");
        }
    }

    private static void Log(string message)
    {
        Debug.Log("[LV-CERT] " + message);
    }

    private static string HandoffPath()
    {
        string path = Environment.GetEnvironmentVariable(HandoffEnvVar);
        Assert.That(path, Is.Not.Null.And.Not.Empty, HandoffEnvVar + " must name the handoff file");
        return path;
    }

    private void WriteHandoff(Dictionary<string, string> values)
    {
        Assert.That(
            Path.GetFullPath(HandoffPath()).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase),
            Is.False,
            "the handoff must live outside the repository directory");

        List<string> lines = new List<string>();
        foreach (KeyValuePair<string, string> pair in values)
        {
            lines.Add(pair.Key + "=" + pair.Value);
        }

        File.WriteAllLines(HandoffPath(), lines);
    }

    private static Dictionary<string, string> ReadHandoff()
    {
        Assert.That(File.Exists(HandoffPath()), Is.True, "no handoff file: run the earlier session first");
        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (string line in File.ReadAllLines(HandoffPath()))
        {
            int split = line.IndexOf('=');
            if (split > 0)
            {
                values[line.Substring(0, split)] = line.Substring(split + 1);
            }
        }

        return values;
    }

    private VersusSeries Durable(SeriesId id)
    {
        VersusSeries series = new FileVersusSeriesRepository(root).Load(id);
        Assert.That(series, Is.Not.Null, "the series is not on disk");
        return series;
    }

    private static ParticipantId FindParticipant(VersusSeries series, string displayName)
    {
        if (series.Participants.First.DisplayName == displayName)
        {
            return series.Participants.First.Id;
        }

        Assert.That(series.Participants.Second.DisplayName, Is.EqualTo(displayName));
        return series.Participants.Second.Id;
    }

    /// <summary>Independent oracle for whose turn it is, read from the stored series.</summary>
    private static ParticipantId ExpectedNext(VersusSeries series)
    {
        VersusGame game = series.CurrentGame;
        ParticipantId first = series.Participants.At(game.FirstAttemptParticipantIndex).Id;
        return series.ViewFor(first).CurrentGame.OwnAttemptState == AttemptState.Completed
            ? series.Participants.Opponent(first).Id
            : first;
    }

    private static Button FindButton(string objectName)
    {
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Button button in sceneRoot.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == objectName)
                {
                    return button;
                }
            }
        }

        Assert.Fail("no button named '" + objectName + "' in " + SceneManager.GetActiveScene().name);
        return null;
    }

    /// <summary>Reads a TextMeshPro label by object name without referencing the TMP assembly.</summary>
    private static string TextOf(string objectName)
    {
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != objectName)
                {
                    continue;
                }

                foreach (Component component in t.GetComponents<Component>())
                {
                    PropertyInfo text = component == null ? null : component.GetType().GetProperty("text");
                    if (text != null && text.PropertyType == typeof(string) && component.GetType().Name.StartsWith("TextMeshPro"))
                    {
                        return (string)text.GetValue(component);
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>Types into a TMP input field by object name, as a player would, without referencing TMP.</summary>
    private static void SetInput(string objectName, string value)
    {
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != objectName)
                {
                    continue;
                }

                foreach (Component component in t.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "TMP_InputField")
                    {
                        component.GetType().GetProperty("text").SetValue(component, value);
                        return;
                    }
                }
            }
        }

        Assert.Fail("no input field named '" + objectName + "'");
    }

    private static MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
            {
                return behaviour;
            }
        }

        return null;
    }

    /// <summary>The real on-disk store, until told to refuse saves. Loads always read the disk.</summary>
    private sealed class FailableRepository : IVersusSeriesRepository
    {
        private readonly IVersusSeriesRepository inner;

        public FailableRepository(IVersusSeriesRepository inner)
        {
            this.inner = inner;
        }

        public bool FailSaves { get; set; }

        public int RefusedSaves { get; private set; }

        public bool Save(VersusSeries series)
        {
            if (FailSaves)
            {
                RefusedSaves++;
                return false;
            }

            return inner.Save(series);
        }

        public VersusSeries Load(SeriesId id) => inner.Load(id);

        public bool Exists(SeriesId id) => inner.Exists(id);

        public IReadOnlyList<SeriesSummary> ListSummaries() => inner.ListSummaries();

        public bool Delete(SeriesId id) => inner.Delete(id);

        public bool Archive(SeriesId id) => inner.Archive(id);
    }
}
#endif
