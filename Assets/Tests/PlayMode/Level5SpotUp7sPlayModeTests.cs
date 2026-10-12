#if UNITY_INCLUDE_TESTS
using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Level5.BackendV2;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Real authored-mode/arena integration. Attempts and hoop makes are injected at existing production
/// seams; this does not certify aiming, input, flight physics, or five-make content intent.
/// </summary>
public class Level5SpotUp7sPlayModeTests
{
    private readonly Dictionary<string, byte[]> preservedFiles = new Dictionary<string, byte[]>();
    private LocalProfileTestDatabase database;
    private BackendV2Session previousSession;
    private Guid owner;
    private RetryableResultTransport transport;

    [SetUp]
    public void SetUp()
    {
        previousSession = BackendV2SessionStore.Current;
        // SQLite redirection does not isolate JSON queues or the session-change disk mirror.
        // Read every file before deleting any, so a failed snapshot cannot partially erase data.
        foreach (string name in new[] { "backendv2_pending_match_results.json", "pending-match-persistence.json", "backendv2_session.json" })
        {
            foreach (string suffix in new[] { "", ".bak" })
            {
                string path = Path.Combine(Application.persistentDataPath, name + suffix);
                preservedFiles[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
        }
        try
        {
            foreach (string path in preservedFiles.Keys) File.Delete(path);
            transport = new RetryableResultTransport();
            BackendV2Runtime.Override(transport);
            owner = Guid.NewGuid();
            BackendV2SessionStore.Clear();
            RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        }
        catch
        {
            RestorePersistentFiles();
            throw;
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            ActiveMatch.Clear();
            yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("spot-up-7s-cleanup");
        }
        finally
        {
            try
            {
                database?.Dispose();
                database = null;
                BackendV2SessionStore.Clear();
                if (previousSession != null) BackendV2SessionStore.Set(previousSession);
                BackendV2Runtime.Reset();
            }
            finally
            {
                RestorePersistentFiles();
            }
        }
    }

    private void RestorePersistentFiles()
    {
        foreach (var file in preservedFiles)
        {
            if (file.Value == null) File.Delete(file.Key);
            else File.WriteAllBytes(file.Key, file.Value);
        }
        preservedFiles.Clear();
    }

    private IEnumerator LaunchCourse()
    {
        // Redirect SQLite before menu/loading bootstrap can access any player database.
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        // Load production catalogs without launching the menu's remembered (possibly non-shooting)
        // character. This fixture tests the authored course rather than a player's menu draft.
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return null;
        yield return null;
        float deadline = Time.realtimeSinceStartup + 90f;
        while (!MatchCatalogs.IsReady || SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_start
            || GameObject.Find("press_start") == null)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "production catalogs/menu did not load");
            yield return null;
        }
        CharacterProfile shooter = Resources.LoadAll<GameObject>("Prefabs/menu_start/default_shooter_profiles")
            .Select(prefab => prefab.GetComponent<CharacterProfile>()).First(profile => profile != null && profile.IsShooter);
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(new CharacterSelection(shooter.PlayerId, shooter.PlayerObjectName,
                shooter.PlayerDisplayName, shooter.IsShooter, shooter.IsFighter))
        });
        MatchBuildResult result = MatchCatalogs.Builder.Build(new MatchRequest(
            GameModeId.SpotUp7s, 1, roster, MatchModifiers.Default, source: "Spot-Up 7s regression"));
        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        Assert.That(result.Configuration.SceneName, Is.EqualTo("level_01_scrapyard"));
        Assert.That(result.Configuration.Rules.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.SevenPoint));
        ActiveMatch.Begin(result.Configuration);
        // The same one-way bridge used by the production launch and existing real-scene tests.
        Assembly.Load("Assembly-CSharp").GetType("LegacyGameOptionsBridge", true)
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { result.Configuration });
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(result.Configuration.SceneName, _ => { });
        for (int frame = 0; frame < 10; frame++)
        {
            yield return null;
        }
        Assert.That(MatchController.instance.IsPlaying, Is.True);
        Assert.That(Timer.instance.TimeStart, Is.EqualTo(180f));
        BackendV2SessionStore.Set(new BackendV2Session("test-access", DateTimeOffset.UtcNow.AddHours(1),
            owner, "test-refresh", DateTimeOffset.UtcNow.AddDays(1)));
    }

    private static BasketBallShotMarker[] AssertCourseComposition(IShotMarkerSession session)
    {
        BasketBallShotMarker[] all = Object.FindObjectsByType<BasketBallShotMarker>(FindObjectsInactive.Include);
        BasketBallShotMarker[] course = all.Where(marker => marker.ShotTypeSeven).ToArray();
        Assert.That(course, Is.Not.Empty);
        Assert.That(all.Any(marker => marker.ShotTypeThree || marker.ShotTypeFour), Is.True);
        foreach (BasketBallShotMarker marker in all)
        {
            Assert.That(marker.isActiveAndEnabled, Is.EqualTo(marker.ShotTypeSeven), marker.name);
            if (marker.ShotTypeSeven)
            {
                Assert.That(marker.MarkerEnabled, Is.True, marker.name);
                Assert.That(marker.MaxShotMade, Is.GreaterThan(0), marker.name);
                TestContext.WriteLine($"Scrapyard/{marker.name}: make quota={marker.MaxShotMade}, attempt quota={marker.MaxShotAttempt}");
            }
        }
        Assert.That(session.MarkersRemaining, Is.EqualTo(course.Length));
        return course;
    }

    private static void BeginAttempt(IBasketballRuntime ball, BasketBallShotMarker marker, IMoneyBallState money)
    {
        BasketballShotPipeline.ApplyShotAttempt(ball, two: false, three: false, four: false, seven: true);
        ball.State.EnterShotMarker(marker);
        BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(ball, money, ActiveMatch.Configuration.Rules);
    }

    private static IEnumerator AssertResults(object gameRules, MatchEndCause cause, int expectedMakes)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (MatchController.instance.Phase != MatchPhase.Completed && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Completed), "durable match-end work must finish");
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(cause));
        Assert.That(BasketBall.instance.GameStats.Stats.SevenPointerMade, Is.EqualTo(expectedMakes));
        object hud = RealScenePlayModeTestSupport.GetField(gameRules, "hud");
        Text summary = RealScenePlayModeTestSupport.GetField<Text>(hud, "displayScoreText");
        Assert.That(summary, Is.Not.Null);
        float elapsed = BasketBall.instance.GameStats.Stats.TimePlayed;
        int minutes = Mathf.FloorToInt(elapsed / 60f);
        Assert.That(summary.text, Does.Contain("Your time was " + minutes.ToString("0") + ":"
            + (elapsed - minutes * 60f).ToString("00.000")), "the first summary must use finalized elapsed time");
    }

    [UnityTest]
    public IEnumerator AuthoredCourseCountsMakesAndEndsOnceAfterTheFinalMarker()
    {
        yield return LaunchCourse();
        object gameRules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
        IShotMarkerSession session = (IShotMarkerSession)gameRules;
        BasketBallShotMarker[] course = AssertCourseComposition(session);
        IBasketballRuntime ball = BasketBall.instance;
        IMoneyBallState money = (IMoneyBallState)gameRules;
        int endings = 0;
        int completions = 0;
        MatchController.instance.Ending += _ => endings++;
        MatchController.instance.Completed += _ => completions++;
        int madeTotal = 0;

        for (int index = 0; index < course.Length; index++)
        {
            BasketBallShotMarker marker = course[index];
            // Resolve a miss by beginning the next attempt with no hoop callback.
            BeginAttempt(ball, marker, money);
            ball.State.ResetShotAttemptSnapshot();
            yield return null;
            Assert.That(marker.ShotMade, Is.Zero);
            Assert.That(marker.MarkerEnabled, Is.True);
            Assert.That(session.MarkersRemaining, Is.EqualTo(course.Length - index));

            for (int made = 1; made <= marker.MaxShotMade; made++)
            {
                BeginAttempt(ball, marker, money);
                BasketBallShotMade.instance.SendMessage("OnTriggerEnter", ((Component)ball).GetComponent<Collider>());
                BasketBallShotMade.instance.shotMade(ball);
                madeTotal++;
                yield return null;
                Assert.That(marker.ShotMade, Is.EqualTo(made));
                Assert.That(marker.MarkerEnabled, Is.EqualTo(made < marker.MaxShotMade));
            }
            yield return null;
            Assert.That(session.MarkersRemaining, Is.EqualTo(course.Length - index - 1));
            Assert.That(endings, Is.EqualTo(index == course.Length - 1 ? 1 : 0));
        }
        // Existing RequestGameOver routes Unknown first; HandleMatchEnded's later ObjectiveComplete
        // cannot replace the accepted cause. See the separate finding in the restoration report.
        yield return AssertResults(gameRules, MatchEndCause.Unknown, madeTotal);
        yield return null;
        Assert.That(endings, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
        Assert.That(ball.Stats.Stats.TotalPoints, Is.EqualTo(7 * madeTotal));
        AssertPersistence(gameRules, expectedScores: 1);
        // Repeated handling cannot write again or change the captured result.
        RealScenePlayModeTestSupport.Invoke(gameRules, "HandleMatchEnded");
        AssertPersistence(gameRules, expectedScores: 1);
    }

    [UnityTest]
    public IEnumerator AuthoredCourseCanTimeOutWithoutCompletingAnyMarker()
    {
        yield return LaunchCourse();
        object gameRules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
        IShotMarkerSession session = (IShotMarkerSession)gameRules;
        BasketBallShotMarker[] course = AssertCourseComposition(session);
        int endings = 0;
        MatchController.instance.Ending += _ => endings++;
        BasketBall.instance.GetComponent<BasketBallState>().Thrown = false;
        // Advance only elapsed time, preserving the authored 180-second duration and expiry routing.
        RealScenePlayModeTestSupport.SetField(Timer.instance, "currentTime", 180f);
        yield return null;
        yield return AssertResults(gameRules, MatchEndCause.TimeExpired, 0);
        Assert.That(session.MarkersRemaining, Is.EqualTo(course.Length));
        Assert.That(course.All(marker => marker.MarkerEnabled && marker.ShotMade == 0), Is.True);
        Assert.That(endings, Is.EqualTo(1));
        AssertPersistence(gameRules, expectedScores: 0);
    }

    private void AssertPersistence(object gameRules, int expectedScores)
    {
        Assert.That(database.CountRows("HighScores", "modeid", 25), Is.EqualTo(expectedScores));
        Assert.That(database.CountRows("MatchPersistenceLedger"), Is.EqualTo(1), "all-time stats must persist");
        Assert.That((bool)RealScenePlayModeTestSupport.GetField(gameRules, "matchScoreSaveCompleted"), Is.True);
        Assert.That((bool)RealScenePlayModeTestSupport.GetField(gameRules, "matchAllTimeStatsSaved"), Is.True);
        Assert.That((bool)RealScenePlayModeTestSupport.GetField(gameRules, "matchProgressionApplied"), Is.True);
        Assert.That((bool)RealScenePlayModeTestSupport.GetField(gameRules, "progressionPersistenceFailed"), Is.False);
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(expectedScores));
        Assert.That(transport.ResultRequests, Is.EqualTo(expectedScores), "failed courses must not reach ordinary submission");
        Type pendingType = Assembly.Load("Assembly-CSharp").GetType("PendingMatchPersistenceStore", true);
        object pending = pendingType.GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        ICollection scores = (ICollection)pending.GetType().GetField("scores").GetValue(pending);
        Assert.That(scores.Count, Is.Zero, "SQLite success and failed courses must leave no pending ordinary score");
    }

    [UnityTest]
    public IEnumerator DeathBeforeCourseCompletionCannotCreateARecordEvenAfterLateMarkerUpdates()
    {
        yield return LaunchCourse();
        object gameRules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
        IShotMarkerSession session = (IShotMarkerSession)gameRules;
        AssertCourseComposition(session);
        // Exercise the real death subscriber and accepted lifecycle transition, then late callbacks
        // before GameRules.Update gets to persist anything.
        RealScenePlayModeTestSupport.Invoke(gameRules, "OnPlayerDied");
        while (session.MarkersRemaining > 0) session.RecordMarkerCompleted();
        session.RequestMatchEnd();
        yield return AssertResults(gameRules, MatchEndCause.PlayerDied, 0);
        AssertPersistence(gameRules, expectedScores: 0);
        RealScenePlayModeTestSupport.Invoke(gameRules, "HandleMatchEnded");
        AssertPersistence(gameRules, expectedScores: 0);
    }

    private sealed class RetryableResultTransport : IApiTransport
    {
        public int ResultRequests { get; private set; }

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            if (request.RelativePath == "api/v2/match-results") ResultRequests++;
            // Keep a successfully queued score pending, without contacting a live Backend.
            completed(RawApiResponse.Completed(503, "{}"));
            yield break;
        }
    }
}
#endif
