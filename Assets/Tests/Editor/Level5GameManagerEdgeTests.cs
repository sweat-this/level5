using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Guard rails for Phase 1d of the systems restructure (see docs/phase1d-game-manager-edge-plan.md).
///
/// The plan found that most of the game-manager -&gt; player/basketball edge is either legitimate
/// roster/spawn-identity bookkeeping or real coupling whose fix carries disproportionate risk right
/// now (Unity Update-order fragility, a deferred HUD-polling design pass, a persistence-layer API
/// change). Rather than pretending the cycle is fully cut, these tests assert the direction: files
/// already on an allowlist may keep what they have, but nothing outside it may start doing the same
/// thing. Two patterns are checked, because a straight type-name search misses the more common shape
/// this coupling actually takes - see the second test.
///
/// The allowlists are the migration's remaining debt, written down, exactly like
/// <see cref="Level5MatchArchitectureTests"/>'s. Each entry should get shorter over time; none should
/// get longer without a documented reason.
/// </summary>
public class Level5GameManagerEdgeTests
{
    private static readonly string GameManagerRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "game manager");

    /// <summary>
    /// Player/basketball types that a back-reference is measured against.
    /// <c>PlayerIdentifier</c> is deliberately not restricted: game manager holding or passing the
    /// roster's own identity type is its job, not a cycle (docs/phase1d-game-manager-edge-plan.md,
    /// Category A).
    /// </summary>
    private static readonly string[] RestrictedTypeNames =
    {
        "PlayerController",
        "PlayerHealth",
        "PlayerAttackQueue",
        "AutoPlayerController",
        "AutoPlayerDefense",
        "CharacterProfile",
        "BasketBall",
        "BasketBallAuto",
        "BasketBallState",
        "BasketBallShotMarker",
        "GameStats",
    };

    /// <summary>
    /// Files allowed to spell one of <see cref="RestrictedTypeNames"/>, with the reason a reference
    /// there is accepted rather than fixed. Removing a name is the definition of finishing one more
    /// slice; adding one back without updating docs/phase1d-game-manager-edge-plan.md is exactly the
    /// regrowth this test exists to catch.
    /// </summary>
    private static readonly HashSet<string> SpelledTypeAllowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Live facade (PlayerController1/PlayerHealth/PlayerAttackQueue) with ~25 external callers in
        // other folders, plus BasketBall.instance for the stats-toggle keybind it handles itself.
        // Inverting it is those folders' work, not a game-manager-scoped slice.
        "GameLevelManager.cs",

        // PlayerHealth.OnDied subscription, campaign-portrait CharacterProfile read, and
        // BasketBallShotMarker win-condition bookkeeping - shot-lifecycle.md already concluded the
        // marker state "correctly stays inline... it feeds GameRules.IsGameOver()'s win condition
        // directly." GameStats here is load-bearing: gameStats1/primaryGameStats call
        // GameStats.getExperienceGainedFromSession(), which (AUD-010 Phase 2b0) needs the match's
        // bound ResolvedMatchRules (GameStats.BindMatchRules) - composition state MatchStats does not
        // hold, so this cannot narrow to MatchStats; SaveMatchResults'/GetPrimaryGameStats' GameStats
        // feed DBConnector.savePlayerAllTimeStats(GameStats), a persistence-layer boundary this plan
        // does not change.
        "GameRules.cs",

        // BasketBall.instance.BasketBallState/.LastShotDistance live-text reads and the
        // getSortedGameStatsList() scoreboard formatting are shot-lifecycle.md's deferred "move the
        // HUD off polling" design pass. GameStats here is load-bearing for the same
        // getExperienceGainedFromSession()/bound-ResolvedMatchRules reason as GameRules.
        "MatchHudPresenter.cs",

        // Spawning/registering participants is this class's whole job.
        "SpawnCoordinator.cs",

        // updateFreePlayStats()'s primaryGameStats feeds DBConnector.savePlayerAllTimeStats(GameStats) /
        // PendingMatchPersistenceStore.QueueAllTime(string, GameStats) - the same persistence-layer
        // boundary GameRules can't narrow past either.
        "Pause.cs",
    };

    /// <summary>
    /// A spelled type name misses the more common shape of this coupling: a chain that reaches
    /// through <c>PlayerIdentifier</c>'s public fields into a foreign type without ever spelling that
    /// type's name (<c>player.basketBallState.Thrown</c> spells only <c>PlayerIdentifier</c>). This
    /// checks for that pattern directly.
    /// </summary>
    private static readonly Regex ReachThroughChain = new Regex(
        @"\.basketBallState\.|\.playerController\.|\.autoPlayerController\.|\.gameStats\.|\.characterProfile\.",
        RegexOptions.Compiled);

    /// <summary>Files allowed a reach-through chain, with the reason.</summary>
    private static readonly HashSet<string> ReachThroughAllowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // getSortedGameStatsList()'s x.gameStats.Stats.TotalPoints - roster/stats sorting for
        // campaign high-score tracking, the same category as the rest of this file's PlayerIdentifier use.
        "GameLevelManager.cs",

        // LoadNextCampaignLevel's players[0].gameStats.Stats.TotalPoints / players[1]... for the
        // campaign end-round winner/loser score - match-end/campaign-transition bookkeeping, the same
        // category as the rest of this file's roster gathering.
        "GameRules.cs",

        // updatePlayerScore()'s and GetDisplayText's versus-branch scoreboard formatting, and
        // SetScoreDisplayText's ConsecutiveShots/InThePocket branches reading
        // GameLevelManager.instance.Player1.gameStats.Stats directly - all part of the deferred HUD
        // design pass (see SpelledTypeAllowlist's note on this file).
        "MatchHudPresenter.cs",

        // InitializeHumanProfile's identifier.characterProfile.intializeShooterStatsFromProfile(...) -
        // rebuilding a freshly spawned human's stats from their roster slot is what this class exists to do.
        "SpawnCoordinator.cs",

        // ReportTimeExpired()'s three-way reach-through (basketBallState.Thrown, playerController.Grounded,
        // gameStats.Stats.ConsecutiveShotsMade). The fix that would actually invert this - running the
        // check from the player/ball side's own Update() instead - risks the exact Unity Update-order
        // bug this file's own comments already document as having shipped once in this area. Deferred;
        // revisit only if it causes an actual bug.
        "Timer.cs",
    };

    [Test]
    public void NoNewFileReachesForRestrictedPlayerOrBasketballTypes()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateGameManagerScripts())
        {
            string name = Path.GetFileName(file);
            if (SpelledTypeAllowlist.Contains(name))
            {
                continue;
            }

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
            "these game-manager files reach for a player/basketball type but are not on the "
            + "allowlist in Level5GameManagerEdgeTests. See docs/phase1d-game-manager-edge-plan.md - "
            + "either this is legitimate roster/spawn-identity work (add to the allowlist with a "
            + "reason) or it is the cycle reforming (fix it instead):\n" + string.Join("\n", offenders));
    }

    [Test]
    public void NoNewFileReachesThroughPlayerIdentifierChains()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateGameManagerScripts())
        {
            string name = Path.GetFileName(file);
            if (ReachThroughAllowlist.Contains(name))
            {
                continue;
            }

            string text = StripComments(File.ReadAllText(file));
            if (ReachThroughChain.IsMatch(text))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these game-manager files reach through a PlayerIdentifier field "
            + "(.basketBallState./.playerController./.autoPlayerController./.gameStats./.characterProfile.) "
            + "into a foreign type without spelling its name - the same coupling the spelled-type test "
            + "checks, in the shape it actually takes most often. See "
            + "docs/phase1d-game-manager-edge-plan.md:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void TheSpelledTypeAllowlistHasNoStaleEntries()
    {
        Dictionary<string, string> byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in EnumerateGameManagerScripts())
        {
            byName[Path.GetFileName(file)] = file;
        }

        List<string> stale = new List<string>();
        foreach (string allowed in SpelledTypeAllowlist)
        {
            if (!byName.TryGetValue(allowed, out string file))
            {
                stale.Add(allowed + " (no such file)");
                continue;
            }

            string text = StripComments(File.ReadAllText(file));
            bool stillUsesOne = RestrictedTypeNames.Any(type => Regex.IsMatch(text, $@"\b{type}\b"));
            if (!stillUsesOne)
            {
                stale.Add(allowed + " (no longer reaches for a restricted type - remove it from the allowlist)");
            }
        }

        Assert.That(stale, Is.Empty, string.Join("\n", stale));
    }

    [Test]
    public void TheReachThroughAllowlistHasNoStaleEntries()
    {
        Dictionary<string, string> byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in EnumerateGameManagerScripts())
        {
            byName[Path.GetFileName(file)] = file;
        }

        List<string> stale = new List<string>();
        foreach (string allowed in ReachThroughAllowlist)
        {
            if (!byName.TryGetValue(allowed, out string file))
            {
                stale.Add(allowed + " (no such file)");
                continue;
            }

            if (!ReachThroughChain.IsMatch(StripComments(File.ReadAllText(file))))
            {
                stale.Add(allowed + " (no longer reaches through a PlayerIdentifier chain - remove it from the allowlist)");
            }
        }

        Assert.That(stale, Is.Empty, string.Join("\n", stale));
    }

    private static IEnumerable<string> EnumerateGameManagerScripts()
    {
        return Directory
            .EnumerateFiles(GameManagerRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("~"));
    }

    private static string Relative(string path) => Level5TestSourceText.Relative(path);

    /// <summary>
    /// Strips comments so a commented-out reference does not count. Deliberately simple: it does not
    /// understand strings containing comment markers, which none of these files have - same
    /// simplification <see cref="Level5MatchArchitectureTests"/> makes. Shared with the other
    /// architecture-guard tests as <see cref="Level5TestSourceText.StripComments"/>.
    /// </summary>
    private static string StripComments(string text) => Level5TestSourceText.StripComments(text);
}
