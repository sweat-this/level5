#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 3 Slice 71: <c>BasketballShotPipeline.ApplyShotAttempt</c>/<c>ApplyLaunchResult</c> are
/// now the single implementation <c>BasketBall.shootBasketBall</c>/<c>Launch</c> and
/// <c>BasketBallAuto</c>'s own copies both call through. The EditMode coverage for this slice
/// (<c>Level5BasketballShotPipelineTests</c>, <c>Level5BasketBallCriticalSuccessPresentationTests</c>,
/// <c>Level5BasketBallShotTelemetryTests</c>) drives <c>Start()</c>/private <c>Launch()</c> by
/// reflection with no running coroutine host - <c>StartCoroutine(LaunchBasketBall())</c> never actually
/// executes outside Play Mode. This file drives the real public <c>shootBasketBall()</c> entry point on
/// a hand-composed human ball and CPU ball and lets Unity actually tick the coroutine, proving the
/// extracted shared methods still run to completion through the real launch path for both roles -
/// exactly the regression risk this slice's own extraction could have introduced.
///
/// The ball GameObject is built inactive and composed/bound before activation, so Unity's own engine
/// runs each component's <c>Start()</c> exactly once, naturally, on the first yielded frame after
/// activation - not via reflection. Reflectively invoking <c>Start()</c> ourselves and then also
/// letting a real Play Mode frame boundary trigger the engine's own (unaware) first call would run it
/// twice; both bodies happen to be idempotent today, but that would be a latent trap for whichever one
/// stops being idempotent later.
/// </summary>
public class Level5BasketballShotAttemptAndLaunchConvergencePlayModeTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    /// <summary>
    /// A coroutine teardown (rather than plain <c>[TearDown]</c>) so the trailing <c>yield return
    /// null</c> lets every <c>Object.Destroy</c> call below actually complete - it defers destruction
    /// to end-of-frame rather than removing the object synchronously - before the next test's own
    /// build spawns a same-named object (<c>"basketBall_target"</c>, <c>"basketBall_position"</c>,
    /// <c>"drop shadow"</c>). Without this, two same-named objects could transiently coexist and make
    /// <c>BasketBallState.Start()</c>'s own <c>GameObject.Find("basketBall_target")</c> nondeterministic
    /// about which instance it resolves. Matches the yield-based teardown shape this repo's other
    /// PlayMode fixtures already use (e.g. <c>BasketballVisibilityTests</c>).
    /// </summary>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        BasketBall.instance = null;
        BasketBallAuto.instance = null;

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }

        spawned.Clear();

        yield return null;
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    /// <summary>
    /// <paramref name="shotMeterEnded"/> is set to the value that lets each role's intentionally
    /// distinct <c>LaunchBasketBall</c> wait predicate (human waits for <c>false</c>, CPU waits for
    /// <c>true</c> - preserved, not converged, by this slice) succeed immediately once the real
    /// coroutine starts ticking.
    /// </summary>
    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; } = true;
        public bool FacingFront => true;
        public bool Grounded => true;
        public bool InAir => false;
        public bool InDunkState => false;
        public float DistanceFromRim => 0f;
        public ShooterAttributes ShooterAttributes { get; }
        public int Clutch => 0;
        public float ShotMeterSliderValue { get; set; } = 50f;
        public bool ShotMeterEnded { get; set; }
        public int EndShootCycleCallCount { get; private set; }

        public FakeShooterActor(bool shotMeterEnded)
        {
            ShooterAttributes = new ShooterAttributes(
                displayName: "fake-shooter", accuracyTwoPoint: 80, accuracyThreePoint: 70, accuracyFourPoint: 60,
                accuracySevenPoint: 50, shootAngle: 45, range: 10000, release: 50, luck: 0, jumpForce: 0, runSpeed: 0);
            ShotMeterEnded = shotMeterEnded;
        }

        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() => EndShootCycleCallCount++;
    }

    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight => 0f;
    }

    private BasketBall BuildHumanBall(FakeShooterActor actor)
    {
        GameObject playerGo = Spawn("human-actor");
        Spawn("basketBall_position").transform.parent = playerGo.transform;

        GameObject ballGo = Spawn("human-ball");
        // Deferred until every component is added and bound below - see the class doc comment.
        ballGo.SetActive(false);
        BasketBallState state = ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        Spawn("drop shadow").transform.parent = ballGo.transform;

        state.TwoPoints = true;
        // Named to match BasketBallState.Start()'s own GameObject.Find("basketBall_target") - real
        // Play Mode fires that Start() naturally once this GameObject activates, which would otherwise
        // clobber this assignment back to null.
        state.BasketBallTarget = Spawn("basketBall_target");
        state.BasketBallTarget.transform.position = new Vector3(0f, 0f, 20f);
        state.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        BasketBall ball = ballGo.AddComponent<BasketBall>();
        ball.BindOwner(0, isCpu: false, isPrimary: true, playerGo, actor);
        ball.BindGroundHeightProvider(new FakeGroundHeightProvider());
        ball.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        ballGo.SetActive(true);
        return ball;
    }

    private BasketBallAuto BuildCpuBall(FakeShooterActor actor)
    {
        GameObject autoPlayerGo = Spawn("cpu-actor");
        Spawn("basketBall_position").transform.parent = autoPlayerGo.transform;

        GameObject ballGo = Spawn("cpu-ball");
        ballGo.SetActive(false);
        BasketBallState state = ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        Spawn("drop shadow").transform.parent = ballGo.transform;

        state.ThreePoints = true;
        // See BuildHumanBall's matching comment on the exact name requirement.
        state.BasketBallTarget = Spawn("basketBall_target");
        state.BasketBallTarget.transform.position = new Vector3(0f, 0f, 20f);
        state.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        BasketBallAuto ball = ballGo.AddComponent<BasketBallAuto>();
        ball.BindOwner(1, isCpu: true, isPrimary: false, autoPlayerGo, actor);
        ball.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        ballGo.SetActive(true);
        return ball;
    }

    [UnityTest]
    public IEnumerator HumanShootBasketBallRunsTheRealCoroutineThroughToTheSharedLaunchApplication()
    {
        FakeShooterActor actor = new FakeShooterActor(shotMeterEnded: false);
        BasketBall ball = BuildHumanBall(actor);

        // let the engine's own Start() run for every newly-activated component before touching
        // anything Start() populates (basketBallPosition, gameStats, rigidbody, ...).
        yield return null;

        GameStats stats = ball.GameStats;
        ball.shootBasketBall(two: true, three: false, four: false, seven: false);

        for (int i = 0; i < 10 && actor.EndShootCycleCallCount == 0; i++)
        {
            yield return null;
        }

        Assert.That(stats.Stats.TwoPointerAttempts, Is.EqualTo(1),
            "ApplyShotAttempt must have run through the real shootBasketBall() entry point");
        Assert.That(ball.BasketBallState.TwoAttempt, Is.True);
        Assert.That(actor.EndShootCycleCallCount, Is.EqualTo(1),
            "ApplyLaunchResult must have run once the real coroutine reached Launch()");
        Assert.IsFalse(actor.HasBasketball, "the shared launch application must clear HasBasketball");
        Assert.That(ball.Rigidbody.linearVelocity, Is.Not.EqualTo(Vector3.zero),
            "the shared launch application must apply the computed launch velocity");
    }

    [UnityTest]
    public IEnumerator CpuShootBasketBallRunsTheRealCoroutineThroughToTheSharedLaunchApplication()
    {
        FakeShooterActor actor = new FakeShooterActor(shotMeterEnded: true);
        BasketBallAuto ball = BuildCpuBall(actor);

        yield return null;

        GameStats stats = ball.GameStats;
        ball.shootBasketBall(two: false, three: true, four: false, seven: false);

        for (int i = 0; i < 10 && actor.EndShootCycleCallCount == 0; i++)
        {
            yield return null;
        }

        Assert.That(stats.Stats.ThreePointerAttempts, Is.EqualTo(1),
            "ApplyShotAttempt must have run through the real shootBasketBall() entry point");
        Assert.That(ball.BasketBallState.ThreeAttempt, Is.True);
        Assert.That(actor.EndShootCycleCallCount, Is.EqualTo(1),
            "ApplyLaunchResult must have run once the real coroutine reached Launch()");
        Assert.IsFalse(actor.HasBasketball, "the shared launch application must clear HasBasketball");

        Rigidbody rigidbody = RealScenePlayModeTestSupport.GetField<Rigidbody>(ball, "rigidbody");
        Assert.That(rigidbody, Is.Not.Null);
        Assert.That(rigidbody.linearVelocity, Is.Not.EqualTo(Vector3.zero),
            "the shared launch application must apply the computed launch velocity");
    }
}
#endif
