using System.Text.RegularExpressions;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: <see cref="GameStats.BuildExperienceInput"/> no longer reads
/// <c>MatchRuntime</c> directly - match composition binds the scene's already-resolved
/// <see cref="ResolvedMatchRules"/> once, through <see cref="GameStats.BindMatchRules"/>, and match-XP
/// calculation reads that bound reference instead.
///
/// These tests cover the bind-once contract itself and the exact rules -> <see cref="MatchExperienceInput"/>
/// mapping the migration had to preserve byte-for-byte. Composition coverage (which real production
/// basketballs get bound, human and CPU) lives in <c>Level5BasketballOwnershipBindingTests</c>.
/// </summary>
public class Level5GameStatsMatchRulesBindingTests
{
    private GameObject host;
    private GameStats stats;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("gamestats-rules-binding");
        stats = host.AddComponent<GameStats>();
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void NullBindIsRejectedAndLeavesTheComponentUnbound()
    {
        LogAssert.Expect(LogType.Error, new Regex("null rules"));
        stats.BindMatchRules(null);

        Assert.IsFalse(stats.HasBoundMatchRules);
    }

    [Test]
    public void FirstValidBindIsRetained()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(hardcore: true);

        stats.BindMatchRules(rules);

        Assert.IsTrue(stats.HasBoundMatchRules);
        Assert.That(stats.BuildExperienceInput().HardcoreEnabled, Is.True);
    }

    [Test]
    public void SecondBindAttemptIsRejectedAndTheOriginalRemainsAuthoritative()
    {
        ResolvedMatchRules first = new ResolvedMatchRules(hardcore: true);
        ResolvedMatchRules second = new ResolvedMatchRules(hardcore: false);
        stats.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already called"));
        stats.BindMatchRules(second);

        // The second bind's distinguishing value (hardcore: false) must not have taken effect.
        Assert.That(stats.BuildExperienceInput().HardcoreEnabled, Is.True,
            "a rejected second bind must not replace the original rules");
    }

    [Test]
    public void UnboundGameStatsRemainsAUsableFacade()
    {
        // Campaign, all-time-stats and versus-report GameStats never call BindMatchRules. Ordinary
        // stats reads/writes must keep working without it.
        stats.ShotAttempt = 4;
        stats.ShotMade = 3;
        stats.TotalPoints = 21;

        Assert.That(stats.getTotalPointAccuracy(), Is.EqualTo(75f).Within(0.0001f));
        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(21));
        Assert.IsFalse(stats.HasBoundMatchRules);
    }

    [Test]
    public void GetExperienceGainedFromSessionWithNoBoundRulesFailsClosed()
    {
        stats.ShotAttempt = 10;
        stats.TotalPoints = 100;

        LogAssert.Expect(LogType.Error, new Regex("no match rules bound"));
        int experience = stats.getExperienceGainedFromSession();

        Assert.That(experience, Is.EqualTo(0));
        Assert.That(stats.ExperienceGained, Is.EqualTo(0));
    }

    [Test]
    public void BuildExperienceInputWithNoBoundRulesFailsClosedWithAnInertInput()
    {
        LogAssert.Expect(LogType.Error, new Regex("no match rules bound"));
        MatchExperienceInput input = stats.BuildExperienceInput();

        Assert.That(input, Is.EqualTo(default(MatchExperienceInput)));
    }

    [Test]
    public void ExperienceInputMapsEveryRuleFieldExactly()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(
            trafficEnabled: true,
            enemiesEnabled: false,
            enemiesOnly: true,
            hardcore: true,
            sniper: SniperMode.Laser,
            arcadeMode: true,
            difficulty: MatchDifficulty.Hardcore);
        stats.BindMatchRules(rules);

        MatchExperienceInput input = stats.BuildExperienceInput();

        Assert.IsTrue(input.TrafficEnabled);
        // EnemiesEnabled is false but EnemiesOnly is true - the existing OR must still report enabled.
        Assert.IsTrue(input.EnemiesEnabled);
        Assert.IsTrue(input.HardcoreEnabled);
        Assert.IsTrue(input.SniperEnabled);
        Assert.IsTrue(input.ArcadeMode);
        Assert.That(input.DifficultySelected, Is.EqualTo(MatchDifficulties.ToInt(MatchDifficulty.Hardcore)));
    }

    [Test]
    public void ExperienceInputReportsDisabledModifiersExactly()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(
            trafficEnabled: false,
            enemiesEnabled: false,
            enemiesOnly: false,
            hardcore: false,
            sniper: SniperMode.None,
            arcadeMode: false,
            difficulty: MatchDifficulty.Easy);
        stats.BindMatchRules(rules);

        MatchExperienceInput input = stats.BuildExperienceInput();

        Assert.IsFalse(input.TrafficEnabled);
        Assert.IsFalse(input.EnemiesEnabled);
        Assert.IsFalse(input.HardcoreEnabled);
        Assert.IsFalse(input.SniperEnabled);
        Assert.IsFalse(input.ArcadeMode);
        Assert.That(input.DifficultySelected, Is.EqualTo(MatchDifficulties.ToInt(MatchDifficulty.Easy)));
    }

    [Test]
    public void GetExperienceGainedFromSessionMatchesMatchExperienceCalculateForTheSameInput()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(
            trafficEnabled: true,
            enemiesEnabled: true,
            hardcore: false,
            sniper: SniperMode.Bullet,
            arcadeMode: false,
            difficulty: MatchDifficulty.Normal);
        stats.BindMatchRules(rules);

        stats.ShotAttempt = 12;
        stats.TwoPointerMade = 3;
        stats.ThreePointerMade = 2;
        stats.FourPointerMade = 1;
        stats.SevenPointerMade = 1;
        stats.TotalDistance = 42f;
        stats.MostConsecutiveShots = 4;
        stats.TotalPoints = 37;
        stats.SniperShots = 5;
        stats.SniperHits = 2;
        stats.MinionsKilled = 3;
        stats.BossKilled = 1;

        int expected = MatchExperience.Calculate(stats.BuildExperienceInput());
        int actual = stats.getExperienceGainedFromSession();

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(stats.ExperienceGained, Is.EqualTo(expected));
    }
}
