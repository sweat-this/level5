namespace Level5.Core
{
    /// <summary>
    /// Everything the shot pipeline needs to know about whoever is shooting.
    ///
    /// Phase 1a of the systems restructure. `basketball` reaches into `player` 19 times and `player`
    /// reaches back 15, which - together with `game manager` on the third side - is the cycle that
    /// pins `Assets/Scripts` into one assembly. Most of the inbound half is the ball asking the
    /// shooter how good the shot should be: `CharacterProfile.Accuracy2Pt`..`Accuracy7Pt`,
    /// `ShootAngle`, `Range`, `Release`, `Luck`, `JumpForce`, `RunSpeed` and `PlayerDisplayName`.
    ///
    /// This is that question as a value, so the pipeline can be handed the numbers instead of the
    /// shooter. It carries no reference to a <c>CharacterProfile</c>, a <c>MonoBehaviour</c> or
    /// anything in <c>Assembly-CSharp</c>, which is what lets the arithmetic eventually live behind
    /// an assembly boundary.
    ///
    /// <see cref="MadeShotResult"/> is the outbound half of the same seam and already travels one
    /// way. This is the inbound half.
    ///
    /// Nothing is migrated onto this yet - Phase 1a introduces the contract and proves it produces
    /// identical results, and Phase 1c moves consumers one at a time.
    /// </summary>
    public readonly struct ShooterAttributes
    {
        /// <summary>What the pipeline uses when no shot-type flag is set. See AccuracyFor.</summary>
        public const float NoShotKindAccuracy = 100f;

        public ShooterAttributes(
            string displayName,
            float accuracyTwoPoint,
            float accuracyThreePoint,
            float accuracyFourPoint,
            float accuracySevenPoint,
            int shootAngle,
            int range,
            int release,
            int luck,
            float jumpForce,
            float runSpeed)
        {
            DisplayName = displayName;
            AccuracyTwoPoint = accuracyTwoPoint;
            AccuracyThreePoint = accuracyThreePoint;
            AccuracyFourPoint = accuracyFourPoint;
            AccuracySevenPoint = accuracySevenPoint;
            ShootAngle = shootAngle;
            Range = range;
            Release = release;
            Luck = luck;
            JumpForce = jumpForce;
            RunSpeed = runSpeed;
        }

        public string DisplayName { get; }

        public float AccuracyTwoPoint { get; }

        public float AccuracyThreePoint { get; }

        public float AccuracyFourPoint { get; }

        public float AccuracySevenPoint { get; }

        public int ShootAngle { get; }

        public int Range { get; }

        public int Release { get; }

        public int Luck { get; }

        public float JumpForce { get; }

        public float RunSpeed { get; }

        /// <summary>
        /// The accuracy that applies to a shot of the given kind.
        ///
        /// Mirrors <c>BasketballShotPipeline.ResolveShotAccuracy</c> exactly, including two
        /// preserved oddities. The flags on <c>BasketBallState</c> are not mutually exclusive, so
        /// precedence matters and runs seven, four, three, two - the original was four independent
        /// assignments in the opposite order, where the last match won, which is the same answer.
        ///
        /// And <see cref="ShotKind.None"/> returns 100, not two-point accuracy. The original left
        /// the accuracy term at 0 when no flag was set, and an accuracy of 100 reproduces that
        /// exactly. Returning AccuracyTwoPoint here would look tidier and silently change the shot.
        /// </summary>
        public float AccuracyFor(ShotKind kind)
        {
            switch (kind)
            {
                case ShotKind.Seven:
                    return AccuracySevenPoint;
                case ShotKind.Four:
                    return AccuracyFourPoint;
                case ShotKind.Three:
                    return AccuracyThreePoint;
                case ShotKind.Two:
                    return AccuracyTwoPoint;
                default:
                    return NoShotKindAccuracy;
            }
        }
    }
}
