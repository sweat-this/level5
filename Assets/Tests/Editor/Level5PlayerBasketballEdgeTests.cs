using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Guard rails for the player↔basketball cycle-cut slice (see the plan referenced from
/// docs/systems-restructure-plan.md's Phase 2b0 remeasurement, and docs/shot-lifecycle.md).
///
/// Unlike <see cref="Level5GameManagerEdgeTests"/>, which allowlists a shrinking set of accepted
/// back-references for the `game manager` edge, this guard asserts a hard zero for
/// `basketball -> concrete player implementation`. The migration this test locks in replaced every
/// basketball-side reference to <c>PlayerController</c>/<c>AutoPlayerController</c>/
/// <c>CharacterProfile</c> with <c>Level5.Core.IShooterActor</c>, reached through
/// <c>PlayerIdentifier.Actor</c> - so there is no remaining legitimate reason for
/// <c>Assets/Scripts/basketball</c> to spell any of the three again. <c>PlayerIdentifier</c> itself,
/// and the basketball module's own types reached through it (<c>BasketBall</c>, <c>BasketBallAuto</c>,
/// <c>BasketBallState</c>, <c>GameStats</c>), are deliberately not restricted - the same precedent
/// <see cref="Level5GameManagerEdgeTests"/> established: holding the roster's own identity type, or
/// reaching a sibling basketball type through it, is not the cycle.
/// </summary>
public class Level5PlayerBasketballEdgeTests
{
    private static readonly string BasketballRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball");

    private static readonly string[] RestrictedTypeNames =
    {
        "PlayerController",
        "AutoPlayerController",
        "CharacterProfile",
    };

    /// <summary>
    /// The reach-through shape a spelled-type search misses: a chain through a field that spells only
    /// <c>PlayerIdentifier</c> (e.g. <c>playerIdentifier.playerController.hasBasketball</c>) or a
    /// controller-typed local/parameter (e.g. <c>autoPlayerController.hasBasketball</c>) without ever
    /// spelling the type name on that line. Mirrors <see cref="Level5GameManagerEdgeTests"/>'s
    /// <c>ReachThroughChain</c> pattern for the same reason it exists there.
    /// </summary>
    private static readonly Regex ReachThroughChain = new Regex(
        @"\.playerController\.|\.autoPlayerController\.|\.characterProfile\.",
        RegexOptions.Compiled);

    [Test]
    public void NoBasketballFileReferencesConcretePlayerTypes()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateBasketballScripts())
        {
            string text = StripComments(File.ReadAllText(file));
            List<string> found = RestrictedTypeNames
                .Where(type => Regex.IsMatch(text, $@"\b{type}\b"))
                .ToList();

            if (found.Count > 0)
            {
                offenders.Add($"{Relative(file)}: {string.Join(", ", found)}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these basketball files reach for a concrete player type. The player↔basketball cycle-cut "
            + "slice removed every legitimate reason for this - basketball-side code should reach a "
            + "shooter through Level5.Core.IShooterActor via PlayerIdentifier.Actor instead:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void NoBasketballFileReachesThroughAControllerField()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateBasketballScripts())
        {
            string text = StripComments(File.ReadAllText(file));
            if (ReachThroughChain.IsMatch(text))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these basketball files reach through a .playerController./.autoPlayerController./"
            + ".characterProfile. chain into a concrete player type without spelling its name - the "
            + "same coupling the spelled-type test checks, in the shape it actually takes most often:\n"
            + string.Join("\n", offenders));
    }

    private static IEnumerable<string> EnumerateBasketballScripts()
    {
        return Directory
            .EnumerateFiles(BasketballRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("~"));
    }

    private static string Relative(string path) => Level5TestSourceText.Relative(path);

    private static string StripComments(string text) => Level5TestSourceText.StripComments(text);
}
