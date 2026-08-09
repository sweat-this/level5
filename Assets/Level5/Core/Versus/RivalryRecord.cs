using System;
using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Head-to-head history between two participants.
    ///
    /// Derived, never stored. Every number here is a fold over completed series, so it can be thrown
    /// away and rebuilt at any time and can never disagree with the competitions that produced it.
    /// A stored counter alongside the series would be a second source of truth, and the counter is
    /// the one that ends up wrong - a save that fails halfway, a series deleted, a migration that
    /// misses a case.
    ///
    /// This is not a prerequisite for playing. It exists because completed series already contain
    /// everything a rivalry is made of, and building it now proves that they do.
    /// </summary>
    public sealed class RivalryRecord
    {
        private RivalryRecord(ParticipantId left, ParticipantId right)
        {
            Left = left;
            Right = right;
        }

        public ParticipantId Left { get; }

        public ParticipantId Right { get; }

        public int SeriesPlayed { get; private set; }

        public int SeriesWinsLeft { get; private set; }

        public int SeriesWinsRight { get; private set; }

        public int SeriesDrawn { get; private set; }

        public int GameWinsLeft { get; private set; }

        public int GameWinsRight { get; private set; }

        public int GamesDrawn { get; private set; }

        /// <summary>Series won without dropping a game.</summary>
        public int SweepsLeft { get; private set; }

        public int SweepsRight { get; private set; }

        /// <summary>Series that went the full distance of their format.</summary>
        public int DecidingGames { get; private set; }

        public int DecidingGameWinsLeft { get; private set; }

        public int DecidingGameWinsRight { get; private set; }

        /// <summary>
        /// The run of series wins in progress, signed: positive for <see cref="Left"/>, negative for
        /// <see cref="Right"/>, zero when the last series was drawn or none has been played.
        /// </summary>
        public int CurrentStreak { get; private set; }

        public int LongestStreakLeft { get; private set; }

        public int LongestStreakRight { get; private set; }

        public DateTime? LastPlayedUtc { get; private set; }

        public bool IsEmpty => SeriesPlayed == 0;

        /// <summary>
        /// Folds completed series into a record.
        ///
        /// Series are sorted by completion time first, because a streak is a statement about order
        /// and the repository has no obligation to list in any particular one.
        /// </summary>
        public static RivalryRecord Build(
            ParticipantId left,
            ParticipantId right,
            IEnumerable<VersusSeries> completedSeries)
        {
            RivalryRecord record = new RivalryRecord(left, right);
            if (completedSeries == null)
            {
                return record;
            }

            List<VersusSeries> ordered = new List<VersusSeries>();
            foreach (VersusSeries series in completedSeries)
            {
                if (series?.Result == null)
                {
                    continue;
                }

                if (!series.Participants.Contains(left) || !series.Participants.Contains(right))
                {
                    continue;
                }

                ordered.Add(series);
            }

            ordered.Sort(CompareByCompletion);

            foreach (VersusSeries series in ordered)
            {
                record.Fold(series);
            }

            return record;
        }

        private void Fold(VersusSeries series)
        {
            SeriesResult result = series.Result;
            SeriesPlayed++;
            LastPlayedUtc = result.CompletedAtUtc;

            foreach (VersusGame game in series.Games)
            {
                if (game.Result == null)
                {
                    continue;
                }

                if (game.Result.Kind == GameOutcomeKind.Draw)
                {
                    GamesDrawn++;
                    continue;
                }

                if (!game.Result.HasWinner)
                {
                    continue;
                }

                if (game.Result.WinnerId == Left)
                {
                    GameWinsLeft++;
                }
                else if (game.Result.WinnerId == Right)
                {
                    GameWinsRight++;
                }
            }

            if (series.Score.PlayedGames == series.Snapshot.Format.GameCount
                && series.Snapshot.Format.GameCount > 1)
            {
                DecidingGames++;
                if (result.WinnerId == Left)
                {
                    DecidingGameWinsLeft++;
                }
                else if (result.WinnerId == Right)
                {
                    DecidingGameWinsRight++;
                }
            }

            if (!result.HasWinner)
            {
                SeriesDrawn++;
                CurrentStreak = 0;
                return;
            }

            bool leftWon = result.WinnerId == Left;
            if (leftWon)
            {
                SeriesWinsLeft++;
                CurrentStreak = CurrentStreak > 0 ? CurrentStreak + 1 : 1;
                LongestStreakLeft = Math.Max(LongestStreakLeft, CurrentStreak);
            }
            else
            {
                SeriesWinsRight++;
                CurrentStreak = CurrentStreak < 0 ? CurrentStreak - 1 : -1;
                LongestStreakRight = Math.Max(LongestStreakRight, -CurrentStreak);
            }

            if (result.IsSweep)
            {
                if (leftWon)
                {
                    SweepsLeft++;
                }
                else
                {
                    SweepsRight++;
                }
            }
        }

        private static int CompareByCompletion(VersusSeries left, VersusSeries right)
        {
            return left.Result.CompletedAtUtc.CompareTo(right.Result.CompletedAtUtc);
        }

        public override string ToString()
        {
            return $"{Left} {SeriesWinsLeft}-{SeriesWinsRight} {Right} over {SeriesPlayed} series";
        }
    }
}
