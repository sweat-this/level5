using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Guard rails for the player-select architecture overhaul, in the same spirit as
/// <see cref="Level5MatchArchitectureTests"/>: these fail the build if the ownership pattern being
/// replaced (StartManager owning selected human/CPU indices, catalog-index selection state,
/// object-name-driven player/CPU rendering) reappears.
/// </summary>
public class Level5PlayerSelectArchitectureTests
{
    private static readonly string RepoRoot = Directory.GetCurrentDirectory();
    private static readonly string CoreDir = Path.Combine(RepoRoot, "Assets", "Level5", "Core", "PlayerSelection");
    private static readonly string AdapterDir = Path.Combine(RepoRoot, "Assets", "Scripts", "menu_start", "player_select");
    private static readonly string StartManagerPath = Path.Combine(RepoRoot, "Assets", "Scripts", "menu_start", "StartManager.cs");
    private static readonly string StartMenuSelectionStatePath = Path.Combine(RepoRoot, "Assets", "Scripts", "menu_start", "StartMenuSelectionState.cs");
    private static readonly string GameOptionsPath = Path.Combine(RepoRoot, "Assets", "Scripts", "menu_start", "GameOptions.cs");

    [Test]
    public void StartMenuSelectionStateNoLongerOwnsPlayerOrCpuSelection()
    {
        string text = StripComments(File.ReadAllText(StartMenuSelectionStatePath));
        string[] forbidden = { "CharacterProfile", "PlayerIndex", "Cpu1Index", "Cpu2Index", "Cpu3Index", "CyclePlayer", "CycleCpu(" };

        List<string> offenders = forbidden.Where(token => text.Contains(token)).ToList();

        Assert.That(offenders, Is.Empty, "StartMenuSelectionState.cs must not reference: " + string.Join(", ", offenders));
    }

    [Test]
    public void PlayerSelectionCoreHasNoUnityOrMenuDependencies()
    {
        string[] forbidden =
        {
            "UnityEngine", "GameOptions", "LoadedData", "StartManager", "SceneManager", "GameObject", "MonoBehaviour",
        };

        List<string> offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(CoreDir, "*.cs", SearchOption.AllDirectories))
        {
            string text = StripComments(File.ReadAllText(file));
            foreach (string token in forbidden)
            {
                if (text.Contains(token))
                {
                    offenders.Add($"{Relative(file)} references {token}");
                }
            }
        }

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    [Test]
    public void StartManagerNoLongerContainsRemovedPlayerSelectRenderOrCycleMethods()
    {
        string text = StripComments(File.ReadAllText(StartManagerPath));
        string[] removedMemberNames =
        {
            "initializePlayerDisplay", "initializeCpuPlayerDisplay", "setCpuPlayer1(", "setCpuPlayer2(", "setCpuPlayer3(",
            "setCpuPlayerDisplay(", "getCharacterStatsText(", "changeSelectedPlayerUp(", "changeSelectedPlayerDown(",
            "changeSelectedCpuOptionUp(", "changeSelectedCpuOptionDown(", "getRandomWizardOfBoat(", "GetPlayerObjectNameOverride(",
            "playerSelectedIndex",
        };

        List<string> offenders = removedMemberNames.Where(token => text.Contains(token)).ToList();

        Assert.That(offenders, Is.Empty, "StartManager.cs still contains removed player-select members: " + string.Join(", ", offenders));
    }

    [Test]
    public void PlayerSelectProductionCodeDoesNotUseGameObjectFind()
    {
        List<string> offenders = new List<string>();
        foreach (string file in EnumeratePlayerSelectFiles())
        {
            if (StripComments(File.ReadAllText(file)).Contains("GameObject.Find"))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    [Test]
    public void PlayerSelectProductionCodeDoesNotBranchOnLegacyObjectNames()
    {
        // These were the string names the old StartManager/TouchInputStartScreenController
        // branched on to decide which player/CPU method to call. The new subsystem takes typed
        // commands (SelectNextPrimary, SelectNextCpu(slot), ...) instead.
        string[] legacyNames = { "player_selected_name", "cpu1_button", "cpu2_button", "cpu3_button" };

        List<string> offenders = new List<string>();
        foreach (string file in EnumeratePlayerSelectFiles())
        {
            string text = StripComments(File.ReadAllText(file));
            foreach (string name in legacyNames)
            {
                if (text.Contains(name))
                {
                    offenders.Add($"{Relative(file)} references \"{name}\"");
                }
            }
        }

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    [Test]
    public void FighterShooterCapabilityIsNotReimplementedOutsideGameModeCompatibility()
    {
        // EnemiesOnly is the one input the fighter/shooter rule keys off. If it shows up in
        // player-select code at all, that is a second copy of the rule being built.
        List<string> offenders = new List<string>();
        foreach (string file in EnumeratePlayerSelectFiles())
        {
            if (StripComments(File.ReadAllText(file)).Contains("EnemiesOnly"))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(offenders, Is.Empty, "player-select code must query GameModeCompatibility.CharacterCanPlay instead of re-deriving the rule:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void GameOptionsNoLongerHasPlayerOrCpuSelectionIndices()
    {
        string text = StripComments(File.ReadAllText(GameOptionsPath));
        string[] removedFields = { "playerSelectedIndex", "cpu1SelectedIndex", "cpu2SelectedIndex", "cpu3SelectedIndex" };

        List<string> offenders = removedFields.Where(token => text.Contains(token)).ToList();

        Assert.That(offenders, Is.Empty, "GameOptions.cs must not reintroduce: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> EnumeratePlayerSelectFiles()
    {
        return Directory.EnumerateFiles(CoreDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(AdapterDir, "*.cs", SearchOption.AllDirectories));
    }

    private static string Relative(string path)
    {
        return path.Substring(RepoRoot.Length + 1).Replace('\\', '/');
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }
}
