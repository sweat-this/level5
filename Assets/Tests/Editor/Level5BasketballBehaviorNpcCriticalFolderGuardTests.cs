using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 2b0: <see cref="BasketBall.Launch"/> and <see cref="BasketBallAuto.Launch"/> no
/// longer call <c>BehaviorNpcCritical.instance.playAnimationCriticalSuccesful()</c> directly - each
/// invokes a bound <c>Action</c> callback (<c>BindCriticalSuccessPresentation</c>), composed by
/// <c>SpawnCoordinator.GiveBall</c> for both human and CPU basketballs via a shared late-resolving
/// <c>SpawnCoordinator.PlayCriticalSuccessPresentation</c> adapter. This closes the production
/// basketball -&gt; <c>BehaviorNpcCritical</c> edge; <c>BehaviorNpcCritical</c> itself, its animation
/// event wiring, and every non-basketball consumer (<c>EnemyController</c>) are all unchanged - only
/// the dependency direction is inverted (composition -&gt; callback -&gt; basketball, instead of
/// basketball -&gt; <c>BehaviorNpcCritical</c>).
///
/// This guard fails the build if a future change reintroduces a live <c>BehaviorNpcCritical</c>
/// reference anywhere in production basketball, mirroring the existing folder-wide
/// <c>MatchRuntime</c>/<c>AnaylticsManager</c> guards (<see cref="Level5BasketballMatchRuntimeFolderGuardTests"/>,
/// <see cref="Level5BasketballAnaylticsManagerFolderGuardTests"/>).
/// </summary>
public class Level5BasketballBehaviorNpcCriticalFolderGuardTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    [Test]
    public void ProductionBasketballHasZeroLiveBehaviorNpcCriticalReferences()
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
            if (Regex.IsMatch(text, @"\bBehaviorNpcCritical\b"))
            {
                offenders.Add(Level5TestSourceText.Relative(path));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 2b0: production basketball must have zero live BehaviorNpcCritical "
            + "references - swish/critical-success presentation must arrive through a bound Action "
            + "callback (BindCriticalSuccessPresentation), composed by SpawnCoordinator.GiveBall. "
            + "Found some in:\n" + string.Join("\n", offenders));
    }
}
