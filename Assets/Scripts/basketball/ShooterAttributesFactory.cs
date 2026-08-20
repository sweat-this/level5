using Level5.Core;
using UnityEngine;

/// <summary>
/// Reads a shooter's numbers off the scene component into a plain value.
///
/// Phase 1a of the systems restructure. This is deliberately the only place that knows a
/// <see cref="ShooterAttributes"/> comes from a <see cref="CharacterProfile"/>. The contract lives in
/// <c>Level5.Core</c>, which cannot reference <c>Assembly-CSharp</c>, so the mapping has to sit on
/// this side of the boundary - and keeping it in one function is what lets the shot pipeline stop
/// taking a <c>CharacterProfile</c> later without every call site learning a new type.
///
/// Phase 1c has since migrated onto this: <c>BasketballShotPipeline.ComputeLaunch</c>,
/// <c>UpdateShooterProfileText</c>, and the <c>BasketBall</c>/<c>BasketBallAuto</c> call sites that
/// build the contract, all reach the shot pipeline through <see cref="From"/> now rather than a
/// <see cref="CharacterProfile"/> reference.
/// </summary>
public static class ShooterAttributesFactory
{
    /// <summary>
    /// Builds the contract from a profile. Returns default when the profile is missing rather than
    /// throwing - the shot pipeline already runs in scenes where a shooter can be absent, and a
    /// zeroed attribute set is inert rather than dangerous.
    ///
    /// Logged rather than silent: Phase 1c wired this into <c>BasketBall</c>/<c>BasketBallAuto</c>'s
    /// live shot path, where a missing profile used to throw at the same call site. The zeroed
    /// fallback stays - this only makes the previously-silent case visible in the console instead
    /// of changing what shot gets computed.
    /// </summary>
    public static ShooterAttributes From(CharacterProfile profile)
    {
        if (profile == null)
        {
            Debug.LogWarning("ShooterAttributesFactory.From: no CharacterProfile - shooting with a zeroed shot pipeline.");
            return default;
        }

        return new ShooterAttributes(
            displayName: profile.PlayerDisplayName,
            accuracyTwoPoint: profile.Accuracy2Pt,
            accuracyThreePoint: profile.Accuracy3Pt,
            accuracyFourPoint: profile.Accuracy4Pt,
            accuracySevenPoint: profile.Accuracy7Pt,
            shootAngle: profile.ShootAngle,
            range: profile.Range,
            release: profile.Release,
            luck: profile.Luck,
            jumpForce: profile.JumpForce,
            runSpeed: profile.RunSpeed);
    }

    /// <summary>
    /// The shot kind the launch pipeline's if/else chain would select, read from the *point* flags.
    ///
    /// Deliberately not named ShotKindOf, because <c>BasketBallShotMade.ShotKindOf</c> already
    /// exists and answers a different question with the opposite precedence. That one reads the
    /// *attempt* flags (TwoAttempt..SevenAttempt) ascending, and can, because by the time a make is
    /// registered they are mutually exclusive. These point flags are not mutually exclusive at
    /// launch time, so precedence runs seven, four, three, two and the highest wins - matching
    /// BasketballShotPipeline.ResolveShotAccuracy.
    ///
    /// Two resolvers distinguished only by which flag set they read is a trap; the names say which.
    /// </summary>
    public static ShotKind KindFromPointFlags(BasketBallState basketBallState)
    {
        if (basketBallState == null)
        {
            Debug.LogWarning("ShooterAttributesFactory.KindFromPointFlags: no BasketBallState - resolving to ShotKind.None.");
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
}
