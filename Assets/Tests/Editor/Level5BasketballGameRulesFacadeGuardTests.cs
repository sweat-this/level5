using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 1c permanent guard: basketball production code must read immutable match-rule
/// values through <c>MatchRuntime.Rules</c> (a <see cref="Level5.Core.Match.ResolvedMatchRules"/>
/// snapshot), not through the <c>GameRules</c> compatibility facade properties that merely forward
/// them. Those properties (<c>GameRules.GameModeRequiresMoneyBall</c>,
/// <c>GameModeThreePointContest</c>, <c>GameModeFourPointContest</c>, <c>GameModeSevenPointContest</c>,
/// <c>GameModeAllPointContest</c>, <c>PositionMarkersRequired</c>) stay on <c>GameRules</c> for other
/// callers - this only forbids the basketball folder from reaching for them again.
///
/// <c>InThePocketActivateValue</c> was the first live dependency removed, from
/// <c>BasketBallShotMade</c> (AUD-010 Phase 1c) - it was always 0 in production, so that file now uses
/// an explicit constant instead. <c>MoneyBallEnabled</c> was the second, from
/// <c>BasketballShotPipeline</c> - it stays live, mutable session state, still owned by
/// <c>GameRules</c>, but basketball now reaches it through a bound
/// <see cref="Level5.Core.Match.IMoneyBallState"/> instead of the singleton.
/// <c>MarkersRemaining</c>, <c>IsGameOver()</c> and <c>RequestGameOver()</c> were the last three, from
/// <c>BasketBallShotMarker</c> - still live, mutable session/lifecycle state, still owned by
/// <c>GameRules</c>, but basketball now reaches them through a bound
/// <see cref="Level5.Core.Match.IShotMarkerSession"/> instead of the singleton. With all five closed,
/// <see cref="ProductionBasketballHasZeroLiveGameRulesReferences"/> pins the whole folder at zero live
/// references, replacing the earlier per-file allowlist guards.
/// </summary>
public class Level5BasketballGameRulesFacadeGuardTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    private static readonly string[] ForbiddenFacadeMembers =
    {
        "GameModeRequiresMoneyBall",
        "GameModeThreePointContest",
        "GameModeFourPointContest",
        "GameModeSevenPointContest",
        "GameModeAllPointContest",
        "PositionMarkersRequired",
    };

    [Test]
    public void BasketballProductionCodeDoesNotReadTheMigratedGameRulesFacadeProperties()
    {
        List<string> offenders = new List<string>();

        foreach (string path in Directory.EnumerateFiles(BasketballRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("Legacy~"))
            {
                continue;
            }

            string text = Level5TestSourceText.StripComments(File.ReadAllText(path));

            foreach (string member in ForbiddenFacadeMembers)
            {
                if (Regex.IsMatch(text, $@"\b{member}\b"))
                {
                    offenders.Add($"{Level5TestSourceText.Relative(path)}: {member}");
                }
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 1c: basketball production code must read these immutable rule values "
            + "through MatchRuntime.Rules (ResolvedMatchRules), not the GameRules compatibility "
            + "facade - found:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// AUD-010 Phase 1c: with <c>BasketBallShotMarker</c>'s last live dependency
    /// (<c>MarkersRemaining</c>, <c>IsGameOver()</c>, <c>RequestGameOver()</c>) replaced by a bound
    /// <see cref="Level5.Core.Match.IShotMarkerSession"/>, production basketball has zero live
    /// <c>GameRules</c> references anywhere in the folder. This is the final, strongest form of the
    /// earlier one-file allowlist guards above: every file in the folder is pinned at zero, so any
    /// future reach for <c>GameRules</c> is caught immediately rather than silently allowed back in.
    /// </summary>
    [Test]
    public void ProductionBasketballHasZeroLiveGameRulesReferences()
    {
        List<string> offenders = new List<string>();

        foreach (string path in Directory.EnumerateFiles(BasketballRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("Legacy~"))
            {
                continue;
            }

            string text = Level5TestSourceText.StripComments(File.ReadAllText(path));
            if (Regex.IsMatch(text, @"\bGameRules\b"))
            {
                offenders.Add(Level5TestSourceText.Relative(path));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 1c: production basketball must have zero live GameRules references - "
            + "found some in:\n" + string.Join("\n", offenders));
    }
}
