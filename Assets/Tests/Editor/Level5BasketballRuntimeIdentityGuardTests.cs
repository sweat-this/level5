using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-013 permanent guards: the basketball runtime/scoring path must not read participant identity
/// from a ball-side <c>PlayerIdentifier</c>, and neither production basketball prefab may carry one.
///
/// Narrower than <see cref="Level5PlayerBasketballEdgeTests"/>, which forbids the whole
/// <c>Assets/Scripts/basketball</c> folder from reaching a concrete player type. This guard is scoped
/// to exactly the five files AUD-013 migrated onto <c>IBasketballRuntime</c> - it deliberately does not
/// extend to <c>ShotMeter</c>/<c>RangeMeter</c>, which are actor-owned and read the authoritative
/// actor-side <c>PlayerIdentifier</c> legitimately (see <c>PlayerIdentifier.Actor</c>'s own doc comment).
/// </summary>
public class Level5BasketballRuntimeIdentityGuardTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    private static readonly string[] RuntimeIdentityFiles =
    {
        "BasketBall.cs",
        "BasketBallAuto.cs",
        "BasketballState.cs",
        "BasketBallShotMade.cs",
        "BasketBallShotMadeCollision.cs",
    };

    /// <summary>The GUID of PlayerIdentifier.cs's own .meta file - the removed component's identity in prefab YAML.</summary>
    private const string PlayerIdentifierScriptGuid = "3f595a79238fe4f439aeac86720f9e96";

    private static readonly string[] BasketballPrefabPaths =
    {
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "Prefabs", "basketball", "basketball.prefab"),
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources", "Prefabs", "basketball", "basketballAuto.prefab"),
    };

    [Test]
    public void RuntimeAndScoringFilesDoNotReferencePlayerIdentifier()
    {
        List<string> offenders = new List<string>();

        foreach (string fileName in RuntimeIdentityFiles)
        {
            string path = Path.Combine(BasketballRoot, fileName);
            string text = Level5TestSourceText.StripComments(File.ReadAllText(path));
            if (Regex.IsMatch(text, @"\bPlayerIdentifier\b"))
            {
                offenders.Add(fileName);
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-013: these files must resolve basketball ownership/identity through IBasketballRuntime, "
            + "bound explicitly by SpawnCoordinator.GiveBall, not a ball-side PlayerIdentifier:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void ProductionBasketballPrefabsHaveNoPlayerIdentifierComponent()
    {
        List<string> offenders = new List<string>();

        foreach (string path in BasketballPrefabPaths)
        {
            string text = File.ReadAllText(path);
            if (Regex.IsMatch(text, $@"guid: {PlayerIdentifierScriptGuid}"))
            {
                offenders.Add(Level5TestSourceText.Relative(path));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-013: the basketball prefabs must not carry a PlayerIdentifier component - ownership is "
            + "bound explicitly at spawn time through IBasketballRuntime instead:\n"
            + string.Join("\n", offenders));
    }
}
