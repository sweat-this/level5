using System;

namespace Level5.Core
{
    /// <summary>Which line the shot was taken from. Exactly one applies to a made shot.</summary>
    public enum ShotKind
    {
        None = 0,
        Two = 1,
        Three = 2,
        Four = 3,
        Seven = 4
    }

    /// <summary>
    /// Everything the scoring rules need to know about one made shot.
    ///
    /// A plain value, filled in by the scene component that noticed the ball go through the hoop.
    /// It names facts ("this was taken from the four line", "the player was standing on an enabled
    /// marker") rather than systems, which is what lets the arithmetic below be tested without a
    /// basketball, a marker list, or a running match.
    /// </summary>
    public struct ShotScoringInput
    {
        /// <summary>Which line the shot came from.</summary>
        public ShotKind Kind;

        /// <summary>The mode scores by clearing markers rather than by open play.</summary>
        public bool IsMarkerContest;

        /// <summary>The mode scores by distance rather than by shot type (Points by Distance).</summary>
        public bool ScoresByDistance;

        /// <summary>The mode awards a bonus while a streak is running (In the Pocket).</summary>
        public bool HasStreakBonus;

        /// <summary>The streak so far, including this shot.</summary>
        public int ConsecutiveShotsMade;

        /// <summary>The streak length at which <see cref="HasStreakBonus"/> starts paying.</summary>
        public int StreakBonusThreshold;

        /// <summary>The player was on a shot marker, and that marker was enabled.</summary>
        public bool OnEnabledMarker;

        /// <summary>This was the marker's final attempt - the money-ball shot.</summary>
        public bool IsFinalMarkerAttempt;

        /// <summary>The mode is one of the contests whose final marker shot scores double.</summary>
        public bool MarkerFinalShotScoresDouble;

        /// <summary>The player had the money ball active when they shot.</summary>
        public bool MoneyBallActive;

        /// <summary>Distance of the shot, used only when <see cref="ScoresByDistance"/>.</summary>
        public float ShotDistance;
    }

    /// <summary>What one made shot was worth, and which counters it moves.</summary>
    public readonly struct ShotScore
    {
        public ShotScore(int points, ShotKind countedAs, int moneyBallMade)
        {
            Points = points;
            CountedAs = countedAs;
            MoneyBallMade = moneyBallMade;
        }

        /// <summary>Points to add to the running total.</summary>
        public int Points { get; }

        /// <summary>Which made-shot counter to increment, or <see cref="ShotKind.None"/> for none.</summary>
        public ShotKind CountedAs { get; }

        /// <summary>
        /// How many money balls to credit. Normally 0 or 1 - see the note in
        /// <see cref="ShotScoring.Score"/> about the case where it is 2.
        /// </summary>
        public int MoneyBallMade { get; }

        public override string ToString()
        {
            return $"{Points} points as {CountedAs}"
                + (MoneyBallMade > 0 ? $" (+{MoneyBallMade} money ball)" : string.Empty);
        }
    }

    /// <summary>
    /// What a made shot is worth.
    ///
    /// Lifted out of <c>BasketBallShotMade.OnTriggerEnter</c>, where it sat as ~130 lines of nested
    /// conditionals reading <c>GameRules.instance</c>, <c>MatchRuntime</c> and a marker list, and
    /// writing straight into a <c>GameStats</c> component. None of that could be tested, and the
    /// same rules had to be understood again every time a mode was added.
    ///
    /// This is a characterization extraction: the behaviour is preserved exactly, oddities included,
    /// and the tests were written from the original expressions before the call site was changed.
    /// Two of those oddities are called out below rather than fixed, because fixing them here would
    /// be a scoring change hiding inside a refactor.
    /// </summary>
    public static class ShotScoring
    {
        /// <summary>Points by Distance: one point per ten feet, six tenths of the distance, floored.</summary>
        public const float DistancePointsMultiplier = 6f;

        public const float DistancePointsDivisor = 10f;

        /// <summary>
        /// Scores one made shot.
        ///
        /// The three scoring worlds, in the order the original tested them:
        ///
        /// 1. **Open play** - not a marker contest and not distance-scored. The shot is worth its
        ///    line, raised while a streak bonus is running.
        /// 2. **Marker contest** - the shot only counts while standing on an enabled marker, and the
        ///    marker's final attempt is worth double in the three, four and seven point contests.
        /// 3. **Points by Distance** - the line still moves its counter, but the points come from how
        ///    far away the shot was taken.
        /// </summary>
        public static ShotScore Score(ShotScoringInput input)
        {
            if (input.ScoresByDistance)
            {
                // The made-shot counter still moves, but the line is worth nothing by itself.
                return new ShotScore(
                    (int)Math.Floor((input.ShotDistance * DistancePointsMultiplier) / DistancePointsDivisor),
                    input.Kind,
                    MoneyBallCredit(input, alreadyCreditedByMarker: false));
            }

            if (input.IsMarkerContest)
            {
                if (!input.OnEnabledMarker)
                {
                    // Off the marker, or on a marker that is already finished: nothing counts, not
                    // even the made-shot counter. That is what the original did, and it is why a
                    // stray shot during a contest does not inflate the shooting percentage.
                    return new ShotScore(0, ShotKind.None, MoneyBallCredit(input, false));
                }

                int basePoints = BasePoints(input.Kind);
                bool doubled = input.IsFinalMarkerAttempt && input.MarkerFinalShotScoresDouble;

                return new ShotScore(
                    doubled ? basePoints * 2 : basePoints,
                    input.Kind,
                    MoneyBallCredit(input, alreadyCreditedByMarker: doubled));
            }

            return new ShotScore(
                OpenPlayPoints(input),
                input.Kind,
                MoneyBallCredit(input, alreadyCreditedByMarker: false));
        }

        /// <summary>
        /// Open play: the line's value, or its streak-bonus value while a streak is running.
        ///
        /// The bonus is +1 on a three, +2 on a four and +3 on a seven, and nothing on a two - which
        /// is not a formula, just the four numbers the mode was authored with.
        /// </summary>
        private static int OpenPlayPoints(ShotScoringInput input)
        {
            bool bonus = input.HasStreakBonus
                && input.ConsecutiveShotsMade >= input.StreakBonusThreshold;

            switch (input.Kind)
            {
                case ShotKind.Two: return 2;
                case ShotKind.Three: return bonus ? 4 : 3;
                case ShotKind.Four: return bonus ? 6 : 4;
                case ShotKind.Seven: return bonus ? 10 : 7;
                default: return 0;
            }
        }

        private static int BasePoints(ShotKind kind)
        {
            switch (kind)
            {
                case ShotKind.Two: return 2;
                case ShotKind.Three: return 3;
                case ShotKind.Four: return 4;
                case ShotKind.Seven: return 7;
                default: return 0;
            }
        }

        /// <summary>
        /// How many money balls this shot credits: one, or none. Never two.
        ///
        /// A shot can be a money ball for either of two reasons - it was a marker's final attempt in
        /// a contest that doubles those, or the player had the money ball active - and the original
        /// counted them in two independent places with nothing checking they were the same shot. A
        /// shot that was both credited two money balls for one shot, inflating the stat and the
        /// saved high-score row.
        ///
        /// Confirmed as a bug and fixed 2026-08-09. It is one shot, so it is at most one money ball.
        /// The doubled points were never affected - only the count.
        /// </summary>
        private static int MoneyBallCredit(ShotScoringInput input, bool alreadyCreditedByMarker)
        {
            return alreadyCreditedByMarker || input.MoneyBallActive ? 1 : 0;
        }
    }
}
