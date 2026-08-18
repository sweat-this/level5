using Level5.Core;

/// <summary>
/// Reads a shooter's numbers off the scene component into a plain value.
///
/// Phase 1a of the systems restructure. This is deliberately the only place that knows a
/// <see cref="ShooterAttributes"/> comes from a <see cref="CharacterProfile"/>. The contract lives in
/// <c>Level5.Core</c>, which cannot reference <c>Assembly-CSharp</c>, so the mapping has to sit on
/// this side of the boundary - and keeping it in one function is what lets the shot pipeline stop
/// taking a <c>CharacterProfile</c> later without every call site learning a new type.
///
/// Nothing is migrated onto this yet. Phase 1c moves consumers one at a time.
/// </summary>
public static class ShooterAttributesFactory
{
    /// <summary>
    /// Builds the contract from a profile. Returns default when the profile is missing rather than
    /// throwing - the shot pipeline already runs in scenes where a shooter can be absent, and a
    /// zeroed attribute set is inert rather than dangerous.
    /// </summary>
    public static ShooterAttributes From(CharacterProfile profile)
    {
        if (profile == null)
        {
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
    /// The shot kind the pipeline's if/else chain would have selected for this ball state.
    ///
    /// The flags are not mutually exclusive, so order matters: seven, then four, then three, then
    /// two. Extracted here so the precedence exists once rather than being re-typed at every site
    /// that needs to pick an accuracy.
    /// </summary>
    public static ShotKind KindOf(BasketBallState basketBallState)
    {
        if (basketBallState == null)
        {
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
