using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-017: <see cref="BasketballShotPipeline"/> is the single shot-launch computation
/// <see cref="BasketBall"/> (human) and <see cref="BasketBallAuto"/> (CPU) now both call, replacing
/// what used to be two independently-maintained copies. These tests exercise it directly - it had
/// no coverage of its own before the extraction, only the arithmetic it delegates to
/// (<see cref="ShotModifiers"/>, covered by <see cref="Level5ShotModifierTests"/>).
/// </summary>
public class Level5BasketballShotPipelineTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private CharacterProfile MakeProfile(int luck, int range, int release, int shootAngle)
    {
        CharacterProfile profile = Spawn("profile").AddComponent<CharacterProfile>();
        profile.Luck = luck;
        profile.Range = range;
        profile.Release = release;
        profile.ShootAngle = shootAngle;
        profile.Accuracy2Pt = 80;
        profile.Accuracy3Pt = 70;
        return profile;
    }

    private BasketBallState MakeState(bool twoPoints)
    {
        BasketBallState state = Spawn("basketball-state").AddComponent<BasketBallState>();
        state.TwoPoints = twoPoints;
        state.ThreePoints = !twoPoints;
        state.BasketBallTarget = Spawn("target");
        state.BasketBallTarget.transform.position = new Vector3(0f, 0f, 20f);
        return state;
    }

    private GameStats MakeStats()
    {
        return Spawn("stats").AddComponent<GameStats>();
    }

    /// <summary>
    /// luck = 100 always rolls critical (<see cref="PercentChance.Succeeds"/> treats >=100 as
    /// certain regardless of the draw), and a range far beyond the shot distance always reaches the
    /// rim with no roll - so this case needs no RNG seeding to be deterministic. Both conditions
    /// zero every modifier, so the shot must resolve as a swish with no aim/release/range error.
    /// </summary>
    [Test]
    public void CertainLuckAndInRangeShotIsAlwaysASwishWithNoModifiers()
    {
        CharacterProfile profile = MakeProfile(luck: 100, range: 10000, release: 50, shootAngle: 45);
        BasketBallState state = MakeState(twoPoints: true);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            profile,
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 50f);

        Assert.That(result.Critical, Is.True);
        Assert.That(result.IsSwish, Is.True);
        Assert.That(result.ShotMeterMessage, Is.EqualTo("swish + critical"));
        Assert.That(stats.CriticalRolled, Is.EqualTo(1), "the critical roll must land on the GameStats instance passed in, not a stray global");
    }

    /// <summary>
    /// luck = 0 never rolls critical, and slider >= 95 skips the accuracy-modifier draw entirely -
    /// only the release roll remains. release = 100 always shoots clean
    /// (<see cref="ShotModifiers.ReleaseModifier"/> returns 0 when rolledClean), so this is also
    /// deterministic without seeding: X and Y both land on 0, and an in-range shot keeps Z at 0 too.
    /// </summary>
    [Test]
    public void HighSliderWithCertainCleanReleaseAndInRangeShotIsAlsoASwish()
    {
        CharacterProfile profile = MakeProfile(luck: 0, range: 10000, release: 100, shootAngle: 45);
        BasketBallState state = MakeState(twoPoints: true);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            profile,
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 95f);

        Assert.That(result.Critical, Is.False);
        Assert.That(result.IsSwish, Is.True);
        Assert.That(result.ShotMeterMessage, Is.EqualTo("swish"));
        Assert.That(stats.CriticalRolled, Is.EqualTo(0));
    }

    /// <summary>
    /// luck = 0 and release = 0 (never clean) with a slider under 95 forces every modifier branch to
    /// actually run. Not asserting exact floats here - that arithmetic is Level5ShotModifierTests'
    /// job - just that the pipeline reaches the "&lt; 95" branch, stops calling it a swish, and still
    /// produces a launch velocity with a forward (Z) component.
    /// </summary>
    [Test]
    public void LowSliderWithNoCleanRollsProducesANonSwishLaunchWithForwardVelocity()
    {
        CharacterProfile profile = MakeProfile(luck: 0, range: 10000, release: 0, shootAngle: 45);
        BasketBallState state = MakeState(twoPoints: true);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            profile,
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 10f);

        Assert.That(result.Critical, Is.False);
        Assert.That(result.ShotMeterMessage, Does.Contain("< 95"));
        Assert.That(result.ShotMeterMessage, Does.Contain("+ release modifier"));
        Assert.That(result.GlobalVelocity.z, Is.GreaterThan(0f));
    }

    [Test]
    public void UpdateScoreTextReportsCountsAndPercentagesFromTheSameGameStatsInstance()
    {
        GameStats stats = MakeStats();
        stats.ShotMade = 3;
        stats.ShotAttempt = 4;
        stats.TwoPointerMade = 2;
        stats.TwoPointerAttempts = 2;
        stats.ThreePointerMade = 1;
        stats.ThreePointerAttempts = 2;
        Text scoreText = Spawn("score-text").AddComponent<UnityEngine.UI.Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BasketballShotPipeline.UpdateScoreText(scoreText, stats, lastShotDistance: 5f);

        Assert.That(scoreText.text, Does.Contain("shots  : 3 / 4"));
        Assert.That(scoreText.text, Does.Contain("2 pointers : 2 / 2  100.00%"));
        Assert.That(scoreText.text, Does.Contain("3 pointers : 1 / 2  50.00%"));
    }
}
