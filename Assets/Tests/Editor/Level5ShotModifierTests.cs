using NUnit.Framework;

/// <summary>
/// AUD-017: characterization tests. These pin the shot-accuracy math exactly as it behaved when it
/// lived duplicated inside `BasketBall` and `BasketBallAuto`, so the extraction into
/// <see cref="ShotModifiers"/> can be shown not to have changed shot feel.
///
/// Written deliberately *before* either basketball script was changed to delegate. This code has
/// already produced two scoring-integrity bugs (AUD-015, AUD-016); deduplicating it without a
/// safety net is how a third would happen.
///
/// Expected values below were computed by hand from the original expressions, not by running the
/// new code and recording what it printed - a test that only asserts "it does what it does" would
/// pin a regression just as happily as correct behaviour.
/// </summary>
public class Level5ShotModifierTests
{
    private const float Tolerance = 0.0001f;

    // ---------- accuracy modifier ----------

    [Test]
    public void APerfectSliderRemovesAllAimError()
    {
        // slider 100 -> sliderModifier = (100-100)*0.025 = 0, and the accuracy term is *scaled by*
        // the slider term rather than added, so it vanishes too - at any accuracy.
        Assert.That(ShotModifiers.AccuracyModifier(100f, 0f, false, 1), Is.EqualTo(0f).Within(Tolerance));
        Assert.That(ShotModifiers.AccuracyModifier(100f, 100f, true, -1), Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void PerfectAccuracyStillLeavesSliderError()
    {
        // accuracy 100 -> accuracyModifier 0, leaving sliderModifier alone.
        // slider 60 -> (100-60)*0.025 = 1.0
        Assert.That(ShotModifiers.AccuracyModifier(60f, 100f, false, 1), Is.EqualTo(1.0f).Within(Tolerance));
    }

    [Test]
    public void AccuracyShortfallCompoundsWithSliderError()
    {
        // slider 60 -> sliderModifier 1.0
        // accuracy 50, non-three -> (100-50)*0.01 = 0.5
        // (1.0 + 0.5*1.0) * 1 = 1.5
        Assert.That(ShotModifiers.AccuracyModifier(60f, 50f, false, 1), Is.EqualTo(1.5f).Within(Tolerance));
    }

    [Test]
    public void ThreePointersArePunishedTwiceAsHardForTheSameShortfall()
    {
        // This asymmetry (0.02 vs 0.01) looks like a typo but both original copies agreed on it,
        // so it is long-standing behaviour. Pinned so a future "cleanup" has to be deliberate.
        float twoPoint = ShotModifiers.AccuracyModifier(60f, 50f, false, 1);
        float threePoint = ShotModifiers.AccuracyModifier(60f, 50f, true, 1);

        // slider 1.0; two-point accuracy term 0.5 -> 1.5; three-point 1.0 -> 2.0
        Assert.That(twoPoint, Is.EqualTo(1.5f).Within(Tolerance));
        Assert.That(threePoint, Is.EqualTo(2.0f).Within(Tolerance));
        Assert.That(ShotModifiers.AccuracyMultiplierFor(true), Is.EqualTo(0.02f));
        Assert.That(ShotModifiers.AccuracyMultiplierFor(false), Is.EqualTo(0.01f));
    }

    [Test]
    public void DirectionOnlyMirrorsTheResult()
    {
        float right = ShotModifiers.AccuracyModifier(60f, 50f, false, 1);
        float left = ShotModifiers.AccuracyModifier(60f, 50f, false, -1);

        Assert.That(left, Is.EqualTo(-right).Within(Tolerance));
    }

    [Test]
    public void SliderIsCeilingedNotRounded()
    {
        // the original used Mathf.CeilToInt, so 59.1 and 60 behave identically
        Assert.That(
            ShotModifiers.AccuracyModifier(59.1f, 100f, false, 1),
            Is.EqualTo(ShotModifiers.AccuracyModifier(60f, 100f, false, 1)).Within(Tolerance));

        // and 59.0 does not
        Assert.That(
            ShotModifiers.AccuracyModifier(59f, 100f, false, 1),
            Is.Not.EqualTo(ShotModifiers.AccuracyModifier(60f, 100f, false, 1)).Within(Tolerance));
    }

    // ---------- range modifier ----------

    [Test]
    public void AShotWithinRangeHasNoPenalty()
    {
        // range 60, distance 10 world units -> 10*6 = 60 ft, modifier = 1.0, which is >= 1
        Assert.That(ShotModifiers.RangeModifier(60f, 10f, false), Is.EqualTo(0f).Within(Tolerance));
        // comfortably within range
        Assert.That(ShotModifiers.RangeModifier(120f, 10f, false), Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void AShotBeyondRangeFallsShortByTheReachableFraction()
    {
        // range 30, distance 10 -> 60 ft needed, reaches half
        Assert.That(ShotModifiers.RangeModifier(30f, 10f, false), Is.EqualTo(0.5f).Within(Tolerance));
    }

    [Test]
    public void WinningTheCleanRollCancelsTheRangePenalty()
    {
        Assert.That(ShotModifiers.RangeModifier(30f, 10f, true), Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void CleanChanceIsTheReachableFractionAsAPercentage()
    {
        // the caller rolls this; half-reachable means a 50% chance to shoot clean anyway
        Assert.That(ShotModifiers.MaxCleanChance(30f, 10f), Is.EqualTo(50f).Within(Tolerance));
        Assert.That(ShotModifiers.MaxCleanChance(60f, 10f), Is.EqualTo(100f).Within(Tolerance));
    }

    // ---------- release modifier ----------

    [Test]
    public void WinningTheReleaseRollShootsClean()
    {
        // the release stat IS the chance to shoot clean - AUD-030 corrected the comment that
        // claimed the inverse, and the code was always right
        Assert.That(ShotModifiers.ReleaseModifier(85f, 1, true), Is.EqualTo(0f).Within(Tolerance));
        Assert.That(ShotModifiers.ReleaseModifier(0f, -1, true), Is.EqualTo(0f).Within(Tolerance));
    }

    [Test]
    public void LosingTheReleaseRollScalesTheShortfallByThreeQuarters()
    {
        // release 20 -> (100-20)*0.01 = 0.8, * 0.75 = 0.6
        Assert.That(ShotModifiers.ReleaseModifier(20f, 1, false), Is.EqualTo(0.6f).Within(Tolerance));
        Assert.That(ShotModifiers.ReleaseModifier(20f, -1, false), Is.EqualTo(-0.6f).Within(Tolerance));
    }

    [Test]
    public void APerfectReleaseLeavesNoErrorEvenOnALostRoll()
    {
        // release 100 -> (100-100)*0.01 = 0. Unreachable in practice because a 100 stat always
        // wins the roll, but the arithmetic should not depend on that.
        Assert.That(ShotModifiers.ReleaseModifier(100f, 1, false), Is.EqualTo(0f).Within(Tolerance));
    }
}
