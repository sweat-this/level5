using System.Collections.Generic;
using System.Text;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The parity validator: the new model against the data the game actually ships with.
///
/// The authored modes and levels live in a binary prefab, so this is the only place they can be
/// checked. Every mode is converted and then converted back, and the round trip has to land on the
/// booleans the prefab holds. If it does not, the new model cannot represent something the shipping
/// data says - which is exactly the failure that would otherwise show up as a mode quietly playing
/// differently.
///
/// The tests skip rather than fail when the prefab is absent, so a checkout without it still runs
/// the rest of the suite.
/// </summary>
public class Level5AuthoredMatchDataTests
{
    /// <summary>Mirrors <c>StartManager.CampaignFirstLevelId</c>, which is private to the menu.</summary>
    private const int CampaignFirstLevelId = 1;

    private List<StartScreenModeSelected> authoredModes;
    private List<LevelSelected> authoredLevels;

    [SetUp]
    public void SetUp()
    {
        // Resolved exactly the way the migration utility resolves it, so the validator and the
        // thing it validates cannot be reading different data.
        if (!MatchDefinitionMigration.TryLoadAuthoredSources(out authoredModes, out authoredLevels, out string error))
        {
            Assert.Ignore(error);
        }
    }

    [Test]
    public void EveryAuthoredModeRoundTripsThroughTheRuleDimensions()
    {
        StringBuilder differences = new StringBuilder();

        foreach (StartScreenModeSelected source in authoredModes)
        {
            GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
            if (mode == null)
            {
                continue;
            }

            string label = $"{source.ModeDisplayName} (id {source.ModeId})";
            Compare(differences, label, "modeRequiresCountDown", source.ModeRequiresCountDown, mode.RequiresCountDown);
            Compare(differences, label, "modeRequiresCounter", source.ModeRequiresCounter, mode.RequiresCounter);
            Compare(differences, label, "shotMarkers3s", source.ModeRequiresShotMarkers3S, mode.RequiresShotMarkers3s);
            Compare(differences, label, "shotMarkers4s", source.ModeRequiresShotMarkers4S, mode.RequiresShotMarkers4s);
            Compare(differences, label, "shotMarkers7s", source.ModeRequiresShotMarkers7s, mode.RequiresShotMarkers7s);
            Compare(differences, label, "threePointContest", source.GameModeThreePointContest, mode.IsThreePointContest);
            Compare(differences, label, "fourPointContest", source.GameModeFourPointContest, mode.IsFourPointContest);
            Compare(differences, label, "sevenPointContest", source.GameModeSevenPointContest, mode.IsSevenPointContest);
            Compare(differences, label, "allPointContest", source.GameModeAllPointContest, mode.IsAllPointContest);
            Compare(differences, label, "isBattleRoyal", source.IsBattleRoyal, mode.IsBattleRoyal);
            Compare(differences, label, "isCageMatch", source.IsCageMatch, mode.IsCageMatch);
            Compare(differences, label, "enemiesOnly", source.EnemiesOnlyEnabled, mode.EnemiesOnly);
            Compare(differences, label, "requiresBasketball", source.GameModeRequiresBasketball, mode.RequiresBasketball);
            Compare(differences, label, "requiresMoneyBall", source.ModeRequiresMoneyBall, mode.RequiresMoneyBall);
            Compare(differences, label, "requiresConsecutiveShots", source.ModeRequiresConsecutiveShots, mode.RequiresConsecutiveShots);
            Compare(differences, label, "requiresPlayerSurvive", source.GameModeRequiresPlayerSurvive, mode.RequiresPlayerSurvive);
            Compare(differences, label, "allowsCpuShooters", source.GameModeAllowsCpuShooters, mode.AllowsCpuShooters);
            Compare(differences, label, "arcadeMode", source.ArcadeModeActive, mode.ArcadeMode);

            float expectedTimer = source.CustomTimer > 0f ? source.CustomTimer : 0f;
            if (!Mathf.Approximately(expectedTimer, mode.CustomTimerSeconds))
            {
                differences.AppendLine($"{label}: customTimer authored {expectedTimer}, resolved {mode.CustomTimerSeconds}");
            }
        }

        Assert.That(differences.ToString(), Is.Empty, "authored mode data the rule dimensions cannot reproduce:\n" + differences);
    }

    [Test]
    public void NoAuthoredModeSaysSomethingTheRuleDimensionsCannotHold()
    {
        List<string> anomalies = new List<string>();
        GameModeDefinitionFactory.CreateAll(authoredModes, anomalies);

        // An anomaly means the conversion had to interpret the authored data rather than carry it
        // across, and an interpretation is a behaviour decision someone should make deliberately.
        // This is what caught the all-point contest holding three contest flags at once, and Cage
        // Match being marked battle royal as well - both of which the dimensions now represent
        // rather than resolve away.
        Assert.That(anomalies, Is.Empty, string.Join("\n", anomalies));
    }

    [Test]
    public void AuthoredModeIdsAreUniqueAndKnown()
    {
        GameModeCatalog catalog = new GameModeCatalog(GameModeDefinitionFactory.CreateAll(authoredModes));

        Assert.That(catalog.Problems, Is.Empty, string.Join("\n", catalog.Problems));
        Assert.That(catalog.Count, Is.EqualTo(authoredModes.Count));

        foreach (GameModeDefinition mode in catalog.Definitions)
        {
            Assert.That(
                GameModeIds.IsKnown(mode.RawModeId),
                Is.True,
                $"authored mode '{mode.DisplayName}' uses id {mode.RawModeId}, which GameModeId does not declare");
        }
    }

    [Test]
    public void AuthoredLevelIdsAreUnique()
    {
        LevelDefinitionCatalog catalog = new LevelDefinitionCatalog(LevelDefinitionFactory.CreateAll(authoredLevels));

        Assert.That(catalog.Problems, Is.Empty, string.Join("\n", catalog.Problems));
    }

    [Test]
    public void EveryAuthoredLevelRoundTripsThroughArenaCapabilities()
    {
        StringBuilder differences = new StringBuilder();

        foreach (LevelSelected source in authoredLevels)
        {
            LevelDefinition level = LevelDefinitionFactory.Create(source);
            if (level == null)
            {
                continue;
            }

            string label = $"{source.LevelDisplayName} (id {source.LevelId})";
            Compare(differences, label, "isShootingLevel", source.IsShootingLevel, level.IsShootingLevel);
            Compare(differences, label, "isFightingLevel", source.IsFightingLevel, level.IsFightingLevel);
            Compare(differences, label, "isCageMatchLevel", source.IsCageMatchLevel, level.IsCageMatchLevel);
            Compare(differences, label, "isBattleRoyalLevel", source.IsBattleRoyalLevel, level.IsBattleRoyalLevel);
            Compare(differences, label, "levelHasSevenPointers", source.LevelHasSevenPointers, level.HasSevenPointers);
            Compare(differences, label, "levelHasTraffic", source.LevelHasTraffic, level.HasTraffic);
            Compare(differences, label, "levelHasWeather", source.LevelHasWeather, level.HasWeather);
            Compare(differences, label, "levelRequiresTimeOfDay", source.LevelRequiresTimeOfDay, level.RequiresTimeOfDay);
            Compare(differences, label, "customCamera", source.CustomCamera, level.CustomCamera);

            string expectedScene = source.LevelObjectName + "_" + source.LevelDescription;
            if (!string.IsNullOrEmpty(source.LevelDescription) && level.SceneName != expectedScene)
            {
                differences.AppendLine($"{label}: scene name authored '{expectedScene}', resolved '{level.SceneName}'");
            }
        }

        Assert.That(differences.ToString(), Is.Empty, "authored level data the capability flags cannot reproduce:\n" + differences);
    }

    [Test]
    public void EveryAuthoredModeCanBePlayedSomewhere()
    {
        // A mode with no compatible arena is unreachable from the menu. Before compatibility had an
        // owner this could not be checked at all - the recursive cycling just spun.
        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(GameModeDefinitionFactory.CreateAll(authoredModes)),
            new LevelDefinitionCatalog(LevelDefinitionFactory.CreateAll(authoredLevels)));

        List<string> unplayable = new List<string>();
        foreach (GameModeDefinition mode in compatibility.Modes.Definitions)
        {
            if (compatibility.LevelsFor(mode).Count == 0)
            {
                unplayable.Add($"{mode.DisplayName} (id {mode.RawModeId}) has no compatible arena");
            }
        }

        Assert.That(unplayable, Is.Empty, string.Join("\n", unplayable));
    }

    [Test]
    public void TheCampaignStartLevelCanHostTheCampaignMode()
    {
        // The campaign forces its own start level at launch, so that pairing never goes through
        // the menu's filtering - if it is not compatible, the only place it can be caught is here
        // or at the launch validation, and at launch it is already too late for the player.
        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(GameModeDefinitionFactory.CreateAll(authoredModes)),
            new LevelDefinitionCatalog(LevelDefinitionFactory.CreateAll(authoredLevels)));

        GameModeDefinition campaign = compatibility.Modes.Find(GameModeId.BeatThaComputahs);
        if (campaign == null)
        {
            Assert.Ignore("This build has no campaign mode.");
        }

        LevelDefinition startLevel = compatibility.Levels.Find(CampaignFirstLevelId);
        Assert.That(startLevel, Is.Not.Null, $"no authored level has id {CampaignFirstLevelId}");
        Assert.That(
            compatibility.CanPlay(campaign, startLevel),
            Is.True,
            $"'{campaign.DisplayName}' starts on '{startLevel.DisplayName}', which it cannot be played in");
    }

    private static void Compare(StringBuilder differences, string label, string field, bool authored, bool resolved)
    {
        if (authored != resolved)
        {
            differences.AppendLine($"{label}: {field} authored {authored}, resolved {resolved}");
        }
    }
}
