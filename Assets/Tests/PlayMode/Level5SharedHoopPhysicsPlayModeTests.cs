#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Controlled Rigidbody/trigger execution in the production Scrapyard Versus CPU match. Positions
/// are driven by the fixture; this is not aiming, natural flight, input, or player-build certification.
/// </summary>
public class Level5SharedHoopPhysicsPlayModeTests
{
    private LocalProfileTestDatabase database;
    private bool previousIgnoreLogs;

    [SetUp]
    public void SetUp()
    {
        // The invocation must use a disposable product namespace as well as disposable SQLite:
        // scene bootstrap and existing local-versus fixtures can access persistent JSON stores.
        if (Application.productName != "level5-gp001-f4-tests")
            Assert.Ignore("Requires the disposable level5-gp001-f4-tests product namespace; never use real player persistence.");
        previousIgnoreLogs = LogAssert.ignoreFailingMessages;
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            ActiveMatch.Clear();
            yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("shared-hoop-physics-cleanup");
        }
        finally
        {
            database?.Dispose();
            database = null;
            LogAssert.ignoreFailingMessages = previousIgnoreLogs;
        }
    }

    private IEnumerator LaunchArena()
    {
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        float deadline = Time.realtimeSinceStartup + 90f;
        do
        {
            yield return null;
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "production menu/catalogs did not load");
        }
        while (!MatchCatalogs.IsReady || SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_start
            || GameObject.Find("press_start") == null);

        CharacterProfile human = Resources.LoadAll<GameObject>("Prefabs/menu_start/default_shooter_profiles")
            .Select(prefab => prefab.GetComponent<CharacterProfile>()).Where(profile => profile != null && profile.IsShooter)
            .First(profile => Resources.Load<GameObject>(Constants.PREFAB_PATH_CHARACTER_human + profile.PlayerObjectName) != null);
        CharacterProfile cpu = Resources.LoadAll<GameObject>("Prefabs/menu_start/cpu_players_selected_objects")
            .Select(prefab => prefab.GetComponent<CharacterProfile>()).Where(profile => profile != null && profile.IsShooter)
            .First(profile => Resources.Load<GameObject>(Constants.PREFAB_PATH_CHARACTER_cpu + profile.PlayerObjectName) != null);
        CharacterSelection Select(CharacterProfile p) => new CharacterSelection(
            p.PlayerId, p.PlayerObjectName, p.PlayerDisplayName, p.IsShooter, p.IsFighter);
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(Select(human)),
            PlayerRosterEntry.Cpu(Select(cpu))
        });
        MatchBuildResult build = MatchCatalogs.Builder.Build(new MatchRequest(
            GameModeId.VersusCpu, 1, roster, MatchModifiers.Default, source: "GP-001 F4 physics regression"));
        Assert.That(build.Succeeded, Is.True, build.Validation.ToString());
        Assert.That(build.Configuration.SceneName, Is.EqualTo("level_01_scrapyard"));
        ActiveMatch.Begin(build.Configuration);
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(build.Configuration.SceneName, _ => { });
        yield return null;
    }

    [UnityTest]
    public IEnumerator HumanAndCpuScoreInOnePhysicsStepAndRenderedFrameOnTheAuthoredHoop()
    {
        // Unity gives the test body its own log scope, separate from SetUp.
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        yield return LaunchArena();
        BasketBallShotMade upper = Object.FindAnyObjectByType<BasketBallShotMade>();
        BasketBallShotMadeCollision lower = Object.FindAnyObjectByType<BasketBallShotMadeCollision>();
        Assert.That(upper, Is.Not.Null);
        Assert.That(lower, Is.Not.Null);
        Assert.That(RealScenePlayModeTestSupport.GetField(lower, "basketBallShotMade"), Is.SameAs(upper));
        Collider upperCollider = upper.GetComponent<Collider>();
        Collider lowerCollider = lower.GetComponent<Collider>();
        Assert.That(upperCollider.isTrigger, Is.True);
        Assert.That(lowerCollider.isTrigger, Is.True);

        IBasketballRuntime human = Object.FindAnyObjectByType<BasketBall>();
        IBasketballRuntime cpu = Object.FindAnyObjectByType<BasketBallAuto>();
        Assert.That(human, Is.Not.Null);
        Assert.That(cpu, Is.Not.Null);
        Assert.That(human.IsCpu, Is.False);
        Assert.That(cpu.IsCpu, Is.True);
        Assert.That(human.ParticipantId, Is.Not.EqualTo(cpu.ParticipantId));
        IBasketballRuntime[] balls = { human, cpu };
        Rigidbody[] bodies = balls.Select(ball => ((Component)ball).GetComponent<Rigidbody>()).ToArray();
        Vector3[] centerOffsets = bodies.Select(body => body.transform.TransformVector(body.GetComponent<SphereCollider>().center)).ToArray();
        int[] before = balls.Select(ball => ball.Stats.Stats.TotalPoints).ToArray();
        List<MadeShotResult> results = new List<MadeShotResult>();
        List<int> eventSteps = new List<int>();
        List<int> eventFrames = new List<int>();
        int step = 0;
        Action<MadeShotResult> resolved = result =>
        {
            results.Add(result);
            eventSteps.Add(step);
            eventFrames.Add(Time.frameCount);
        };
        upper.ShotResolved += resolved;
        SimulationMode previousMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            foreach (Rigidbody body in bodies)
            {
                body.isKinematic = false;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                body.linearVelocity = Vector3.zero;
            }
            foreach (IBasketballRuntime ball in balls)
                BasketballShotPipeline.ApplyShotAttempt(ball, false, true, false, false);

            void MoveAndStep(Vector3 position)
            {
                for (int i = 0; i < bodies.Length; i++) bodies[i].position = position - centerOffsets[i];
                Physics.SyncTransforms();
                step++;
                Physics.Simulate(Time.fixedDeltaTime);
            }

            // Real collider volumes and real Rigidbody callbacks; no SendMessage/reflection scoring.
            Vector3 above = upperCollider.bounds.center + Vector3.up * 2f;
            MoveAndStep(above);
            MoveAndStep(upperCollider.bounds.center);
            Assert.That(results.Count, Is.Zero, "upper crossings alone never score");
            int scoringStep = step + 1;
            MoveAndStep(lowerCollider.bounds.center);
            Assert.That(results.Count, Is.EqualTo(2), "both real balls must score through the wired lower adapter");
            Assert.That(eventSteps, Is.All.EqualTo(scoringStep));
            Assert.That(eventFrames.Distinct().Count(), Is.EqualTo(1));
            Assert.That(results.Count(result => result.PlayerId == human.ParticipantId && !result.IsCpu), Is.EqualTo(1));
            Assert.That(results.Count(result => result.PlayerId == cpu.ParticipantId && result.IsCpu), Is.EqualTo(1));
            for (int i = 0; i < balls.Length; i++)
                Assert.That(balls[i].Stats.Stats.TotalPoints, Is.EqualTo(before[i] + 3));

            MoveAndStep(above);
            MoveAndStep(upperCollider.bounds.center);
            MoveAndStep(lowerCollider.bounds.center);
            Assert.That(results.Count, Is.EqualTo(2), "a rebound cannot resolve either attempt twice");

            MoveAndStep(above);
            foreach (IBasketballRuntime ball in balls)
                BasketballShotPipeline.ApplyShotAttempt(ball, false, true, false, false);
            MoveAndStep(upperCollider.bounds.center);
            MoveAndStep(lowerCollider.bounds.center);
            Assert.That(results.Count, Is.EqualTo(4), "new attempts from both balls can score again");
        }
        finally
        {
            Physics.simulationMode = previousMode;
            upper.ShotResolved -= resolved;
        }
    }
}
#endif
