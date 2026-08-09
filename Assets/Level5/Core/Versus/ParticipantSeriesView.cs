using System;
using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Which kind of competition a series is.
    ///
    /// Chosen when the series is created, and every ruleset in the playlist has to declare the
    /// matching capability before it can be. This is the single value that decides whether the
    /// participants are expected to be sitting next to each other.
    /// </summary>
    public enum VersusMode
    {
        /// <summary>Both attempts in one sitting, on one device.</summary>
        LocalSimultaneous = 0,

        /// <summary>Turns taken on one device, one attempt at a time.</summary>
        LocalAlternating = 1,

        /// <summary>Attempts separated by however long the participants like.</summary>
        Asynchronous = 2,

        /// <summary>Live networked play. Not implemented; here so it can be rejected by name.</summary>
        OnlineRealtime = 3
    }

    /// <summary>Maps a competition kind onto the capability a ruleset has to declare to be in one.</summary>
    public static class VersusModes
    {
        public static VersusCapability RequiredCapability(VersusMode mode)
        {
            switch (mode)
            {
                case VersusMode.LocalSimultaneous: return VersusCapability.LocalSimultaneous;
                case VersusMode.LocalAlternating: return VersusCapability.LocalAlternating;
                case VersusMode.Asynchronous: return VersusCapability.Asynchronous;
                case VersusMode.OnlineRealtime: return VersusCapability.OnlineRealtime;
                default:
                    throw new VersusDomainException($"unknown versus mode {mode}");
            }
        }

        /// <summary>
        /// Whether the two participants are expected to be present at the same time. Used by the
        /// launch path to decide whether it can hand straight over to the next attempt.
        /// </summary>
        public static bool IsLocal(VersusMode mode)
        {
            return mode == VersusMode.LocalSimultaneous || mode == VersusMode.LocalAlternating;
        }
    }

    /// <summary>
    /// A whole series as one participant is entitled to see it.
    ///
    /// Scores are expressed as "yours" and "theirs" rather than "first" and "second", because a
    /// screen that has to work out which side it is looking at is a screen that will eventually get
    /// it backwards.
    /// </summary>
    public sealed class ParticipantSeriesView
    {
        private readonly List<ParticipantGameView> games;

        public ParticipantSeriesView(
            SeriesId seriesId,
            SeriesFormat format,
            SeriesStatus status,
            VersusMode mode,
            InformationPolicy informationPolicy,
            MatchParticipant you,
            MatchParticipant opponent,
            int yourWins,
            int opponentWins,
            int draws,
            List<ParticipantGameView> games,
            SeriesResult result,
            int currentGameIndex)
        {
            SeriesId = seriesId;
            Format = format;
            Status = status;
            Mode = mode;
            InformationPolicy = informationPolicy;
            You = you;
            Opponent = opponent;
            YourWins = yourWins;
            OpponentWins = opponentWins;
            Draws = draws;
            this.games = games ?? new List<ParticipantGameView>();
            Result = result;
            CurrentGameIndex = currentGameIndex;
        }

        public SeriesId SeriesId { get; }

        public SeriesFormat Format { get; }

        public SeriesStatus Status { get; }

        public VersusMode Mode { get; }

        public InformationPolicy InformationPolicy { get; }

        public MatchParticipant You { get; }

        public MatchParticipant Opponent { get; }

        public int YourWins { get; }

        public int OpponentWins { get; }

        public int Draws { get; }

        public IReadOnlyList<ParticipantGameView> Games => games;

        /// <summary>The verdict once the series is over. Null before that.</summary>
        public SeriesResult Result { get; }

        /// <summary>Index of the game being played, or -1 when there is none.</summary>
        public int CurrentGameIndex { get; }

        public ParticipantGameView CurrentGame => CurrentGameIndex >= 0 && CurrentGameIndex < games.Count
            ? games[CurrentGameIndex]
            : null;

        /// <summary>Wins still needed. Zero once the series is settled.</summary>
        public int WinsNeeded => Math.Max(0, Format.RequiredWins - YourWins);

        public bool IsYourTurn => CurrentGame != null && CurrentGame.IsYourTurn;

        public bool IsAwaitingOpponent => CurrentGame != null && CurrentGame.IsAwaitingOpponent;

        public bool YouWon => Result != null && Result.WinnerId == You.Id;

        public override string ToString()
        {
            return $"{You.DisplayName} {YourWins}-{OpponentWins} {Opponent.DisplayName} ({Format}, {Status})";
        }
    }
}
