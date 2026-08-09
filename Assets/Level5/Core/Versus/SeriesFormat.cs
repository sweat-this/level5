using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// A best-of-N format: how many games at most, and how many wins settle it.
    ///
    /// One type for every length rather than a class per format. Best-of-three and best-of-seven
    /// differ by two integers; giving them separate implementations would mean two places to get
    /// early termination wrong.
    ///
    /// The length must be odd. An even best-of-N cannot be settled by wins alone, and inventing a
    /// decider for it would be inventing gameplay.
    /// </summary>
    public readonly struct SeriesFormat : IEquatable<SeriesFormat>
    {
        /// <summary>The presentation calls this a Quick Challenge; the domain does not care.</summary>
        public static readonly SeriesFormat BestOf1 = new SeriesFormat(1);

        /// <summary>Standard Series.</summary>
        public static readonly SeriesFormat BestOf3 = new SeriesFormat(3);

        /// <summary>Extended Series.</summary>
        public static readonly SeriesFormat BestOf5 = new SeriesFormat(5);

        /// <summary>Championship Series.</summary>
        public static readonly SeriesFormat BestOf7 = new SeriesFormat(7);

        public SeriesFormat(int gameCount)
        {
            if (gameCount < 1 || gameCount > 7 || gameCount % 2 == 0)
            {
                throw new VersusDomainException(
                    $"a series must be a best of 1, 3, 5 or 7; {gameCount} is not one of those");
            }

            GameCount = gameCount;
        }

        /// <summary>The most games that can be played. Often fewer are.</summary>
        public int GameCount { get; }

        /// <summary>Wins that settle the series: four in a best of seven.</summary>
        public int RequiredWins => (GameCount / 2) + 1;

        public static SeriesFormat FromGameCount(int gameCount)
        {
            return new SeriesFormat(gameCount);
        }

        /// <summary>All four supported formats, shortest first.</summary>
        public static SeriesFormat[] All()
        {
            return new[] { BestOf1, BestOf3, BestOf5, BestOf7 };
        }

        public bool Equals(SeriesFormat other)
        {
            return GameCount == other.GameCount;
        }

        public override bool Equals(object obj)
        {
            return obj is SeriesFormat other && Equals(other);
        }

        public override int GetHashCode()
        {
            return GameCount;
        }

        public override string ToString()
        {
            return $"best of {GameCount}";
        }
    }

    /// <summary>Wins and draws so far. A value, recomputed from the games rather than accumulated.</summary>
    public readonly struct SeriesScore
    {
        public SeriesScore(int firstWins, int secondWins, int draws)
        {
            FirstWins = firstWins;
            SecondWins = secondWins;
            Draws = draws;
        }

        public int FirstWins { get; }

        public int SecondWins { get; }

        /// <summary>
        /// Games neither side won. A draw is possible whenever a ruleset's comparison keys can all
        /// come out level, and pretending otherwise is what leaves a series stuck at 1-1-1.
        /// </summary>
        public int Draws { get; }

        public int DecidedGames => FirstWins + SecondWins;

        public int PlayedGames => DecidedGames + Draws;

        public int WinsFor(int participantIndex)
        {
            return participantIndex == 0 ? FirstWins : SecondWins;
        }

        public override string ToString()
        {
            return Draws > 0 ? $"{FirstWins}-{SecondWins} ({Draws} drawn)" : $"{FirstWins}-{SecondWins}";
        }
    }
}
