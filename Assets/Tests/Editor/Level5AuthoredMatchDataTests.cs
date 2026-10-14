using System.Collections.Generic;
using System.Text;
using Level5.Core.Match;
using Level5.Core.Progression;
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
    public void NewAndUnmigratedModeAuthoringDefaultsToOneLocalHuman()
    {
        GameObject host = new GameObject("unmigrated-mode");
        GameModeDefinition asset = ScriptableObject.CreateInstance<GameModeDefinition>();
        GameModeDefinition converted = null;
        try
        {
            StartScreenModeSelected source = host.AddComponent<StartScreenModeSelected>();
            // Simulate old serialized data: its known Total Points id must not imply certification.
            JsonUtility.FromJsonOverwrite("{\"modeId\":1}", source);
            JsonUtility.FromJsonOverwrite("{\"modeId\":1}", asset);
            converted = GameModeDefinitionFactory.Create(source);
            Assert.That(source.SupportsTwoLocalHumans, Is.False);
            Assert.That(asset.SupportsTwoLocalHumans, Is.False);
            Assert.That(converted.SupportsTwoLocalHumans, Is.False);
            Assert.That(GameModeDefinitionData.Default(1).SupportsTwoLocalHumans, Is.False);
            Assert.That(GameModeDefinitionFactory.Create(null), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(converted);
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(host);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ModeConversionAndMigrationCopyPreserveTheAuthoredOptIn(bool supported)
    {
        GameObject host = new GameObject("authored-mode");
        GameModeDefinition converted = null;
        GameModeDefinition existing = ScriptableObject.CreateInstance<GameModeDefinition>();
        try
        {
            StartScreenModeSelected source = host.AddComponent<StartScreenModeSelected>();
            SerializedObject serialized = new SerializedObject(source);
            serialized.FindProperty("modeId").intValue = 1;
            serialized.FindProperty("supportsTwoLocalHumans").boolValue = supported;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            converted = GameModeDefinitionFactory.Create(source);
            Assert.That(converted.SupportsTwoLocalHumans, Is.EqualTo(supported));

            // Exercise the exact copy used when migration updates an existing asset in place.
            System.Reflection.MethodInfo copy = typeof(MatchDefinitionMigration).GetMethod("ToData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null, new[] { typeof(GameModeDefinition) }, null);
            Assert.That(copy, Is.Not.Null);
            GameModeDefinitionData previous = GameModeDefinitionData.Default(1);
            previous.SupportsTwoLocalHumans = !supported;
            existing.Apply(previous);
            existing.Apply((GameModeDefinitionData)copy.Invoke(null, new object[] { converted }));
            Assert.That(existing.SupportsTwoLocalHumans, Is.EqualTo(supported), "migration must both set and clear the flag");
        }
        finally
        {
            Object.DestroyImmediate(existing);
            Object.DestroyImmediate(converted);
            Object.DestroyImmediate(host);
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
            Compare(differences, label, "supportsTwoLocalHumans", source.SupportsTwoLocalHumans, mode.SupportsTwoLocalHumans);

            float expectedTimer = source.CustomTimer > 0f ? source.CustomTimer : 0f;
            if (!Mathf.Approximately(expectedTimer, mode.CustomTimerSeconds))
            {
                differences.AppendLine($"{label}: customTimer authored {expectedTimer}, resolved {mode.CustomTimerSeconds}");
            }
        }

        Assert.That(differences.ToString(), Is.Empty, "authored mode data the rule dimensions cannot reproduce:\n" + differences);
    }

    [Test]
    public void AuthoredInThePocketDescriptionMatchesTheAlwaysActiveBonus()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_15.prefab");
        Assert.That(prefab, Is.Not.Null);
        StartScreenModeSelected source = prefab.GetComponent<StartScreenModeSelected>();
        Assert.That(source, Is.Not.Null);
        Assert.That(source.ModeId, Is.EqualTo(15));
        Assert.That(source.ModeDescription, Is.EqualTo(
            "Bonus always active: 3s +1, 4s +2, 7s +3. No bonus for 2s."));
        Assert.That(source.ModeRequiresCountDown, Is.True);
        Assert.That(source.ModeRequiresCounter, Is.False);
        Assert.That(source.CustomTimer, Is.Zero);
        Assert.That(source.HighScoreField, Is.EqualTo("totalPoints"));

        GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
        LevelDefinition arena = LevelDefinitionFactory.Create(authoredLevels.Find(level => level.LevelId == 1));
        try
        {
            ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
                mode, arena, TestDefinitions.SoloRoster(), MatchModifiers.Default);
            Assert.That(mode.Id, Is.EqualTo(GameModeId.InThePocket));
            Assert.That(mode.Description, Is.EqualTo(source.ModeDescription));
            Assert.That(mode.HighScoreField, Is.EqualTo("totalPoints"));
            Assert.That(rules.ClockMode, Is.EqualTo(MatchClockMode.Countdown));
            Assert.That(rules.CustomTimerSeconds, Is.Zero);
            Assert.That(rules.MatchLengthSeconds, Is.EqualTo(180f));
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Score));
            Assert.That(rules.RequiresConsecutiveShots, Is.False);
            Assert.That(rules.ShotRule, Is.EqualTo(ShotRule.Any));
            Assert.That(rules.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.None));
        }
        finally
        {
            Object.DestroyImmediate(mode);
            Object.DestroyImmediate(arena);
        }
    }

    [TestCase(14, 60f, "The clock starts at 60 seconds.")]
    [TestCase(24, 90f, "90 seconds to get a high score.")]
    public void AuthoredCountdownDescriptionsMatchTheStartingClock(int id, float seconds, string countdownText)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_{id:D2}.prefab");
        Assert.That(prefab, Is.Not.Null);
        StartScreenModeSelected source = prefab.GetComponent<StartScreenModeSelected>();
        Assert.That(source, Is.Not.Null);
        Assert.That(source.ModeId, Is.EqualTo(id));
        Assert.That(source.ModeRequiresCountDown, Is.True);
        Assert.That(source.ModeRequiresCounter, Is.False);
        Assert.That(source.CustomTimer, Is.EqualTo(seconds));
        Assert.That(source.ModeDescription, Does.Contain(countdownText));

        GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
        LevelDefinition arena = LevelDefinitionFactory.Create(authoredLevels.Find(level => level.LevelId == 1));
        try
        {
            ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
                mode, arena, TestDefinitions.SoloRoster(), MatchModifiers.Default);
            Assert.That(mode.RawModeId, Is.EqualTo(id));
            Assert.That(mode.Description, Is.EqualTo(source.ModeDescription));
            Assert.That(mode.CustomTimerSeconds, Is.EqualTo(seconds));
            Assert.That(rules.ClockMode, Is.EqualTo(MatchClockMode.Countdown));
            Assert.That(rules.MatchLengthSeconds, Is.EqualTo(seconds));

            if (id == 14)
            {
                Assert.That(source.ModeDescription, Does.Contain(
                    "Keep a streak of at least 3 going to continue after time runs out."));
                Assert.That(rules.Objective, Is.EqualTo(MatchObjective.ConsecutiveShots));
                Assert.That(rules.RequiresConsecutiveShots, Is.True);
                Assert.That(MatchEndConditions.ConsecutiveShotsToPlayOn, Is.EqualTo(3));
                Assert.That(MatchEndConditions.TimeExpired(rules.RequiresConsecutiveShots,
                    ballThrown: false, playerGrounded: true, consecutiveShotsMade: 2), Is.True);
                Assert.That(MatchEndConditions.TimeExpired(rules.RequiresConsecutiveShots,
                    ballThrown: false, playerGrounded: true, consecutiveShotsMade: 3), Is.False);
            }
            else
            {
                Assert.That(rules.Objective, Is.EqualTo(MatchObjective.ContestCompletion));
                Assert.That(rules.RequiresConsecutiveShots, Is.False);
                Assert.That(rules.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.SevenPoint));
                Assert.That(rules.ShotRule, Is.EqualTo(ShotRule.SevenPoint));
                Assert.That(rules.IsSevenPointContest, Is.True);
                Assert.That(rules.RequiresMoneyBall, Is.False);
                Assert.That(MatchEndConditions.MarkersCleared(1), Is.False);
                Assert.That(MatchEndConditions.MarkersCleared(0), Is.True);
            }
        }
        finally
        {
            Object.DestroyImmediate(mode);
            Object.DestroyImmediate(arena);
        }
    }

    [TestCase(16, 70f, "70 seconds to get a high score.", ShotMarkerRequirement.ThreePoint,
        ShotRule.ThreePoint, true)]
    [TestCase(17, 75f, "75 seconds to get a high score.", ShotMarkerRequirement.FourPoint,
        ShotRule.FourPoint, false)]
    [TestCase(18, 140f, "140 seconds to get a high score.",
        ShotMarkerRequirement.ThreePoint | ShotMarkerRequirement.FourPoint,
        ShotRule.ThreePoint | ShotRule.FourPoint | ShotRule.AllRanges, false)]
    [TestCase(24, 90f, "90 seconds to get a high score.", ShotMarkerRequirement.SevenPoint,
        ShotRule.SevenPoint, false)]
    public void AuthoredContestDescriptionsPreserveExistingRulesWithoutPromisingATimeBonus(
        int id, float seconds, string description, ShotMarkerRequirement markers,
        ShotRule shotRule, bool allowsCpuShooters)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_{id:D2}.prefab");
        Assert.That(prefab, Is.Not.Null);
        StartScreenModeSelected source = prefab.GetComponent<StartScreenModeSelected>();
        Assert.That(source, Is.Not.Null);
        Assert.That(source.ModeId, Is.EqualTo(id));
        Assert.That(source.ModeDescription, Is.EqualTo(description));
        Assert.That(source.ModeDescription, Does.Not.Contain("Remaining time").IgnoreCase);
        Assert.That(source.ModeDescription, Does.Not.Contain("bonus").IgnoreCase);
        Assert.That(source.CustomTimer, Is.EqualTo(seconds));
        Assert.That(source.ModeRequiresCountDown, Is.True);
        Assert.That(source.ModeRequiresCounter, Is.False);
        Assert.That(source.ModeRequiresShotMarkers3S,
            Is.EqualTo((markers & ShotMarkerRequirement.ThreePoint) != 0));
        Assert.That(source.ModeRequiresShotMarkers4S,
            Is.EqualTo((markers & ShotMarkerRequirement.FourPoint) != 0));
        Assert.That(source.ModeRequiresShotMarkers7s,
            Is.EqualTo((markers & ShotMarkerRequirement.SevenPoint) != 0));
        Assert.That(source.GameModeThreePointContest, Is.EqualTo(id == 16 || id == 18));
        Assert.That(source.GameModeFourPointContest, Is.EqualTo(id == 17 || id == 18));
        Assert.That(source.GameModeSevenPointContest, Is.EqualTo(id == 24));
        Assert.That(source.GameModeAllPointContest, Is.EqualTo(id == 18));
        Assert.That(source.GameModeAllowsCpuShooters, Is.EqualTo(allowsCpuShooters));
        Assert.That(source.GameModeRequiresBasketball, Is.True);
        Assert.That(source.ModeRequiresMoneyBall, Is.False);
        Assert.That(source.ModeRequiresConsecutiveShots, Is.False);
        Assert.That(source.GameModeRequiresPlayerSurvive, Is.False);
        Assert.That(source.HighScoreField, Is.EqualTo("totalPoints"));

        GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
        LevelDefinition arena = LevelDefinitionFactory.Create(authoredLevels.Find(level => level.LevelId == 1));
        try
        {
            ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
                mode, arena, TestDefinitions.SoloRoster(), MatchModifiers.Default);
            Assert.That(mode.RawModeId, Is.EqualTo(id));
            Assert.That(mode.Description, Is.EqualTo(description));
            Assert.That(mode.CustomTimerSeconds, Is.EqualTo(seconds));
            Assert.That(mode.MinPlayers, Is.EqualTo(1));
            Assert.That(mode.MaxPlayers, Is.EqualTo(PlayerRoster.MaxSlots));
            Assert.That(mode.RequiresCpuOpponent, Is.False);
            Assert.That(mode.AddsImplicitDefender, Is.False);
            Assert.That(mode.AllowsCpuShooters, Is.EqualTo(allowsCpuShooters));
            Assert.That(mode.ShotMarkers, Is.EqualTo(markers));
            Assert.That(mode.ShotRule, Is.EqualTo(shotRule));
            Assert.That(rules.ClockMode, Is.EqualTo(MatchClockMode.Countdown));
            Assert.That(rules.CustomTimerSeconds, Is.EqualTo(seconds));
            Assert.That(rules.MatchLengthSeconds, Is.EqualTo(seconds));
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.ContestCompletion));
            Assert.That(rules.ShotMarkers, Is.EqualTo(markers));
            Assert.That(rules.ShotRule, Is.EqualTo(shotRule));
            Assert.That(rules.IsThreePointContest, Is.EqualTo(source.GameModeThreePointContest));
            Assert.That(rules.IsFourPointContest, Is.EqualTo(source.GameModeFourPointContest));
            Assert.That(rules.IsSevenPointContest, Is.EqualTo(source.GameModeSevenPointContest));
            Assert.That(rules.IsAllPointContest, Is.EqualTo(source.GameModeAllPointContest));
            Assert.That(rules.AllowsCpuShooters, Is.EqualTo(allowsCpuShooters));
            Assert.That(rules.BasketballCount, Is.EqualTo(1));
            Assert.That(rules.AddsImplicitDefender, Is.False);
            Assert.That(rules.RequiresBasketball, Is.True);
            Assert.That(rules.RequiresMoneyBall, Is.False);
            Assert.That(rules.RequiresConsecutiveShots, Is.False);
            Assert.That(rules.RequiresPlayerSurvive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(mode);
            Object.DestroyImmediate(arena);
        }
    }

    [TestCase(21, "Battle Royal", CombatMode.BattleRoyal)]
    [TestCase(22, "Cage Match", CombatMode.Cage | CombatMode.BattleRoyal)]
    public void AuthoredCombatSurvivalObjectivesPreserveTheirIndependentRules(int id, string name, CombatMode combat)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_{id:D2}.prefab");
        Assert.That(prefab, Is.Not.Null);
        StartScreenModeSelected source = prefab.GetComponent<StartScreenModeSelected>();
        Assert.That(source, Is.Not.Null);
        GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
        var arenas = new List<LevelDefinition>();
        try
        {
            foreach (int arenaId in new[] { 1, 17, 18 })
            {
                LevelSelected arenaSource = authoredLevels.Find(level => level.LevelId == arenaId);
                Assert.That(arenaSource, Is.Not.Null);
                arenas.Add(LevelDefinitionFactory.Create(arenaSource));
            }
            ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
                mode, arenas[1], TestDefinitions.SoloRoster(isShooter: false), MatchModifiers.Default);
            Assert.That(source.ModeId, Is.EqualTo(id));
            Assert.That(mode.RawModeId, Is.EqualTo(id));
            Assert.That(mode.DisplayName, Is.EqualTo(name));
            Assert.That(mode.Description, Is.EqualTo("Survive"));
            Assert.That(mode.HighScoreField, Is.EqualTo("enemiesKilled"));
            Assert.That(mode.Objective, Is.EqualTo(MatchObjective.Survival));
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Survival));
            Assert.That(rules.CombatMode, Is.EqualTo(combat));
            Assert.That(rules.ClockMode, Is.EqualTo(MatchClockMode.CountUp));
            Assert.That(rules.CustomTimerSeconds, Is.Zero);
            Assert.That(rules.EnemiesOnly, Is.True);
            Assert.That(rules.EnemiesEnabled, Is.True);
            Assert.That(rules.RequiresBasketball, Is.False);
            Assert.That(rules.AllowsCpuShooters, Is.False);
            Assert.That(rules.RequiresPlayerSurvive, Is.False);
            Assert.That(rules.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.None));
            Assert.That(rules.ShotRule, Is.EqualTo(ShotRule.Any));
            Assert.That(mode.MinPlayers, Is.EqualTo(1));
            Assert.That(mode.MaxPlayers, Is.EqualTo(PlayerRoster.MaxSlots));
            Assert.That(mode.RequiresCpuOpponent, Is.False);
            Assert.That(GameModeCompatibility.CharacterCanPlay(mode, MatchModifiers.Default,
                TestDefinitions.Character("non-fighter", isFighter: false)), Is.False);
            var compatibility = new GameModeCompatibility(new GameModeCatalog(new[] { mode }),
                new LevelDefinitionCatalog(arenas));
            Assert.That(compatibility.CanPlay(mode, arenas[0]), Is.False);
            Assert.That(compatibility.CanPlay(mode, arenas[1]), Is.True);
            Assert.That(compatibility.CanPlay(mode, arenas[2]), Is.EqualTo(id == 21));
        }
        finally
        {
            Object.DestroyImmediate(mode);
            foreach (LevelDefinition arena in arenas) Object.DestroyImmediate(arena);
        }
    }

    [Test]
    public void EveryAuthoredModeKeepsItsExistingStartingClock()
    {
        Dictionary<int, float> customTimers = new Dictionary<int, float>
        {
            [14] = 60f, [16] = 70f, [17] = 75f, [18] = 140f,
            [24] = 90f, [26] = 60f, [27] = 120f
        };
        LevelDefinition arena = LevelDefinitionFactory.Create(authoredLevels.Find(level => level.LevelId == 1));
        try
        {
            foreach (StartScreenModeSelected source in authoredModes)
            {
                float customTimer = customTimers.TryGetValue(source.ModeId, out float seconds) ? seconds : 0f;
                GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
                try
                {
                    ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
                        mode, arena, TestDefinitions.SoloRoster(), MatchModifiers.Default);
                    Assert.That(source.CustomTimer, Is.EqualTo(customTimer), $"mode {source.ModeId}");
                    Assert.That(rules.MatchLengthSeconds, Is.EqualTo(customTimer > 0f ? customTimer : 180f),
                        $"mode {source.ModeId}");
                }
                finally
                {
                    Object.DestroyImmediate(mode);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(arena);
        }
    }

    [TestCase(GameModeId.SpotUp3s, ShotMarkerRequirement.ThreePoint, MatchClockMode.CountUp)]
    [TestCase(GameModeId.SpotUp4s, ShotMarkerRequirement.FourPoint, MatchClockMode.CountUp)]
    [TestCase(GameModeId.SpotUp7s, ShotMarkerRequirement.SevenPoint, MatchClockMode.Countdown)]
    [TestCase(GameModeId.SpotUpAll, ShotMarkerRequirement.ThreePoint | ShotMarkerRequirement.FourPoint, MatchClockMode.CountUp)]
    public void AuthoredSpotUpModesResolveToMakeQuotaCourses(
        GameModeId id, ShotMarkerRequirement markers, MatchClockMode clock)
    {
        // Pin the shipping prefab, not just parity with whatever booleans it happens to contain.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Resources/Prefabs/menu_start/mode_selected_objects/mode_selected_{(int)id:D2}.prefab");
        Assert.That(prefab, Is.Not.Null);
        StartScreenModeSelected source = prefab.GetComponent<StartScreenModeSelected>();
        Assert.That(source, Is.Not.Null);
        Assert.That(source.ModeId, Is.EqualTo((int)id));
        Assert.That(authoredModes.Exists(authored => authored.ModeId == (int)id), Is.True);
        Assert.That(source.ModeRequiresShotMarkers7s,
            Is.EqualTo((markers & ShotMarkerRequirement.SevenPoint) != 0));
        if (id == GameModeId.SpotUp7s)
        {
            Assert.That(source.ModeDescription, Is.EqualTo(
                "Make the required 7-point shots from each position marker to complete the course."));
            Assert.That(source.HighScoreField, Is.EqualTo("time"));
        }

        GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
        LevelDefinition arena = LevelDefinitionFactory.Create(authoredLevels.Find(level => level.LevelId == 1));
        try
        {
            ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
                mode, arena, TestDefinitions.SoloRoster(), MatchModifiers.Default);
            Assert.That(mode.ShotMarkers, Is.EqualTo(markers));
            Assert.That(mode.Description, Is.EqualTo(source.ModeDescription));
            Assert.That(rules.ShotMarkers, Is.EqualTo(markers));
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.ContestCompletion));
            Assert.That(rules.ClockMode, Is.EqualTo(clock));
            Assert.That(rules.MatchLengthSeconds, Is.EqualTo(180f));
            Assert.That(rules.ShotRule, Is.EqualTo(ShotRule.Any), "Spot-Up counts makes, not contest attempts");
            Assert.That(rules.IsThreePointContest || rules.IsFourPointContest
                || rules.IsSevenPointContest || rules.IsAllPointContest, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(mode);
            Object.DestroyImmediate(arena);
        }
    }

    [Test]
    public void AuthoredSpotUp7sRequiresSevenPointLineThroughSelectionAndLaunch()
    {
        GameModeDefinition mode = GameModeDefinitionFactory.Create(authoredModes.Find(source => source.ModeId == 25));
        LevelDefinition scrapyard = LevelDefinitionFactory.Create(authoredLevels.Find(source => source.LevelId == 1));
        LevelDefinition plainCourt = TestDefinitions.Level(1001, ArenaCapability.Basketball);
        try
        {
            Assert.That(mode, Is.Not.Null);
            Assert.That(mode.RequiredArenaCapabilities.HasFlag(ArenaCapability.SevenPointLine), Is.True);
            GameModeCatalog modes = new GameModeCatalog(new[] { mode });
            LevelDefinitionCatalog levels = new LevelDefinitionCatalog(new[] { plainCourt, scrapyard });
            GameModeCompatibility compatibility = new GameModeCompatibility(modes, levels);
            UnlockSnapshot unlock = new UnlockSnapshot(new Dictionary<int, bool>(),
                new Dictionary<int, bool> { [1001] = true, [1] = true });

            Assert.That(LevelEligibility.CanSelect(plainCourt, mode, compatibility, unlock), Is.False);
            Assert.That(LevelEligibility.CanSelect(scrapyard, mode, compatibility, unlock), Is.True);
            Assert.That(LevelEligibility.NextEligibleLevelIndex(compatibility, mode, 0, 1, unlock), Is.EqualTo(1));
            MatchConfigurationBuilder builder = new MatchConfigurationBuilder(modes, levels, compatibility);
            MatchBuildResult rejected = builder.Build(new MatchRequest(mode.Id, 1001, TestDefinitions.SoloRoster()), unlock);
            Assert.That(rejected.Validation.HasError(MatchValidationCode.ArenaLacksSevenPointLine), Is.True);
            MatchBuildResult accepted = builder.Build(new MatchRequest(mode.Id, 1, TestDefinitions.SoloRoster()), unlock);
            Assert.That(accepted.Succeeded, Is.True, accepted.Validation.ToString());
            Assert.That(accepted.Configuration.Rules.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.SevenPoint));
        }
        finally
        {
            Object.DestroyImmediate(mode);
            Object.DestroyImmediate(scrapyard);
            Object.DestroyImmediate(plainCourt);
        }
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
            Compare(differences, label, "levelSupportsMultiplayer", source.LevelSupportsMultiplayer, level.Supports(ArenaCapability.Multiplayer));

            string expectedScene = source.LevelObjectName + "_" + source.LevelDescription;
            if (!string.IsNullOrEmpty(source.LevelDescription) && level.SceneName != expectedScene)
            {
                differences.AppendLine($"{label}: scene name authored '{expectedScene}', resolved '{level.SceneName}'");
            }
        }

        Assert.That(differences.ToString(), Is.Empty, "authored level data the capability flags cannot reproduce:\n" + differences);
    }

    [Test]
    public void TotalPointsAloneAuthorsAndResolvesTheTwoLocalHumanCapability()
    {
        Assert.That(authoredModes.FindAll(mode => mode.SupportsTwoLocalHumans), Has.Count.EqualTo(1));
        LevelDefinition scrapyard = LevelDefinitionFactory.Create(authoredLevels.Find(level => level.LevelId == 1));
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("first")),
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("second"))
        });
        try
        {
            foreach (StartScreenModeSelected source in authoredModes)
            {
                GameModeDefinition mode = GameModeDefinitionFactory.Create(source);
                try
                {
                    bool certified = mode.Id == GameModeId.TotalPoints;
                    Assert.That(source.SupportsTwoLocalHumans, Is.EqualTo(certified), source.ModeDisplayName);
                    Assert.That(mode.SupportsTwoLocalHumans, Is.EqualTo(certified), source.ModeDisplayName);
                    MatchBuildResult result = new MatchConfigurationBuilder(
                        new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { scrapyard }))
                        .Build(new MatchRequest(mode.Id, scrapyard.LevelId, roster));
                    Assert.That(result.Succeeded, Is.EqualTo(certified), source.ModeDisplayName + ": " + result.Validation);
                    Assert.That(result.Validation.HasError(MatchValidationCode.ModeLocalHumansNotSupported),
                        Is.EqualTo(!certified), source.ModeDisplayName);
                    Assert.That(result.Validation.HasError(MatchValidationCode.ArenaLacksMultiplayer), Is.False);
                }
                finally
                {
                    Object.DestroyImmediate(mode);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(scrapyard);
        }
    }

    [Test]
    public void TwoHumanTotalPointsRequiresACertifiedArenaThroughTheRuntimeFallbackCatalog()
    {
        LegacyMatchCatalogBootstrap.Reset();
        MatchCatalogs.Reset();
        try
        {
            LegacyMatchCatalogBootstrap.EnsureBuilt(authoredModes, authoredLevels);
            Assert.That(MatchCatalogs.Modes.Find(GameModeId.TotalPoints).SupportsTwoLocalHumans, Is.True);
            PlayerRoster roster = PlayerRoster.Build(new[]
            {
                PlayerRosterEntry.LocalHuman(TestDefinitions.Character("first")),
                PlayerRosterEntry.LocalHuman(TestDefinitions.Character("second"))
            });
            foreach (LevelDefinition arena in MatchCatalogs.Levels.Definitions)
            {
                MatchBuildResult result = MatchCatalogs.Builder.Build(new MatchRequest(GameModeId.TotalPoints, arena.LevelId, roster));
                Assert.That(result.Succeeded, Is.EqualTo(arena.LevelId == 1), arena.DisplayName + ": " + result.Validation);
                Assert.That(result.Validation.HasError(MatchValidationCode.ArenaLacksMultiplayer), Is.EqualTo(arena.LevelId != 1));
                Assert.That(result.Validation.HasError(MatchValidationCode.ModeLocalHumansNotSupported), Is.False);
            }
        }
        finally
        {
            LegacyMatchCatalogBootstrap.Reset();
            MatchCatalogs.Reset();
        }
    }

    [Test]
    public void OnlyArenasThatHaveBeenCertifiedForTwoLocalHumansAuthorMultiplayer()
    {
        // Multiplayer claims a spawn topology and a camera that frames it. Adding an arena to this
        // list is a decision that needs the same two-human PlayMode certification the Scrapyard has
        // (Level5TwoHumanLocalInputPlayModeTests) - not a convenience edit to make a menu entry appear.
        HashSet<int> certified = new HashSet<int> { 1 };

        List<string> uncertified = new List<string>();
        foreach (LevelSelected source in authoredLevels)
        {
            if (source.LevelSupportsMultiplayer && !certified.Contains(source.LevelId))
            {
                uncertified.Add($"{source.LevelDisplayName} (id {source.LevelId}) authors multiplayer without a two-human certification");
            }
        }

        Assert.That(uncertified, Is.Empty, string.Join("\n", uncertified));

        foreach (int id in certified)
        {
            LevelSelected source = authoredLevels.Find(level => level.LevelId == id);
            Assert.That(source, Is.Not.Null, $"certified arena {id} is not authored");
            Assert.That(source.LevelSupportsMultiplayer, Is.True, $"certified arena {id} must author multiplayer");
        }
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
