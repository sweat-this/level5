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

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();
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

#if UNITY_EDITOR
        UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "lv-cert 1920x1080");
        yield return null;
        yield return null;
#endif
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

    private static SeriesId CreateSeries(string first, string second, int games)
    {
        RulesetId ruleset = VersusCatalogs.Rulesets.Supporting(VersusModes.RequiredCapability(VersusMode.LocalAlternating))[0].Id;
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
            VersusMode.LocalAlternating,
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
        yield return new WaitForEndOfFrame();
        yield return null;
        yield return new WaitForEndOfFrame();

        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);

        List<string> found = new List<string>();
        CheckLayout(name, found);
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
                        float preferredHeight = (float)component.GetType().GetProperty("preferredHeight").GetValue(component);
                        float boxHeight = rect.rect.height * rect.lossyScale.y;
                        float needed = preferredHeight * rect.lossyScale.y;
                        if (needed > boxHeight + 1f)
                        {
                            problems.Add(state + ": '" + component.gameObject.name + "' text needs " + needed.ToString("0")
                                + "px but its box is " + boxHeight.ToString("0") + "px (truncated): \"" + Short(text) + "\"");
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
