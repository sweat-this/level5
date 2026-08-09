using System;
using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// The one seam between the competitive domain and wherever series are kept.
    ///
    /// There is deliberately no <c>CreateChallenge</c>, <c>AcceptChallenge</c> or
    /// <c>SubmitAttemptResult</c> here. A challenge is a series in <see cref="SeriesStatus.Invited"/>
    /// and accepting one is a domain operation followed by a save; giving storage its own vocabulary
    /// for those would put the rules in two places, and the copy in the storage layer is the one
    /// that would drift.
    ///
    /// A remote implementation replaces this interface and nothing above it. That is the whole
    /// point of it being this small: the coordinator, the series, the games and gameplay itself all
    /// stay exactly as they are when a server starts owning the documents.
    /// </summary>
    public interface IVersusSeriesRepository
    {
        /// <summary>Writes a series. Returns false when it could not be made durable.</summary>
        bool Save(VersusSeries series);

        /// <summary>Reads a series, or null when there is none under that id.</summary>
        VersusSeries Load(SeriesId id);

        /// <summary>Whether a series exists without paying to deserialize it.</summary>
        bool Exists(SeriesId id);

        /// <summary>
        /// Enough about every stored series to list them, without loading any.
        ///
        /// A summary rather than the series itself because a turn list wants twenty of these and
        /// deserializing twenty full series to render twenty rows is how a menu starts stuttering.
        /// </summary>
        IReadOnlyList<SeriesSummary> ListSummaries();

        /// <summary>Removes a series outright. Used by tests and by "forget this".</summary>
        bool Delete(SeriesId id);

        /// <summary>
        /// Moves a completed series out of the active list while keeping it readable.
        ///
        /// Archived series remain the source of truth for rivalry history, so this must never be
        /// implemented as a delete.
        /// </summary>
        bool Archive(SeriesId id);
    }

    /// <summary>
    /// The listable facts about a stored series.
    ///
    /// Everything here is safe for both participants to see: ids, names, format, status, score and
    /// times. No attempt result and no metric, so listing series can never be the thing that leaks a
    /// sealed attempt.
    /// </summary>
    public sealed class SeriesSummary
    {
        public SeriesSummary(
            SeriesId id,
            SeriesStatus status,
            VersusMode mode,
            SeriesFormat format,
            ParticipantId firstId,
            string firstDisplayName,
            ParticipantId secondId,
            string secondDisplayName,
            int firstWins,
            int secondWins,
            int currentGameNumber,
            DateTime createdAtUtc,
            DateTime? completedAtUtc,
            bool archived)
        {
            Id = id;
            Status = status;
            Mode = mode;
            Format = format;
            FirstId = firstId;
            FirstDisplayName = firstDisplayName;
            SecondId = secondId;
            SecondDisplayName = secondDisplayName;
            FirstWins = firstWins;
            SecondWins = secondWins;
            CurrentGameNumber = currentGameNumber;
            CreatedAtUtc = createdAtUtc;
            CompletedAtUtc = completedAtUtc;
            Archived = archived;
        }

        public SeriesId Id { get; }

        public SeriesStatus Status { get; }

        public VersusMode Mode { get; }

        public SeriesFormat Format { get; }

        public ParticipantId FirstId { get; }

        public string FirstDisplayName { get; }

        public ParticipantId SecondId { get; }

        public string SecondDisplayName { get; }

        public int FirstWins { get; }

        public int SecondWins { get; }

        /// <summary>Which game is being played, or 0 when none is.</summary>
        public int CurrentGameNumber { get; }

        public DateTime CreatedAtUtc { get; }

        public DateTime? CompletedAtUtc { get; }

        public bool Archived { get; }

        public bool Involves(ParticipantId participantId)
        {
            return FirstId == participantId || SecondId == participantId;
        }

        public override string ToString()
        {
            return $"{Id}: {FirstDisplayName} {FirstWins}-{SecondWins} {SecondDisplayName} ({Status})";
        }
    }
}
