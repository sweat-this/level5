using System.IO;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0 permanent guard: <c>BasketballShotPipeline</c> must carry no <c>MatchRuntime</c>
/// dependency. <c>ApplyMarkerAndMoneyBallOnShoot</c> used to read <c>MatchRuntime.Rules</c> directly;
/// match rules now arrive as an explicit <c>ResolvedMatchRules</c> parameter, supplied by each caller's
/// own bound reference (<c>BasketBall.matchRules</c>/<c>BasketBallAuto.matchRules</c>, each set once by
/// <c>SpawnCoordinator.GiveBall</c>). This fails the build if a future change reintroduces a direct
/// <c>MatchRuntime</c> read on this type instead of using the supplied parameter. Mirrors
/// <see cref="Level5BasketBallDependencyGuardTests"/> and <see cref="Level5BasketBallAutoDependencyGuardTests"/>.
/// </summary>
public class Level5BasketballShotPipelineDependencyGuardTests
{
    private static readonly string BasketballShotPipelinePath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "BasketballShotPipeline.cs");

    [Test]
    public void BasketballShotPipelineHasNoMatchRuntimeReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(BasketballShotPipelinePath));

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "BasketballShotPipeline must have zero MatchRuntime references - match rules must arrive "
            + "through ApplyMarkerAndMoneyBallOnShoot's explicit ResolvedMatchRules parameter, supplied "
            + "by each caller's own bound reference, not by reading MatchRuntime directly.");
    }
}
