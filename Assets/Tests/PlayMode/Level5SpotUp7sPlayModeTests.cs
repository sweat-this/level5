#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Real authored-mode/arena integration. Attempts and hoop makes are injected at existing production
/// seams; this does not certify aiming, input, flight physics, or five-make content intent.
/// </summary>
public class Level5SpotUp7sPlayModeTests
{
    private LocalProfileTestDatabase database;

    [SetUp]
    public void SetUp() => RealScenePlayModeTestSupport.IgnoreSceneLogNoise();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        ActiveMatch.Clear();
        yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("spot-up-7s-cleanup");
        database?.Dispose();
        database = null;
    }

    private IEnumerator LaunchCourse()
    {
        yield return GameplayScenePlayModeHarness.EnterPlayableGameplayScene(_ => { });
        MatchConfiguration initial = ActiveMatch.Configuration;
        Assert.That(initial, Is.Not.Null);
        database = new LocalProfileTestDatabase();
        yield return database.Open();

        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(initial.Roster.GetBySlotId(0).Character)
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
        Assert.That(summary.text, Does.Contain("Your time was"));
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
    }
}
#endif
