using System;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Utility;
using Level5.Core;
using Level5.Core.Match;
using Random = UnityEngine.Random;

/// <summary>
/// AUD-017: the shot-launch modifier computation and the score/profile text formatting were
/// byte-identical between <see cref="BasketBall"/> (human) and <see cref="BasketBallAuto"/> (CPU),
/// apart from which controller supplied the shot-meter slider value. Extracted here so a fix only
/// has to be made once, the same reasoning that already moved the three accuracy-modifier formulas
/// into <see cref="ShotModifiers"/>.
///
/// <c>shootBasketBall</c>'s marker/money-ball block (present on the human path, commented out on
/// the CPU path) was originally left alone here as already-diverged behavior needing a product
/// decision. 2026-08-13: confirmed CPU shots should carry marker/money-ball credit the same as
/// human shots, so <see cref="ApplyMarkerAndMoneyBallOnShoot"/> now covers it too - built from
/// <c>BasketBall</c>'s current live block, not <c>BasketBallAuto</c>'s stale commented copy, which
/// had already drifted (missing the seven-point-contest clause).
///
/// Still deliberately not touched: <c>LaunchBasketBall</c>'s shot-meter wait condition (the two
/// files wait on opposite <c>MeterEnded</c> values) - confirmed intentional, left as-is.
/// </summary>
public static class BasketballShotPipeline
{
    /// <summary>
    /// Credits the current shot toward its marker's attempt count and money-ball bonus when the
    /// shooter is standing on an enabled marker in a mode that requires them. Mirrors
    /// <c>BasketBall.shootBasketBall</c>'s original block; both human and CPU shots call this now.
    ///
    /// AUD-010 Phase 1c: takes the shooter's own <see cref="IBasketballRuntime"/> instead of a bare
    /// <c>BasketBallState</c>/<c>GameStats</c> pair, so the marker this shot is credited to is the
    /// exact <see cref="BasketBallState.CurrentShotMarker"/> reference this participant is standing
    /// on - never an id resolved back through <c>GameRules.BasketBallShotMarkersList</c>.
    ///
    /// AUD-010 Phase 1c: the marker-required gate now reads <see cref="ResolvedMatchRules.RequiresAnyShotMarkers"/>
    /// from one <c>MatchRuntime.Rules</c> snapshot for the whole call, rather than
    /// <c>GameRules.instance.PositionMarkersRequired</c> - a compatibility cache <c>GameRules.Start</c>
    /// copies from the same immutable rule once, before match play can reach a shot. See
    /// docs/shot-lifecycle.md. <c>GameRules.instance.MoneyBallEnabled</c> stays untouched: it is
    /// separately mutable session state, not an immutable match rule.
    /// </summary>
    public static void ApplyMarkerAndMoneyBallOnShoot(IBasketballRuntime runtime)
    {
        BasketBallState basketBallState = runtime.State;
        GameStats gameStats = runtime.Stats;
        ResolvedMatchRules rules = MatchRuntime.Rules;

        if (!basketBallState.PlayerOnMarker || !rules.RequiresAnyShotMarkers)
        {
            return;
        }

        if (basketBallState.CurrentShotMarker == null)
        {
            // PlayerOnMarker is only ever set true alongside a marker reference by
            // BasketBallState.EnterShotMarker, so this is an ownership/composition bug, not a
            // reachable gameplay state - never guess marker zero.
            Debug.LogError($"BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot: participant {runtime.ParticipantId} has PlayerOnMarker true but no CurrentShotMarker - skipping marker accounting for this shot.");
            return;
        }

        basketBallState.CaptureShotMarkerForAttempt();
        BasketBallShotMarker marker = basketBallState.OnShootShotMarker;
        marker.RegisterAttempt(runtime);

        if (marker.ShotAttempt == 5
            && (rules.IsThreePointContest || rules.IsFourPointContest || rules.IsSevenPointContest))
        {
            gameStats.Stats.MoneyBallAttempts++;
        }

        if (GameRules.instance.MoneyBallEnabled)
        {
            basketBallState.MoneyBallEnabledOnShoot = true;
            gameStats.Stats.MoneyBallAttempts++;
        }
    }

    public struct LaunchComputation
    {
        public Vector3 GlobalVelocity;
        public bool Critical;
        public bool IsSwish;
        public string ShotMeterMessage;
    }

    /// <summary>
    /// Everything the original <c>Launch()</c> computed before touching the Rigidbody: the
    /// critical/accuracy/range/release rolls, the shot-meter message, and the resulting launch
    /// velocity in world space. Rolls happen in the same order the original did - direction is
    /// drawn before the release roll, and the in-range check short-circuits before any range roll -
    /// because swapping either shifts the whole random sequence for the rest of the shot.
    ///
    /// Phase 1c of the systems restructure: takes <see cref="ShooterAttributes"/> rather than a
    /// <c>CharacterProfile</c>, the first consumer migrated onto the Phase 1a contract. Callers
    /// build it once via the actor's <see cref="IShooterActor.ShooterAttributes"/> (backed by
    /// <c>ShooterAttributesMapper.From</c> on the player side since the player↔basketball cycle-cut
    /// slice).
    /// </summary>
    public static LaunchComputation ComputeLaunch(
        Transform ballTransform,
        Vector3 ballPositionAtLaunch,
        Vector3 targetPosition,
        ShooterAttributes shooter,
        BasketBallState basketBallState,
        GameStats gameStats,
        float lastShotDistance,
        float shotMeterSliderValue)
    {
        // rotate the object to face the target
        ballTransform.LookAt(targetPosition);

        // shorthands for the formula
        float R = Vector3.Distance(ballPositionAtLaunch, targetPosition);
        float G = Physics.gravity.y;
        float tanAlpha;
        // check last shot distance. if > 500, angle = 55 degrees. almost impossible to make shot
        // >500ft with shoot angle 45-52 that most characters have
        if (lastShotDistance >= 500)
        {
            tanAlpha = Mathf.Tan(55 * Mathf.Deg2Rad);
        }
        else
        {
            tanAlpha = Mathf.Tan(shooter.ShootAngle * Mathf.Deg2Rad);
        }
        float H = targetPosition.y - ballPositionAtLaunch.y;
        // Code review: a degenerate ShootAngle (0, from ShooterAttributesMapper's zeroed fallback
        // when a shooter has no CharacterProfile) can make this radicand negative - G is gravity,
        // always negative, and a zero tanAlpha leaves the denominator at the ordinary sign for an
        // above-release target. Sqrt of a negative number is NaN, not an exception, and it would
        // flow straight into the Rigidbody's velocity. Clamped rather than left to propagate, so a
        // malformed shooter produces a shot that goes nowhere instead of corrupting physics state.
        float Vz = Mathf.Sqrt(Mathf.Max(0f, G * R * R / (2.0f * (H - R * tanAlpha))));
        float Vy = tanAlpha * Vz;

        bool critical = RollForCriticalShotChance(shooter.Luck, gameStats);

        float accuracyModifierX = 0f;
        float accuracyModifierY = 0f;
        float accuracyModifierZ;

        string shotMeterMessage = "";
        string shotMeterMessageX = "";
        string shotMeterMessageY = "";
        string shotMeterMessageZ = "";

        // if rolled critical
        if (critical)
        {
            accuracyModifierX = 0;
            accuracyModifierY = 0;
            shotMeterMessage = "critical";
        }
        // if >= 95 and NOT critical (release stat factored in)
        if (shotMeterSliderValue >= 95 && !critical)
        {
            accuracyModifierX = 0;
            accuracyModifierY = GetReleaseModifier(shooter);
            accuracyModifierZ = 0;
            shotMeterMessage = ">= 95";
            shotMeterMessageY = "+ release modifier";
        }
        // NOT critical and NOT >= 95 (get X, Y modifiers)
        if (shotMeterSliderValue < 95 && !critical)
        {
            accuracyModifierX = GetAccuracyModifier(shooter, basketBallState, shotMeterSliderValue);
            accuracyModifierY = GetReleaseModifier(shooter);

            shotMeterMessage = "< 95";
            shotMeterMessageX = "+ accuracy modifier";
            shotMeterMessageY = "+ release modifier";
        }

        // range modifier always factors in
        accuracyModifierZ = GetRangeModifier(shooter, lastShotDistance);

        if (accuracyModifierZ != 0)
        {
            shotMeterMessageZ = "+ range modifer";
        }

        // set shot meter message
        if (shotMeterMessage != null)
        {
            shotMeterMessage = shotMeterMessage + "\n" + shotMeterMessageX + "\n" + shotMeterMessageY + "\n" + shotMeterMessageZ;
        }
        else
        {
            shotMeterMessage = shotMeterMessageX + "\n" + shotMeterMessageY + "\n" + shotMeterMessageZ;
        }

        bool isSwish = accuracyModifierX == 0 && accuracyModifierY == 0 && accuracyModifierZ == 0;
        if (isSwish)
        {
            shotMeterMessage = critical ? "swish + critical" : "swish";
        }

        float xVector = 0 + accuracyModifierX;
        float yVector = Vy + accuracyModifierY;
        float zVector = Vz - accuracyModifierZ;

        Vector3 localVelocity = new Vector3(xVector, yVector, zVector);
        Vector3 globalVelocity = ballTransform.TransformDirection(localVelocity);

        return new LaunchComputation
        {
            GlobalVelocity = globalVelocity,
            Critical = critical,
            IsSwish = isSwish,
            ShotMeterMessage = shotMeterMessage,
        };
    }

    // ========================== shot accuracy functions ==========================================
    // all three roll a plain percentage chance through the shared helper, so a 0 stat
    // never succeeds and a 100 stat always does.
    private static bool RollForCriticalShotChance(float maxPercent, GameStats gameStats)
    {
        if (UtilityFunctions.RollPercent(maxPercent))
        {
            gameStats.Stats.CriticalRolled++;
            return true;
        }
        return false;
    }

    private static float GetAccuracyModifier(ShooterAttributes shooter, BasketBallState basketBallState, float shotMeterSliderValue)
    {
        // drawn first, as the original did
        int direction = GetRandomPositiveOrNegative();
        ResolveShotAccuracy(basketBallState, shooter, out float shotTypeAccuracy, out bool threePoints);

        return ShotModifiers.AccuracyModifier(shotMeterSliderValue, shotTypeAccuracy, threePoints, direction);
    }

    /// <summary>
    /// Picks the accuracy stat for the shot being taken.
    ///
    /// Phase 1c: defers to <see cref="ShooterAttributes.AccuracyFor"/> and
    /// <see cref="ShooterAttributes.IsThreePointBranch"/>, which mirror the precedence this method
    /// used to compute inline (see their doc comments for the preserved oddities - the reversed
    /// precedence order and the no-flag-set case returning 100, not two-point accuracy). One
    /// implementation now, not two that could drift apart.
    /// </summary>
    private static void ResolveShotAccuracy(BasketBallState basketBallState, ShooterAttributes shooter, out float shotTypeAccuracy, out bool threePoints)
    {
        ShotKind kind = KindFromPointFlags(basketBallState);
        shotTypeAccuracy = shooter.AccuracyFor(kind);
        threePoints = ShooterAttributes.IsThreePointBranch(kind);
    }

    /// <summary>
    /// The shot kind the launch pipeline's if/else chain would select, read from the *point* flags.
    ///
    /// Player↔basketball cycle-cut slice: inlined from the deleted <c>ShooterAttributesFactory</c>,
    /// which this was the only caller of. Deliberately not named <c>ShotKindOf</c>, because
    /// <c>BasketBallShotMade.ShotKindOf</c> already exists and answers a different question with the
    /// opposite precedence. That one reads the *attempt* flags (TwoAttempt..SevenAttempt) ascending,
    /// and can, because by the time a make is registered they are mutually exclusive. These point
    /// flags are not mutually exclusive at launch time, so precedence runs seven, four, three, two and
    /// the highest wins.
    /// </summary>
    public static ShotKind KindFromPointFlags(BasketBallState basketBallState)
    {
        if (basketBallState == null)
        {
            Debug.LogWarning("BasketballShotPipeline.KindFromPointFlags: no BasketBallState - resolving to ShotKind.None.");
            return ShotKind.None;
        }

        if (basketBallState.SevenPoints)
        {
            return ShotKind.Seven;
        }

        if (basketBallState.FourPoints)
        {
            return ShotKind.Four;
        }

        if (basketBallState.ThreePoints)
        {
            return ShotKind.Three;
        }

        if (basketBallState.TwoPoints)
        {
            return ShotKind.Two;
        }

        return ShotKind.None;
    }

    private static float GetRangeModifier(ShooterAttributes shooter, float lastShotDistance)
    {
        // range divided by distance to get %
        // ex. range 50 ft / shot distance 100 = 50% chance of reaching rim
        // the in-range check comes first and returns without rolling - the original's `||`
        // short-circuited, so an in-range shot must not consume a random value
        if (ShotModifiers.ReachesRim(shooter.Range, lastShotDistance))
        {
            return 0f;
        }

        bool rolledClean = UtilityFunctions.RollPercent(
            ShotModifiers.MaxCleanChance(shooter.Range, lastShotDistance));

        return ShotModifiers.RangeModifier(shooter.Range, lastShotDistance, rolledClean);
    }

    private static float GetReleaseModifier(ShooterAttributes shooter)
    {
        // direction is drawn before the roll, matching the original's order - swapping them would
        // shift every subsequent random value
        int direction = GetRandomPositiveOrNegative();

        // the release stat IS the chance to shoot clean.
        // ex if release = 85, 85% chance to remove the modifier entirely.
        bool rolledClean = UtilityFunctions.RollPercent(shooter.Release);

        return ShotModifiers.ReleaseModifier(shooter.Release, direction, rolledClean);
    }

    private static int GetRandomPositiveOrNegative()
    {
        return Random.value < 0.5f ? 1 : -1;
    }

    // ========================== ui text formatting ==========================================

    /// <summary>
    /// Phase 1c: second consumer migrated onto <see cref="ShooterAttributes"/>. Exercises every
    /// field of the contract, including RunSpeed, JumpForce and DisplayName that
    /// <see cref="ComputeLaunch"/>'s arithmetic does not touch.
    /// </summary>
    public static void UpdateShooterProfileText(Text shootProfileText, ShooterAttributes shooter)
    {
        shootProfileText.text = shooter.DisplayName + "\n"
                                + "2 point : " + (shooter.AccuracyTwoPoint) + "\n"
                                + "3 point : " + (shooter.AccuracyThreePoint) + "\n"
                                + "4 point : " + (shooter.AccuracyFourPoint) + "\n"
                                + "7 point : " + (shooter.AccuracySevenPoint) + "\n"
                                + "release : " + shooter.Release + "\n"
                                + "range : " + shooter.Range + "\n"
                                + "speed : " + shooter.RunSpeed + "\n"
                                + "jump : " + shooter.JumpForce + "\n"
                                + "luck : " + shooter.Luck;
    }

    /// <summary>
    /// Phase 1c: reads counters through <see cref="GameStats.Stats"/> rather than the facade's
    /// passthrough properties. Still takes <c>GameStats</c> itself, not just <c>MatchStats</c> -
    /// <see cref="GameStats.getExperienceGainedFromSession"/> reads <c>MatchRuntime</c>, which
    /// <c>MatchStats</c> cannot reach across the assembly boundary, so that one call stays on the
    /// facade.
    /// </summary>
    public static void UpdateScoreText(Text scoreText, GameStats gameStats, float lastShotDistance)
    {
        MatchStats stats = gameStats.Stats;
        scoreText.text = "shots  : " + stats.ShotMade + " / " + stats.ShotAttempt + "  " +
                         stats.TotalPointAccuracy.ToString("0.00") + "\n"
                         + "points : " + stats.TotalPoints + "\n"
                         + "2 pointers : " + stats.TwoPointerMade + " / " +
                         stats.TwoPointerAttempts + "  " + GetPercentage(stats.TwoPointerMade, stats.TwoPointerAttempts).ToString("0.00") + "%\n"
                         + "3 pointers : " + stats.ThreePointerMade + " / " +
                         stats.ThreePointerAttempts + "  " + GetPercentage(stats.ThreePointerMade, stats.ThreePointerAttempts).ToString("0.00") + "%\n"
                         + "4 pointers : " + stats.FourPointerMade + " / " +
                         stats.FourPointerAttempts + "  : " + GetPercentage(stats.FourPointerMade, stats.FourPointerAttempts).ToString("0.00") + "%\n"
                         + "7 pointers : " + stats.SevenPointerMade + " / " +
                         stats.SevenPointerAttempts + "  " + GetPercentage(stats.SevenPointerMade, stats.SevenPointerAttempts).ToString("0.00") + "%\n"
                         + "last shot distance : " + (Math.Round(lastShotDistance, 2) * 6f).ToString("0.00") + " ft." +
                         "\n"
                         + "longest shot distance : " +
                         (Math.Round(stats.LongestShotMade, 2)).ToString("0.00") + " ft." + "\n" +
                         "criticals rolled : " + stats.CriticalRolled + " / " + stats.ShotAttempt
                         + "  " + GetPercentage(stats.CriticalRolled, stats.ShotAttempt).ToString("0.00") + "%\n"
                         + "consecutive shots made : " + stats.ConsecutiveShotsMade + "\n"
                         + "current exp : " + gameStats.getExperienceGainedFromSession();
    }

    // * NOTE : cast to float has to be (float) num1 / num2 to work;
    //  this format will not work for some reason -- (float)(num1 / num2 to work);
    private static float GetPercentage(int made, int attempts)
    {
        if (attempts <= 0)
        {
            return 0f;
        }

        float accuracy = (float)made / attempts;
        return accuracy * 100;
    }
}
