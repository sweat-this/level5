using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// What is driving a participant.
    ///
    /// The domain reads this for exactly one thing: telling a human turn from one the game plays
    /// itself. It never reads it to decide how a result is produced, how a game resolves or how a
    /// series advances - which is what makes swapping <see cref="LocalHuman"/> for
    /// <see cref="RemoteHuman"/> a launch-path change rather than a rewrite.
    /// </summary>
    public enum ParticipantKind
    {
        /// <summary>A person on this device.</summary>
        LocalHuman = 0,

        /// <summary>A person on another device. The series does not care which.</summary>
        RemoteHuman = 1,

        /// <summary>Played by the game.</summary>
        Cpu = 2
    }

    /// <summary>
    /// One side of a competition.
    ///
    /// Identity plus a name to show. Notably absent: input device, roster slot, account id, save
    /// key, character. Those all belong to the match the attempt is played in, not to the
    /// competition the attempt belongs to, and keeping them out is what lets a series survive a
    /// participant changing character between games - or machine between weeks.
    /// </summary>
    public sealed class MatchParticipant
    {
        public MatchParticipant(ParticipantId id, string displayName, ParticipantKind kind = ParticipantKind.LocalHuman)
        {
            if (!id.HasValue)
            {
                throw new VersusDomainException("a participant needs an id");
            }

            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? id.Value : displayName;
            Kind = kind;
        }

        public ParticipantId Id { get; }

        public string DisplayName { get; }

        public ParticipantKind Kind { get; }

        public bool IsLocalHuman => Kind == ParticipantKind.LocalHuman;

        public override string ToString()
        {
            return $"{DisplayName} ({Id})";
        }
    }

    /// <summary>
    /// The two sides of a series, in order.
    ///
    /// Two, not N. Best-of-N is a head-to-head format and every rule in this domain - required
    /// wins, alternating first attempt, sealed reveal, rivalry history - is defined for two sides.
    /// Modelling an arbitrary count now would mean inventing behaviour for cases the game does not
    /// have, which is worse than the honest constraint.
    ///
    /// "First" and "Second" are positions in the series, not privileges: which of them attempts
    /// first in a given game is decided per game.
    /// </summary>
    public sealed class VersusParticipants
    {
        public VersusParticipants(MatchParticipant first, MatchParticipant second)
        {
            if (first == null || second == null)
            {
                throw new VersusDomainException("a series needs two participants");
            }

            if (first.Id == second.Id)
            {
                throw new VersusDomainException(
                    $"a series needs two different participants; both are '{first.Id}'");
            }

            First = first;
            Second = second;
        }

        public MatchParticipant First { get; }

        public MatchParticipant Second { get; }

        public bool Contains(ParticipantId id)
        {
            return First.Id == id || Second.Id == id;
        }

        public MatchParticipant Find(ParticipantId id)
        {
            if (First.Id == id)
            {
                return First;
            }

            return Second.Id == id ? Second : null;
        }

        /// <summary>The other side. Throws for an id that is not in this series at all.</summary>
        public MatchParticipant Opponent(ParticipantId id)
        {
            if (First.Id == id)
            {
                return Second;
            }

            if (Second.Id == id)
            {
                return First;
            }

            throw new VersusDomainException($"'{id}' is not a participant in this series");
        }

        /// <summary>Position in the series: 0 or 1. Used to alternate who attempts first.</summary>
        public int IndexOf(ParticipantId id)
        {
            if (First.Id == id)
            {
                return 0;
            }

            if (Second.Id == id)
            {
                return 1;
            }

            throw new VersusDomainException($"'{id}' is not a participant in this series");
        }

        public MatchParticipant At(int index)
        {
            switch (index)
            {
                case 0: return First;
                case 1: return Second;
                default:
                    throw new VersusDomainException($"a series has participants 0 and 1, not {index}");
            }
        }

        public override string ToString()
        {
            return $"{First.DisplayName} v {Second.DisplayName}";
        }
    }
}
