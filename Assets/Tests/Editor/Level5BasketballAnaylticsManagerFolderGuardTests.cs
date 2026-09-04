using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0: <see cref="BasketBall.Launch"/> no longer calls <c>AnaylticsManager.PlayerShoot</c>
/// directly - it invokes a bound <c>Action&lt;float&gt;</c> callback
/// (<see cref="BasketBall.BindShotTelemetry"/>), composed by <c>SpawnCoordinator.GiveBall</c> for
/// human basketballs only (<c>AnaylticsManager.PlayerShoot</c>). This closes the production
/// basketball -&gt; <c>AnaylticsManager</c> edge; the analytics call itself, its event name, fields,
/// and <c>MatchRuntime</c>-based attribution are all unchanged - only the dependency direction is
/// inverted (composition -&gt; callback -&gt; <c>BasketBall</c>, instead of <c>BasketBall</c> -&gt;
/// <c>AnaylticsManager</c>).
///
/// This guard fails the build if a future change reintroduces a live <c>AnaylticsManager</c>
/// reference anywhere in production basketball, mirroring the existing folder-wide
/// <c>MatchRuntime</c> guard (<see cref="Level5BasketballMatchRuntimeFolderGuardTests"/>).
/// </summary>
public class Level5BasketballAnaylticsManagerFolderGuardTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    [Test]
    public void ProductionBasketballHasZeroLiveAnaylticsManagerReferences()
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
            if (Regex.IsMatch(text, @"\bAnaylticsManager\b"))
            {
                offenders.Add(Level5TestSourceText.Relative(path));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 2b0: production basketball must have zero live AnaylticsManager references - "
            + "human shot telemetry must arrive through a bound Action<float> callback "
            + "(BasketBall.BindShotTelemetry), composed by SpawnCoordinator.GiveBall. Found some in:\n"
            + string.Join("\n", offenders));
    }
}
