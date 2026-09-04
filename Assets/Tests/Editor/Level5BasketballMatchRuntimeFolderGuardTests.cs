using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0 permanent guard: production basketball must have zero live <c>MatchRuntime</c>
/// references anywhere in the folder. <c>BasketBall</c>, <c>BasketBallAuto</c>, <c>BasketballState</c>,
/// <c>BasketballShotPipeline</c> and <c>BasketBallShotMade</c> were migrated onto bind-once
/// <see cref="Level5.Core.Match.ResolvedMatchRules"/>/<c>GameModeId</c> references across earlier
/// Phase 2b0 slices, leaving <c>BasketBallShotMarker</c> as the one remaining production file with a
/// live <c>MatchRuntime.Rules</c> read (<c>Start()</c>'s marker-required check,
/// <c>IsPointContestMode()</c>). This slice closes it: both now read a
/// <see cref="Level5.Core.Match.ResolvedMatchRules"/> bound once by
/// <c>GameRules.BindShotMarkerSessionToMarkers</c> (<see cref="BasketBallShotMarker.BindMatchRules"/>),
/// the same bind/rebind/null-guard shape the rest of this migration already established.
///
/// With that closed, production basketball reaches zero live <c>MatchRuntime</c> references anywhere
/// in the folder - this is the strongest accurate form of the guard, replacing the need for further
/// per-file <c>MatchRuntime</c> guards in this folder (the existing per-file guards for
/// <c>BasketBall</c>, <c>BasketBallAuto</c>, <c>BasketballState</c>, <c>BasketballShotPipeline</c> and
/// <c>BasketBallShotMade</c> are kept unchanged; this test is additive, not a replacement for them).
/// <c>MatchRuntime</c> itself, and its legacy-globals fallback for a directly-entered scene, are
/// untouched - this only forbids production basketball from reading it directly.
/// </summary>
public class Level5BasketballMatchRuntimeFolderGuardTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    [Test]
    public void ProductionBasketballHasZeroLiveMatchRuntimeReferences()
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
            if (Regex.IsMatch(text, @"\bMatchRuntime\b"))
            {
                offenders.Add(Level5TestSourceText.Relative(path));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 2b0: production basketball must have zero live MatchRuntime references - "
            + "match rules must arrive through a bound ResolvedMatchRules reference (BindMatchRules/"
            + "BindMatchContext), not by reading MatchRuntime directly. Found some in:\n"
            + string.Join("\n", offenders));
    }
}
