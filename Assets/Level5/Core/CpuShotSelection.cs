namespace Level5.Core
{
    /// <summary>
    /// Everything CPU shot-kind selection needs, decoupled from <c>CharacterProfile</c>,
    /// <c>GameLevelManager</c> and world-position geometry (#54).
    ///
    /// Accuracies here are runtime-resolved values (what <c>CharacterProfile</c> holds after
    /// <c>calculateAccuracyAttributeRatings</c> has run, or the Arcade/easy override), not the raw
    /// serialized prefab fields - the #54 audit found those two routinely disagree.
    /// </summary>
    public readonly struct CpuShotSelectionContext
    {
        public CpuShotSelectionContext(
            ShotKind preferredKind,
            float accuracyThree,
            float accuracyFour,
            float accuracySeven,
            bool canShootSeven,
            int scoreDeficit)
        {
            PreferredKind = preferredKind;
            AccuracyThree = accuracyThree;
            AccuracyFour = accuracyFour;
            AccuracySeven = accuracySeven;
            CanShootSeven = canShootSeven;
            ScoreDeficit = scoreDeficit;
        }

        /// <summary>The authored CPU shooter identity (<c>CpuBaseStats.ShooterType</c>), expressed as
        /// a shot kind. Breaks ties in the base preference below; never overrides a clear accuracy
        /// leader.</summary>
        public ShotKind PreferredKind { get; }

        public float AccuracyThree { get; }

        public float AccuracyFour { get; }

        public float AccuracySeven { get; }

        /// <summary>Whether a seven-point shot is legal at all right now - the arena supports it and
        /// this shooter's range/accuracy clear <see cref="CpuSevenPointEligibility"/>. When false,
        /// Seven is not a legal shot kind regardless of accuracy or preference.</summary>
        public bool CanShootSeven { get; }

        /// <summary>How far behind the leading opponent this CPU is, floored at zero. Drives the
        /// comeback override in <see cref="CpuShotSelectionPolicy"/>.</summary>
        public int ScoreDeficit { get; }
    }

    /// <summary>
    /// The single, explicit CPU shot-kind decision (#54).
    ///
    /// Replaces <c>AutoPlayerController.getClosestPositionMarker</c>'s three independent <c>if</c>
    /// statements, which mixed accuracy preference with a score-gap override in one condition each
    /// and silently overwrote one another - leaving an unreachable "Vector3.zero" default for the
    /// 13-15 point deficit gap and, in practice, no distinguishable three-point branch for 21 of the
    /// 22 authored CPU shooters (see the issue for the measured breakdown).
    ///
    /// Plain C#: no <c>MonoBehaviour</c>, no <c>GameObject</c>, no <c>Vector3</c>, no randomness. The
    /// result is always one of <see cref="ShotKind.Three"/>, <see cref="ShotKind.Four"/> or
    /// <see cref="ShotKind.Seven"/> - never <see cref="ShotKind.None"/> or <see cref="ShotKind.Two"/>.
    /// </summary>
    public static class CpuShotSelectionPolicy
    {
        /// <summary>Below this deficit, the comeback override does not apply.</summary>
        public const int ComebackFourThreshold = 16;

        /// <summary>At or above this deficit, the comeback override reaches for seven (falling back to
        /// four when seven is not legal).</summary>
        public const int ComebackSevenThreshold = 21;

        public static ShotKind Select(in CpuShotSelectionContext context)
        {
            ShotKind basePreference = SelectBasePreference(in context);
            return ApplyComeback(in context, basePreference);
        }

        /// <summary>
        /// Who this character is, independent of the game situation.
        ///
        /// Considers only legal shot kinds (three and four always; seven only when
        /// <see cref="CpuShotSelectionContext.CanShootSeven"/>) and prefers whichever has the
        /// strongest runtime accuracy. A tie among the legal leaders is resolved by the authored
        /// preferred kind when that preference is itself one of the tied leaders; otherwise - most
        /// notably an unresolved three/four tie, which is the common case since 64 of 69 authored
        /// characters share <c>accuracy3pt == accuracy4pt</c> - it falls back to four, preserving the
        /// original code's default.
        ///
        /// **Known, currently-unreachable edge:** the fallback is "four", not "whichever tied leader
        /// is closest to the preference". If three and seven tie for the lead with four strictly
        /// behind, and the preferred kind is four (not itself a tied leader), this still returns
        /// four even though four is not one of the accuracy leaders. No authored CPU shooter reaches
        /// that shape today - a shooter's own dominant stat structurally stays at or above its other
        /// two until all three converge at the level-25 cap together (see the #54 characterization
        /// of all 22 prefabs) - but a future archetype or retune could. Treat that as a deliberate
        /// follow-up if it ever matters, not a defect in this method.
        /// </summary>
        private static ShotKind SelectBasePreference(in CpuShotSelectionContext context)
        {
            float best = context.AccuracyThree;
            if (context.AccuracyFour > best)
            {
                best = context.AccuracyFour;
            }
            if (context.CanShootSeven && context.AccuracySeven > best)
            {
                best = context.AccuracySeven;
            }

            int tiedCount = 0;
            ShotKind soleLeader = ShotKind.Four;
            bool preferredIsAmongLeaders = false;

            if (context.AccuracyThree == best)
            {
                tiedCount++;
                soleLeader = ShotKind.Three;
                preferredIsAmongLeaders |= context.PreferredKind == ShotKind.Three;
            }
            if (context.AccuracyFour == best)
            {
                tiedCount++;
                soleLeader = ShotKind.Four;
                preferredIsAmongLeaders |= context.PreferredKind == ShotKind.Four;
            }
            if (context.CanShootSeven && context.AccuracySeven == best)
            {
                tiedCount++;
                soleLeader = ShotKind.Seven;
                preferredIsAmongLeaders |= context.PreferredKind == ShotKind.Seven;
            }

            if (tiedCount == 1)
            {
                return soleLeader;
            }

            return preferredIsAmongLeaders ? context.PreferredKind : ShotKind.Four;
        }

        /// <summary>
        /// The score-situation override, applied after - and able to replace - the base preference.
        ///
        /// Explicit thresholds, not tuned here: below 16 the base preference stands; 16-20 reaches for
        /// four; 21+ reaches for seven when legal, otherwise four. This removes the original code's
        /// unexplained 13-15 gap, where neither the three- nor four-point condition fired.
        /// </summary>
        private static ShotKind ApplyComeback(in CpuShotSelectionContext context, ShotKind basePreference)
        {
            if (context.ScoreDeficit >= ComebackSevenThreshold)
            {
                return context.CanShootSeven ? ShotKind.Seven : ShotKind.Four;
            }
            if (context.ScoreDeficit >= ComebackFourThreshold)
            {
                return ShotKind.Four;
            }
            return basePreference;
        }
    }

    /// <summary>
    /// Whether a CPU shooter is allowed to attempt a seven-point shot right now (#54).
    ///
    /// Extracted, unchanged, from <c>AutoPlayerController.cpuShootSevenpointers</c> so the formula has
    /// one name and is reachable from EditMode tests. The formula is preserved deliberately, oddity
    /// included: <c>range / accuracySeven</c> means a *higher* seven-point accuracy makes this *less*
    /// likely to pass, which is very likely a separate bug from #54 and is reported, not fixed, here.
    /// </summary>
    public static class CpuSevenPointEligibility
    {
        /// <summary>The range/accuracy quotient (as a percentage) must clear this to be eligible.</summary>
        public const float RangePercentThreshold = 70f;

        public static bool IsEligible(bool levelHasSevenPointers, int range, float accuracySeven)
        {
            // AUD-055 (preserved): an unset accuracySeven made this Infinity, which cleared the
            // threshold below and turned every CPU into a seven-point specialist.
            if (accuracySeven <= 0)
            {
                return false;
            }

            float rangePercent = (range / accuracySeven) * 100f;
            return levelHasSevenPointers && rangePercent > RangePercentThreshold;
        }
    }
}
