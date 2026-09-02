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
/// <c>GameModeAllPointContest</c>) stay on <c>GameRules</c> for other callers - this only forbids the
/// basketball folder from reaching for them again.
///
/// Deliberately does not forbid <c>GameRules</c> itself: <c>MoneyBallEnabled</c>,
/// <c>PositionMarkersRequired</c>, <c>MarkersRemaining</c>, <c>IsGameOver()</c>,
/// <c>RequestGameOver()</c> and <c>InThePocketActivateValue</c> remain valid, unresolved mutable
/// session/lifecycle dependencies - see docs/shot-lifecycle.md.
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
}
