using System.IO;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0 permanent guard: <c>BasketBallAuto</c> must carry no <c>MatchRuntime</c>
/// dependency. <c>Start()</c>/<c>Update()</c> used to read <c>MatchRuntime.Rules.EnemiesOnly</c>
/// directly; match rules now arrive once through <c>BindMatchRules(ResolvedMatchRules)</c>, bound by
/// composition (<c>SpawnCoordinator.GiveBall</c>). This fails the build if a future change
/// reintroduces a direct <c>MatchRuntime</c> read on this type instead of using the bound reference.
/// </summary>
public class Level5BasketBallAutoDependencyGuardTests
{
    private static readonly string BasketBallAutoPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "BasketBallAuto.cs");

    [Test]
    public void BasketBallAutoHasNoMatchRuntimeReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(BasketBallAutoPath));

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "BasketBallAuto must have zero MatchRuntime references - match rules must arrive through "
            + "BindMatchRules(ResolvedMatchRules), bound once at match composition, not by reading "
            + "MatchRuntime directly.");
    }
}
