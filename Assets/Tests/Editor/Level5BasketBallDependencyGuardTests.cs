using System.IO;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0 permanent guard: <c>BasketBall</c> must carry no <c>MatchRuntime</c>
/// dependency. <c>Start()</c>/<c>Update()</c> used to read <c>MatchRuntime.Rules.EnemiesOnly</c> and
/// <c>MatchRuntime.Rules.IsBattleRoyal</c> directly; match rules now arrive once through
/// <c>BindMatchRules(ResolvedMatchRules)</c>, bound by composition (<c>SpawnCoordinator.GiveBall</c>).
/// This fails the build if a future change reintroduces a direct <c>MatchRuntime</c> read on this
/// type instead of using the bound reference. Mirrors <see cref="Level5BasketBallAutoDependencyGuardTests"/>
/// and <see cref="Level5BasketBallStateDependencyGuardTests"/>.
/// </summary>
public class Level5BasketBallDependencyGuardTests
{
    private static readonly string BasketBallPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "BasketBall.cs");

    [Test]
    public void BasketBallHasNoMatchRuntimeReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(BasketBallPath));

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "BasketBall must have zero MatchRuntime references - match rules must arrive through "
            + "BindMatchRules(ResolvedMatchRules), bound once at match composition, not by reading "
            + "MatchRuntime directly.");
    }
}
