using System.IO;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0 permanent guard: <c>BasketBallState</c> must carry no <c>MatchRuntime</c>
/// dependency. <c>Update()</c> used to read <c>MatchRuntime.Rules.RequiresBasketball</c> directly;
/// match rules now arrive once through <c>BindMatchRules(ResolvedMatchRules)</c>, bound by composition
/// (<c>SpawnCoordinator.GiveBall</c>). This fails the build if a future change reintroduces a direct
/// <c>MatchRuntime</c> read on this type instead of using the bound reference.
/// </summary>
public class Level5BasketBallStateDependencyGuardTests
{
    private static readonly string BasketballStatePath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "BasketballState.cs");

    [Test]
    public void BasketBallStateHasNoMatchRuntimeReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(BasketballStatePath));

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "BasketBallState must have zero MatchRuntime references - match rules must arrive through "
            + "BindMatchRules(ResolvedMatchRules), bound once at match composition, not by reading "
            + "MatchRuntime directly.");
    }
}
