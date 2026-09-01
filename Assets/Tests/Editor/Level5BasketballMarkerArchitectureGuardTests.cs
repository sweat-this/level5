using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 1c permanent guards: the marker-launch and made-shot paths must resolve a
/// participant's marker through the direct <see cref="BasketBallState.CurrentShotMarker"/> /
/// <see cref="BasketBallState.OnShootShotMarker"/> references, never by indexing
/// <c>GameRules.instance.BasketBallShotMarkersList</c> by an id. Final-attempt completion must wait
/// on the runtime that took the final attempt, never <c>GameLevelManager.instance.players[0]</c>.
///
/// Narrower than <see cref="Level5BasketballRuntimeIdentityGuardTests"/>, which guards a different
/// coupling (ball-side <c>PlayerIdentifier</c>) on a different file set - <c>BasketBallShotMarker.cs</c>
/// legitimately still reaches the actor-side <c>PlayerIdentifier</c> and is not part of that guard.
/// </summary>
public class Level5BasketballMarkerArchitectureGuardTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    private static readonly string[] MarkerIdResolutionFiles =
    {
        "BasketballState.cs",
        "BasketballShotPipeline.cs",
        "BasketBallShotMade.cs",
    };

    private static readonly Regex IndexedMarkerListAccess = new Regex(
        @"BasketBallShotMarkersList\s*\[", RegexOptions.Compiled);

    [Test]
    public void MarkerLaunchAndMadeShotPathsDoNotIndexTheGlobalMarkerListById()
    {
        List<string> offenders = new List<string>();

        foreach (string fileName in MarkerIdResolutionFiles)
        {
            string path = Path.Combine(BasketballRoot, fileName);
            string text = Level5TestSourceText.StripComments(File.ReadAllText(path));
            if (IndexedMarkerListAccess.IsMatch(text))
            {
                offenders.Add(fileName);
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 1c: these files must resolve a participant's marker through "
            + "BasketBallState.CurrentShotMarker/OnShootShotMarker, not by indexing "
            + "GameRules.BasketBallShotMarkersList by an id:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void BasketballStateNoLongerExposesWritableMarkerIds()
    {
        string path = Path.Combine(BasketballRoot, "BasketballState.cs");
        string text = Level5TestSourceText.StripComments(File.ReadAllText(path));

        Assert.That(
            text,
            Does.Not.Match(@"\bCurrentShotMarkerId\b"),
            "BasketBallState.CurrentShotMarkerId must not come back - marker occupancy is the "
            + "CurrentShotMarker reference, not an id.");
        Assert.That(
            text,
            Does.Not.Match(@"\bOnShootShotMarkerId\b"),
            "BasketBallState.OnShootShotMarkerId must not come back - the launch snapshot is the "
            + "OnShootShotMarker reference, not an id.");
    }

    [Test]
    public void ShotMarkerFinalAttemptCompletionDoesNotReadPlayerZero()
    {
        string path = Path.Combine(BasketballRoot, "BasketBallShotMarker.cs");
        string text = Level5TestSourceText.StripComments(File.ReadAllText(path));

        Assert.That(
            text,
            Does.Not.Match(@"players\s*\[\s*0\s*\]"),
            "BasketBallShotMarker must decide final-attempt readiness from the runtime that took the "
            + "final attempt (finalAttemptRuntime), not GameLevelManager.instance.players[0].");
    }
}
