using Level5.Core;
using NUnit.Framework;

/// <summary>
/// Phase 1a of the systems restructure: the inbound half of the shot seam.
///
/// <see cref="ShooterAttributes"/> exists so the shot pipeline can be handed a shooter's numbers
/// instead of reaching into <c>CharacterProfile</c> - the coupling that, with the `game manager`
/// edge, forms the cycle pinning `Assets/Scripts` into one assembly.
///
/// These lock the accuracy selection to what <c>BasketballShotPipeline.ResolveShotAccuracy</c>
/// does today, because Phase 1c migrates consumers onto it one at a time and any drift here would
/// change how shots score without changing any call site.
/// </summary>
public class Level5ShooterAttributesTests
{
    private static ShooterAttributes Attributes()
    {
        return new ShooterAttributes(
            displayName: "tester",
            accuracyTwoPoint: 2f,
            accuracyThreePoint: 3f,
            accuracyFourPoint: 4f,
            accuracySevenPoint: 7f,
            shootAngle: 48,
            range: 55,
            release: 60,
            luck: 10,
            jumpForce: 4.5f,
            runSpeed: 9f);
    }

    [TestCase(ShotKind.Two, 2f)]
    [TestCase(ShotKind.Three, 3f)]
    [TestCase(ShotKind.Four, 4f)]
    [TestCase(ShotKind.Seven, 7f)]
    public void EachShotKindSelectsItsOwnAccuracy(ShotKind kind, float expected)
    {
        Assert.That(Attributes().AccuracyFor(kind), Is.EqualTo(expected));
    }

    /// <summary>
    /// The preserved oddity. `ResolveShotAccuracy` ends `else { shotTypeAccuracy = 100f; }` because
    /// the original left the accuracy term at 0 when no flag was set, and 100 reproduces that.
    /// Returning two-point accuracy here would read as tidier and would silently change the shot.
    /// </summary>
    [Test]
    public void NoShotKindUsesTheNeutralAccuracyRatherThanTwoPoint()
    {
        ShooterAttributes attributes = Attributes();

        Assert.That(attributes.AccuracyFor(ShotKind.None), Is.EqualTo(ShooterAttributes.NoShotKindAccuracy));
        Assert.That(
            attributes.AccuracyFor(ShotKind.None),
            Is.Not.EqualTo(attributes.AccuracyTwoPoint),
            "if these ever coincide the test stops proving anything - pick different fixture values");
    }

    /// <summary>
    /// Precedence is seven, four, three, two. The BasketBallState flags are not mutually exclusive,
    /// so a shot flagged both seven and three has to resolve as seven, the way the if/else chain
    /// does.
    /// </summary>
    [Test]
    public void HigherShotKindsOutrankLowerOnes()
    {
        ShooterAttributes attributes = Attributes();

        Assert.That(attributes.AccuracyFor(ShotKind.Seven), Is.GreaterThan(attributes.AccuracyFor(ShotKind.Four)));
        Assert.That(attributes.AccuracyFor(ShotKind.Four), Is.GreaterThan(attributes.AccuracyFor(ShotKind.Three)));
        Assert.That(attributes.AccuracyFor(ShotKind.Three), Is.GreaterThan(attributes.AccuracyFor(ShotKind.Two)));
    }

    /// <summary>
    /// The contract has to carry every member the pipeline reads off CharacterProfile, or migrating
    /// a consumer in Phase 1c would need the profile anyway and the cycle would survive.
    /// </summary>
    [Test]
    public void TheContractCarriesEveryAttributeThePipelineReads()
    {
        ShooterAttributes attributes = Attributes();

        Assert.That(attributes.DisplayName, Is.EqualTo("tester"));
        Assert.That(attributes.ShootAngle, Is.EqualTo(48));
        Assert.That(attributes.Range, Is.EqualTo(55));
        Assert.That(attributes.Release, Is.EqualTo(60));
        Assert.That(attributes.Luck, Is.EqualTo(10));
        Assert.That(attributes.JumpForce, Is.EqualTo(4.5f));
        Assert.That(attributes.RunSpeed, Is.EqualTo(9f));
    }

    /// <summary>A default instance must be inert rather than throwing - see ShooterAttributesFactory.</summary>
    [Test]
    public void ADefaultInstanceIsInert()
    {
        ShooterAttributes attributes = default;

        Assert.That(attributes.AccuracyFor(ShotKind.Two), Is.EqualTo(0f));
        Assert.That(attributes.AccuracyFor(ShotKind.None), Is.EqualTo(ShooterAttributes.NoShotKindAccuracy));
        Assert.That(attributes.DisplayName, Is.Null);
    }
}
