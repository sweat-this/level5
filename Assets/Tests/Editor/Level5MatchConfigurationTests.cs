using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// The configuration boundary: what the builder resolves, and that it refuses what it should.
///
/// The resolution tests are the important half. Each one covers a decision that used to be made
/// somewhere downstream, at a point where changing it meant the scene was editing the rules of the
/// match it was already running.
/// </summary>
public class Level5MatchConfigurationTests
{
    private static MatchConfigurationBuilder BuilderWith(GameModeDefinition mode, LevelDefinition level)
    {
        return new MatchConfigurationBuilder(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { level }));
    }

    [Test]
    public void AValidRequestProducesAConfiguration()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(4, objectName: "level_04_park", sceneDescriptor: "day");

        MatchBuildResult result = BuilderWith(mode, level).Build(
            new MatchRequest(GameModeId.TotalPoints, 4, TestDefinitions.SoloRoster()));

        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        Assert.That(result.Configuration.ModeId, Is.EqualTo(GameModeId.TotalPoints));
        Assert.That(result.Configuration.LevelId, Is.EqualTo(4));
        Assert.That(result.Configuration.SceneName, Is.EqualTo("level_04_park_day"));
    }

    [Test]
    public void AnUnknownModeOrLevelIsRejectedWithAReason()
    {
        MatchConfigurationBuilder builder = BuilderWith(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(1));

        MatchBuildResult missingMode = builder.Build(
            new MatchRequest(GameModeId.CageMatch, 1, TestDefinitions.SoloRoster()));
        MatchBuildResult missingLevel = builder.Build(
            new MatchRequest(GameModeId.TotalPoints, 99, TestDefinitions.SoloRoster()));

        Assert.That(missingMode.Succeeded, Is.False);
        Assert.That(missingMode.Validation.HasError(MatchValidationCode.UnknownMode), Is.True);
        Assert.That(missingLevel.Succeeded, Is.False);
        Assert.That(missingLevel.Validation.HasError(MatchValidationCode.UnknownLevel), Is.True);
    }

    [Test]
    public void AnEmptyRosterIsRejected()
    {
        MatchBuildResult result = BuilderWith(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(1))
            .Build(new MatchRequest(GameModeId.TotalPoints, 1, PlayerRoster.Build(null)));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Validation.HasError(MatchValidationCode.RosterEmpty), Is.True);
    }

    [Test]
    public void AModeWithoutACustomTimerGetsTheDefaultMatchLength()
    {
        ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(1),
            TestDefinitions.SoloRoster(),
            MatchModifiers.Default);

        Assert.That(rules.CustomTimerSeconds, Is.EqualTo(0f));
        Assert.That(rules.MatchLengthSeconds, Is.EqualTo(MatchClock.DefaultMatchSeconds));
    }

    [Test]
    public void ACustomTimerWinsOverTheDefault()
    {
        ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.ThreePointContest, customTimerSeconds: 80f),
            TestDefinitions.Level(1),
            TestDefinitions.SoloRoster(),
            MatchModifiers.Default);

        Assert.That(rules.MatchLengthSeconds, Is.EqualTo(80f));
    }

    [Test]
    public void AFightingModeResolvesEnemiesOnEvenWhenNobodyAskedForThem()
    {
        // GameLevelManager.Awake used to do this at scene start, after the menu had settled it.
        ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(
                GameModeId.BashUpSomeNerds,
                combatMode: CombatMode.Standard,
                enemiesOnly: true,
                requiresBasketball: false),
            TestDefinitions.Level(1, ArenaCapability.Combat),
            TestDefinitions.SoloRoster(),
            MatchModifiers.Default);

        Assert.That(rules.EnemiesEnabled, Is.True);
    }

    [Test]
    public void TrafficResolvesOffOnAnArenaThatHasNone()
    {
        // StartManager used to overwrite the player's choice on the way out; now it is a resolution
        // the configuration records, so nothing downstream has to ask why the setting did nothing.
        ResolvedMatchRules withTraffic = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(1, ArenaCapability.Basketball | ArenaCapability.Traffic),
            TestDefinitions.SoloRoster(),
            new MatchModifiers(trafficRequested: true));

        ResolvedMatchRules withoutTraffic = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(2, ArenaCapability.Basketball),
            TestDefinitions.SoloRoster(),
            new MatchModifiers(trafficRequested: true));

        Assert.That(withTraffic.TrafficEnabled, Is.True);
        Assert.That(withoutTraffic.TrafficEnabled, Is.False);
    }

    [Test]
    public void OneBallPerParticipantExceptWhereTheModePinsIt()
    {
        PlayerRoster pair = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("me")),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("them"))
        });

        ResolvedMatchRules versus = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.VersusCpu),
            TestDefinitions.Level(1),
            pair,
            MatchModifiers.Default);

        ResolvedMatchRules lockdown = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.Lockdown, addsImplicitDefender: true),
            TestDefinitions.Level(1),
            TestDefinitions.SoloRoster(),
            MatchModifiers.Default);

        Assert.That(versus.BasketballCount, Is.EqualTo(2));
        Assert.That(lockdown.BasketballCount, Is.EqualTo(1));
    }

    [Test]
    public void AModeWithNoBallHasEnemiesEvenWithoutBeingAFightingMode()
    {
        // Faithful to "if basketball doesn't exist, enable enemies", which is what the level
        // manager did on the way in.
        ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.TotalPoints, requiresBasketball: false),
            TestDefinitions.Level(1),
            TestDefinitions.SoloRoster(),
            MatchModifiers.Default);

        Assert.That(rules.EnemiesEnabled, Is.True);
    }

    [Test]
    public void ASingleContestVariantSetsOnlyItsOwnFlag()
    {
        // Contest ranges are a set - the authored all-point contest legitimately holds three of
        // them - but a mode that names one range must not read as any of the others.

        ResolvedMatchRules rules = MatchConfigurationBuilder.Resolve(
            TestDefinitions.Mode(GameModeId.FourPointContest, shotRule: ShotRule.FourPoint),
            TestDefinitions.Level(1),
            TestDefinitions.SoloRoster(),
            MatchModifiers.Default);

        Assert.That(rules.IsFourPointContest, Is.True);
        Assert.That(rules.IsThreePointContest, Is.False);
        Assert.That(rules.IsSevenPointContest, Is.False);
        Assert.That(rules.IsAllPointContest, Is.False);
    }
}
