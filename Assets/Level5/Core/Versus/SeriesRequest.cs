using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// What a caller is asking for when it wants a series.
    ///
    /// Not authoritative and not validated by construction, in the same spirit as
    /// <c>MatchRequest</c>: any source - a menu, a dev console, an accepted challenge from a server
    /// later - hands one of these to the coordinator and gets the same verdict. A screen filtering
    /// its own options is a convenience for the player; the validator is the gate.
    /// </summary>
    public sealed class SeriesRequest
    {
        private readonly List<RulesetId> playlist;

        public SeriesRequest(
            MatchParticipant challenger,
            MatchParticipant opponent,
            SeriesFormat format,
            IEnumerable<RulesetId> playlist,
            VersusMode mode = VersusMode.LocalAlternating,
            InformationPolicy informationPolicy = InformationPolicy.SealedAttempt,
            bool requiresInvitation = false,
            bool alternatesFirstAttempt = true,
            string source = null)
        {
            Challenger = challenger;
            Opponent = opponent;
            Format = format;
            Mode = mode;
            InformationPolicy = informationPolicy;
            RequiresInvitation = requiresInvitation;
            AlternatesFirstAttempt = alternatesFirstAttempt;
            Source = string.IsNullOrEmpty(source) ? "unknown" : source;

            this.playlist = playlist == null ? new List<RulesetId>() : new List<RulesetId>(playlist);
        }

        /// <summary>The participant who asked. Position one in the series.</summary>
        public MatchParticipant Challenger { get; }

        public MatchParticipant Opponent { get; }

        public SeriesFormat Format { get; }

        /// <summary>The rulesets to play, in order. One per game in the format.</summary>
        public IReadOnlyList<RulesetId> Playlist => playlist;

        public VersusMode Mode { get; }

        public InformationPolicy InformationPolicy { get; }

        /// <summary>
        /// Whether the series starts as an invitation the opponent has to accept. A local series
        /// between two people at one device does not need one; a correspondence challenge does.
        /// </summary>
        public bool RequiresInvitation { get; }

        public bool AlternatesFirstAttempt { get; }

        /// <summary>Where the request came from. Diagnostics only.</summary>
        public string Source { get; }

        public override string ToString()
        {
            string challenger = Challenger == null ? "?" : Challenger.DisplayName;
            string opponent = Opponent == null ? "?" : Opponent.DisplayName;
            return $"{challenger} v {opponent}, {Format}, {Mode}, {InformationPolicy} from {Source}";
        }
    }
}
