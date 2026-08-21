using Level5.Core;
using UnityEngine;

/// <summary>
/// Reads a shooter's numbers off the scene component into a plain value.
///
/// Player↔basketball cycle-cut slice: moved from <c>Assets/Scripts/basketball/ShooterAttributesFactory</c>
/// (Phase 1a/1c) to the player side. Once basketball reaches a shooter only through
/// <see cref="IShooterActor"/>, nothing in <c>Assets/Scripts/basketball</c> needs to know a
/// <see cref="ShooterAttributes"/> comes from a <see cref="CharacterProfile"/> - only
/// <c>PlayerController</c>/<c>AutoPlayerController</c>, which own the mapping now, do.
/// </summary>
public static class ShooterAttributesMapper
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
            Debug.LogWarning("ShooterAttributesMapper.From: no CharacterProfile - shooting with a zeroed shot pipeline.");
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
}
