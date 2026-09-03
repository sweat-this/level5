using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 1c permanent guard: <c>GameStats</c> must carry no <c>BasketBallState</c>
/// dependency. The two methods that once read it - <c>ApplyMadeShot(BasketBallState, ...)</c> and
/// <c>calculateConsecutiveShot(BasketBallState)</c> - were temporary migration seams, not permanent
/// contracts; this fails the build if either, or any other <c>BasketBallState</c> reference, comes
/// back.
/// </summary>
public class Level5GameStatsDependencyGuardTests
{
    private static readonly string GameStatsPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "GameStats.cs");

    private static readonly Regex PublicMethodTakingBasketBallState = new Regex(
        @"public\s+[\w<>\[\],\s]+\s+\w+\s*\([^)]*\bBasketBallState\b", RegexOptions.Compiled);

    [Test]
    public void GameStatsHasNoBasketBallStateReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(GameStatsPath));

        Assert.That(
            text,
            Does.Not.Match(@"\bBasketBallState\b"),
            "GameStats must have zero BasketBallState references - that dependency was cut in AUD-010 "
            + "Phase 1c. The live made-shot path now calls Stats.ApplyMadeShot(bool, ShotScoringInput) "
            + "directly, with the caller (BasketBallShotMade) capturing BasketBallState.TwoAttempt itself.");
    }

    [Test]
    public void GameStatsExposesNoPublicMethodTakingBasketBallState()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(GameStatsPath));

        Assert.That(
            PublicMethodTakingBasketBallState.IsMatch(text),
            Is.False,
            "GameStats must not expose a public method whose parameter type is BasketBallState.");
    }

    /// <summary>
    /// AUD-010 Phase 2b0 permanent guard: <c>GameStats</c> must carry no <c>MatchRuntime</c>
    /// dependency. <c>BuildExperienceInput</c> used to read <c>MatchRuntime.Rules</c> directly; match
    /// rules now arrive once through <c>BindMatchRules(ResolvedMatchRules)</c>, bound by composition
    /// (<c>SpawnCoordinator.GiveBall</c>). This fails the build if a future change reintroduces a
    /// direct <c>MatchRuntime</c> read on this type instead of using the bound reference.
    /// </summary>
    [Test]
    public void GameStatsHasNoMatchRuntimeReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(GameStatsPath));

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "GameStats must have zero MatchRuntime references - match rules must arrive through "
            + "BindMatchRules(ResolvedMatchRules), bound once at match composition, not by reading "
            + "MatchRuntime directly.");
    }
}
