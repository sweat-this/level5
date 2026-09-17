using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The racing minigame subsystem (<c>player_racing</c>/<c>Level5.PlayerRacing</c>,
/// <c>minigame_racing.unity</c>, <c>RacingInputReader</c>, and the racing-only prefab folder) was
/// retired and deleted, not migrated. This guard fails the build if any of those concrete retired
/// paths, or the retired type names as live code (not historical comments - see
/// <see cref="Level5TestSourceText.StripComments"/>), silently reappear.
///
/// Deliberately scoped to specific retired paths/identifiers rather than banning the words "race"/
/// "racing" repository-wide - those words remain legitimate in generic competitive concepts, dated
/// audit documents, and historical explanatory comments (e.g. the AUD-079/081 "same unguarded-lookup
/// shape" notes still in PlayerController.cs/AutoPlayerController.cs/AutoPlayerDefense.cs).
/// </summary>
public class Level5RacingRetirementGuardTests
{
    private static readonly string ProjectRoot = Directory.GetCurrentDirectory();
    private static readonly string ScriptsRoot = Path.Combine(ProjectRoot, "Assets", "Scripts");
    private static readonly string TestsRoot = Path.Combine(ProjectRoot, "Assets", "Tests");

    private static readonly string[] RetiredPaths =
    {
        Path.Combine("Assets", "Scripts", "player_racing"),
        Path.Combine("Assets", "Scripts", "input", "RacingInputReader.cs"),
        Path.Combine("Assets", "Scenes", "minigame_racing.unity"),
        Path.Combine("Assets", "Resources", "Prefabs", "racing"),
        Path.Combine("Assets", "Terrain", "standard_terrain_racing_sand.asset"),
    };

    private static readonly string Level5PlayerRacingAsmdef =
        Path.Combine("Assets", "Scripts", "player_racing", "Level5PlayerRacing", "Level5.PlayerRacing.asmdef");

    private static readonly string[] RetiredTypeNames =
    {
        "RacingGameManager",
        "RacingVehicleController",
        "RacingVehicleProfile",
        "RacingVehicleCollisions",
        "RacingGroundCheck",
        "RacingAnimationEvents",
        "RacingCinderBlock",
        "RacingInputReader",
    };

    [Test]
    public void RetiredRacingPathsDoNotExist()
    {
        List<string> stillPresent = new List<string>();
        foreach (string relativePath in RetiredPaths)
        {
            string fullPath = Path.Combine(ProjectRoot, relativePath);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                stillPresent.Add(relativePath.Replace('\\', '/'));
            }
        }

        Assert.That(
            stillPresent,
            Is.Empty,
            "the racing minigame was retired and deleted, not migrated - these retired paths must not "
                + "be recreated:\n" + string.Join("\n", stillPresent));
    }

    [Test]
    public void Level5PlayerRacingAssemblyDoesNotExist()
    {
        string fullPath = Path.Combine(ProjectRoot, Level5PlayerRacingAsmdef);
        Assert.That(
            File.Exists(fullPath),
            Is.False,
            "Level5.PlayerRacing was the retired racing production assembly and must not be recreated.");
    }

    [Test]
    public void NoLiveCodeReferencesRetiredRacingTypes()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateSourceFiles())
        {
            // StripCommentsAndLiterals (not just StripComments): this test's own RetiredTypeNames
            // array is a list of string literals naming exactly these types, which a comment-only
            // strip would still see as "live code" and permanently fail on itself.
            string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(file));
            foreach (string typeName in RetiredTypeNames)
            {
                if (Regex.IsMatch(text, $@"\b{typeName}\b"))
                {
                    offenders.Add($"{Level5TestSourceText.Relative(file)}: {typeName}");
                }
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these retired racing types must not reappear as live code (historical comments are exempt "
                + "- this scan strips comments first):\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> EnumerateSourceFiles()
    {
        return Directory.EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(TestsRoot, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains("~"));
    }
}
