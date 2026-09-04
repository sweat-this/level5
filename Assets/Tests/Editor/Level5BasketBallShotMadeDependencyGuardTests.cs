using System.IO;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0 permanent guard: <c>BasketBallShotMade</c> must carry no <c>MatchRuntime</c>
/// dependency. <c>shotMade</c> used to read <c>MatchRuntime.Rules</c> and <c>MatchRuntime.RawModeId</c>
/// directly; both now arrive as explicit <c>ResolvedMatchRules</c>/<c>GameModeId</c> bound once through
/// <c>BindMatchContext</c>, supplied by <c>GameLevelManager.Awake</c>. This fails the build if a future
/// change reintroduces a direct <c>MatchRuntime</c> read on this type instead of using the bound
/// context. Mirrors <see cref="Level5BasketballShotPipelineDependencyGuardTests"/>.
/// </summary>
public class Level5BasketBallShotMadeDependencyGuardTests
{
    private static readonly string BasketBallShotMadePath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "BasketBallShotMade.cs");

    [Test]
    public void BasketBallShotMadeHasNoMatchRuntimeReference()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(BasketBallShotMadePath));

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "BasketBallShotMade must have zero MatchRuntime references - match rules and mode identity "
            + "must arrive through BindMatchContext(ResolvedMatchRules, GameModeId), bound once at "
            + "match composition, not by reading MatchRuntime directly.");
    }
}
