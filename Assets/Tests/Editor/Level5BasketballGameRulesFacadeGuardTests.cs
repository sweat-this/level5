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
/// Deliberately does not forbid <c>GameRules</c> itself: <c>MoneyBallEnabled</c>,
/// <c>MarkersRemaining</c>, <c>IsGameOver()</c> and <c>RequestGameOver()</c> remain valid, unresolved
/// mutable session/lifecycle dependencies - see docs/shot-lifecycle.md.
/// <c>InThePocketActivateValue</c> was the last of these read by <c>BasketBallShotMade</c>
/// (AUD-010 Phase 1c) - it was always 0 in production, so that file now uses an explicit constant
/// instead and is banned from referencing <c>GameRules</c> at all, below.
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
    /// AUD-010 Phase 1c: <c>BasketBallShotMade</c>'s last live <c>GameRules</c> dependency
    /// (<c>InThePocketActivateValue</c>, always 0 in production) was replaced by an explicit
    /// constant - see <c>CurrentInThePocketStreakBonusThreshold</c> and docs/shot-lifecycle.md.
    /// This pins that file at zero live GameRules references so the dependency cannot silently
    /// come back.
    /// </summary>
    [Test]
    public void BasketBallShotMadeHasNoLiveGameRulesReferences()
    {
        string path = Path.Combine(BasketballRoot, "BasketBallShotMade.cs");
        string text = Level5TestSourceText.StripComments(File.ReadAllText(path));

        Assert.That(
            Regex.IsMatch(text, @"\bGameRules\b"),
            Is.False,
            "AUD-010 Phase 1c: BasketBallShotMade must have zero live GameRules references - "
            + "found one in " + Level5TestSourceText.Relative(path));
    }
}
