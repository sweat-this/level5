using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// Proves player select and full roster validation agree on character capability, because they
/// both call <see cref="GameModeCompatibility.CharacterCanPlay"/> - there is exactly one
/// fighter/shooter rule in the codebase, not two that could drift.
/// </summary>
public class Level5PlayerSelectCompatibilityParityTests
{
    [Test]
    public void ShooterCanPlayAShootingModeInBothQueryAndFullValidation()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        CharacterSelection shooter = TestDefinitions.Character("shooter", isShooter: true, isFighter: false);

        Assert.That(GameModeCompatibility.CharacterCanPlay(mode, MatchModifiers.Default, shooter), Is.True);

        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { TestDefinitions.Level(1) }));
        ValidationResult verdict = compatibility.Validate(new MatchRequest(
            mode.Id,
            1,
            PlayerRoster.SingleLocalHuman(shooter)));

        Assert.That(verdict.HasError(MatchValidationCode.CharacterCannotShoot), Is.False);
    }

    [Test]
    public void FighterCannotPlayAShootingModeInEitherQueryOrFullValidation()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        CharacterSelection fighterOnly = TestDefinitions.Character("fighter", isShooter: false, isFighter: true);

        Assert.That(GameModeCompatibility.CharacterCanPlay(mode, MatchModifiers.Default, fighterOnly), Is.False);

        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { TestDefinitions.Level(1) }));
        ValidationResult verdict = compatibility.Validate(new MatchRequest(
            mode.Id,
            1,
            PlayerRoster.SingleLocalHuman(fighterOnly)));

        Assert.That(verdict.HasError(MatchValidationCode.CharacterCannotShoot), Is.True);
    }

    [Test]
    public void EnemiesModifierMakesAShootingModeRequireAFighterInBothQueryAndFullValidation()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        MatchModifiers enemiesOn = MatchModifiers.Default.With(enemies: true);
        CharacterSelection shooterOnly = TestDefinitions.Character("shooter", isShooter: true, isFighter: false);

        Assert.That(GameModeCompatibility.CharacterCanPlay(mode, enemiesOn, shooterOnly), Is.False);

        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { TestDefinitions.Level(1) }));
        ValidationResult verdict = compatibility.Validate(new MatchRequest(
            mode.Id,
            1,
            PlayerRoster.SingleLocalHuman(shooterOnly),
            enemiesOn));

        Assert.That(verdict.HasError(MatchValidationCode.CharacterCannotFight), Is.True);
    }

    [Test]
    public void AnEmptyCharacterIsReportedPlayableByTheQueryAlone()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);

        Assert.That(GameModeCompatibility.CharacterCanPlay(mode, MatchModifiers.Default, CharacterSelection.None), Is.True);
    }
}
