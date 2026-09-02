using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 1c permanent guard: production <c>Assets/Scripts/basketball</c> must never reference
/// <c>GameLevelManager</c> directly again.
///
/// <c>BasketBall.cs</c>'s no-active-Terrain drop-shadow fallback was the last live direct reference -
/// it read <c>GameLevelManager.instance.TerrainHeight</c>. It now receives a live
/// <c>Level5.Core.IGroundHeightProvider</c> through explicit composition
/// (<c>SpawnCoordinator.GiveBall</c> -&gt; <c>BasketBall.BindGroundHeightProvider</c>), which
/// <c>GameLevelManager</c> implements over its existing <c>TerrainHeight</c> value. This is a hard
/// zero, unlike <see cref="Level5GameManagerEdgeTests"/>'s shrinking allowlist for the opposite
/// direction (game manager -&gt; player/basketball): there is no remaining legitimate reason for this
/// folder to spell <c>GameLevelManager</c>.
///
/// This guard is scoped to the <c>GameLevelManager</c> dependency only. Basketball's dependency on
/// <c>GameRules</c> (a separate game-manager type - see <c>BasketBallShotMarker</c>/
/// <c>BasketballState</c>/<c>BasketBallShotMade</c>) is unresolved and intentionally out of scope
/// here; do not read this guard's pass as evidence that the broader
/// <c>basketball -&gt; game manager</c> edge is zero.
/// </summary>
public class Level5BasketballGameManagerEdgeTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    [Test]
    public void NoBasketballFileReferencesGameLevelManager()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateBasketballScripts())
        {
            string text = Level5TestSourceText.StripComments(File.ReadAllText(file));
            if (Regex.IsMatch(text, @"\bGameLevelManager\b"))
            {
                offenders.Add(Level5TestSourceText.Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 1c: production Assets/Scripts/basketball must not reference "
            + "GameLevelManager directly - a basketball-side need for scene/manager state should be "
            + "supplied through an explicit composition-time binding (e.g. IGroundHeightProvider), the "
            + "way BasketBall now receives its no-Terrain drop-shadow fallback. "
            + "GameLevelManager direct dependency = forbidden here; the separate, still-unresolved "
            + "basketball -> GameRules dependency is out of scope for this guard. Offending files:\n"
            + string.Join("\n", offenders));
    }

    private static IEnumerable<string> EnumerateBasketballScripts()
    {
        return Directory
            .EnumerateFiles(BasketballRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("~"));
    }
}
