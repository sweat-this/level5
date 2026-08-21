using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Guard rails for the match migration.
///
/// A strangler migration only works if the old thing stops growing. These fail the build when the
/// pattern being replaced reappears: a new global match flag, a new mode identity boolean, or a new
/// file reaching for <c>GameOptions</c> instead of the configuration.
///
/// The allowlists below are the migration's remaining debt, written down. Each entry should get
/// shorter over time; none should get longer.
/// </summary>
public class Level5MatchArchitectureTests
{
    private static readonly string ScriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts");

    /// <summary>
    /// Files still allowed to read or write <see cref="GameOptions"/> directly. Everything else
    /// takes its rules from the match configuration.
    ///
    /// This is the work left in the migration. Removing a name from this list is the definition of
    /// finishing one more consumer.
    /// </summary>
    private static readonly HashSet<string> LegacyGameOptionsConsumers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // the boundary itself: the bridge out, the fallback in, and the fields being retired
        "LegacyGameOptionsBridge.cs",
        "MatchRuntime.cs",
        "StartMenuSelectionState.cs",
        "MatchSession.cs",

        // menu, navigation and start-screen widgets, migrating in a later slice
        "StartManager.cs",
        "StartScreenCpuSelectManager.cs",
        "StartScreenTipDialogueManager.cs",
        "ProgressionManager.cs",
        "EndRoundMenuManager.cs",
        "AccountManager.cs",
        "LoadGame.cs",
        "LoadManager.cs",

        // account, api and persistence: their own owners, plan phase 11
        "APIHelper.cs",
        "HighScoreModel.cs",
        "DBConnector.cs",
        "DBHelper.cs",
        "PlayerData.cs",
        "UserAccountManager.cs",
        "LocalAccount.cs",
        "ProgressionService.cs",
        "CharacterProgressAccountId.cs",

        // gameplay consumers not yet migrated
        "GameLevelManager.cs",
        "GameRules.cs",
        "SpawnCoordinator.cs",
        "RacingGameManager.cs",
    };

    /// <summary>
    /// Files that already declare a mode identity boolean. Same ratchet as the list above: these
    /// are being replaced by GameModeId and the rule dimensions, and no name should be added.
    /// </summary>
    private static readonly HashSet<string> LegacyModeIdentityDeclarations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "GameOptions.cs",
        "StartScreenModeSelected.cs",
        "LevelSelected.cs",
        "GameRules.cs",
        "BasketBallShotMarker.cs"
    };

    /// <summary>
    /// Names that read as mode identity. Identity belongs to <c>GameModeId</c> and the rule
    /// dimensions; a new boolean for it puts the same fact in two places again.
    /// </summary>
    private static readonly Regex ModeIdentityBoolean = new Regex(
        @"\bbool\s+(?<name>(is|game[Mm]ode)[A-Za-z]*(Mode|Royal|Match|Contest)[A-Za-z]*)\s*[;=]",
        RegexOptions.Compiled);

    [Test]
    public void NoNewFileReachesForGameOptions()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateGameScripts())
        {
            string name = Path.GetFileName(file);
            if (name == "GameOptions.cs" || LegacyGameOptionsConsumers.Contains(name))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (StripComments(text).Contains("GameOptions."))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these files use GameOptions but are not on the migration allowlist. New code should take "
            + "its rules from MatchConfiguration / ResolvedMatchRules instead:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void GameOptionsGrowsNoNewMatchFields()
    {
        // GameOptions is a legacy compatibility surface being retired, not somewhere to add to.
        // The count is a ratchet: it may go down, never up. It started this migration at 85.
        // Lowered from 65 to 60 when playerSelectedIndex/cpu1SelectedIndex/cpu2SelectedIndex/
        // cpu3SelectedIndex moved to PlayerSelectionSession (player-select architecture overhaul).
        const int allowedPublicStaticFields = 60;

        string text = File.ReadAllText(Path.Combine(ScriptsRoot, "menu_start", "GameOptions.cs"));
        int fields = Regex.Matches(StripComments(text), @"static\s+public|public\s+static").Count
            - Regex.Matches(StripComments(text), @"(static\s+public|public\s+static)\s+(void|bool\s+\w+\s*\()").Count;

        Assert.That(
            fields,
            Is.LessThanOrEqualTo(allowedPublicStaticFields),
            "GameOptions has grown. New match state belongs in MatchConfiguration; new non-match state "
            + "belongs to its own owner. If you removed fields, lower the number in this test.");
    }

    [Test]
    public void NoNewModeIdentityBooleans()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateGameScripts())
        {
            if (LegacyModeIdentityDeclarations.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            foreach (Match match in ModeIdentityBoolean.Matches(StripComments(File.ReadAllText(file))))
            {
                offenders.Add($"{Relative(file)}: {match.Groups["name"].Value}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "mode identity is GameModeId plus the rule dimensions (CombatMode, ShotRule, "
            + "MatchClockMode, MatchObjective). A boolean for it is a second source of truth:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void TheAllowlistHasNoStaleEntries()
    {
        // A name left behind after its file stopped using GameOptions would quietly re-permit the
        // pattern if the file ever picked it up again.
        Dictionary<string, string> byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in EnumerateGameScripts())
        {
            byName[Path.GetFileName(file)] = file;
        }

        List<string> stale = new List<string>();
        foreach (string allowed in LegacyGameOptionsConsumers)
        {
            if (!byName.TryGetValue(allowed, out string file))
            {
                stale.Add(allowed + " (no such file)");
                continue;
            }

            if (!StripComments(File.ReadAllText(file)).Contains("GameOptions."))
            {
                stale.Add(allowed + " (no longer uses GameOptions - remove it from the allowlist)");
            }
        }

        Assert.That(stale, Is.Empty, string.Join("\n", stale));
    }

    private static IEnumerable<string> EnumerateGameScripts()
    {
        return Directory
            .EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories)
            // Directories Unity ignores, and the quarantined copy of the old start manager.
            .Where(path => !path.Contains("~") && !path.Contains(Path.Combine("Scripts", "Dev")));
    }

    private static string Relative(string path) => Level5TestSourceText.Relative(path);

    /// <summary>
    /// Strips comments so a commented-out reference does not count. Deliberately simple: it does
    /// not understand strings containing comment markers, which none of these files have. Shared
    /// with the other architecture-guard tests as <see cref="Level5TestSourceText.StripComments"/>.
    /// </summary>
    private static string StripComments(string text) => Level5TestSourceText.StripComments(text);
}
