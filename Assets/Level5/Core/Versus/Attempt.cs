using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Where one competitive run is in its life.
    ///
    /// An explicit state, never inferred from "the score is still null" or "there is no completion
    /// timestamp". A run that legitimately scores zero and a run that was never played look
    /// identical under that kind of inference, and telling them apart is the whole basis of a
    /// correspondence turn.
    /// </summary>
    public enum AttemptState
    {
        /// <summary>Issued, not yet handed to gameplay.</summary>
        Created = 0,

        /// <summary>The participant may begin. This is the state an outstanding turn sits in.</summary>
        Ready = 1,

        /// <summary>Gameplay is running, or was running when the application stopped.</summary>
        Started = 2,

        /// <summary>Finished, with a result.</summary>
        Completed = 3,

        /// <summary>Given up. A new attempt may be issued in its place.</summary>
        Abandoned = 4
    }

    /// <summary>
    /// One participant's run at one game of a series.
    ///
    /// This is also the attempt ticket. The brief names an <c>AttemptTicket</c> carrying an id, a
    /// participant, a game, a ruleset version, an issue time and a status - which is this type's
    /// field list exactly, so a second type mirroring it would be two objects owning one lifecycle.
    /// The properties a server would later sign are all here; what changes when a backend becomes
    /// authoritative is who constructs it, not what it is.
    ///
    /// The lifecycle is enforced here rather than by whoever holds the reference. Completing twice
    /// throws, and that is the retry exploit closed at the only place it can be closed.
    /// </summary>
    public sealed class Attempt
    {
        private Attempt(
            AttemptId id,
            ParticipantId participantId,
            int gameIndex,
            RulesetId rulesetId,
            int rulesetVersion,
            AttemptState state,
            DateTime issuedAtUtc,
            DateTime? startedAtUtc,
            DateTime? completedAtUtc,
            AttemptResult result)
        {
            Id = id;
            ParticipantId = participantId;
            GameIndex = gameIndex;
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            State = state;
            IssuedAtUtc = issuedAtUtc;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            Result = result;
        }

        public AttemptId Id { get; }

        public ParticipantId ParticipantId { get; }

        /// <summary>Which game of the series this run belongs to. Zero-based.</summary>
        public int GameIndex { get; }

        public RulesetId RulesetId { get; }

        /// <summary>The version of the rules this run is played under - the series' frozen version.</summary>
        public int RulesetVersion { get; }

        public AttemptState State { get; private set; }

        public DateTime IssuedAtUtc { get; }

        public DateTime? StartedAtUtc { get; private set; }

        public DateTime? CompletedAtUtc { get; private set; }

        public AttemptResult Result { get; private set; }

        /// <summary>True while this attempt is somebody's outstanding turn.</summary>
        public bool IsLive => State == AttemptState.Created
            || State == AttemptState.Ready
            || State == AttemptState.Started;

        public bool IsCompleted => State == AttemptState.Completed;

        /// <summary>Issues a fresh attempt. Only a <see cref="VersusGame"/> should call this.</summary>
        public static Attempt Issue(
            AttemptId id,
            ParticipantId participantId,
            int gameIndex,
            RulesetId rulesetId,
            int rulesetVersion,
            DateTime issuedAtUtc)
        {
            if (!id.HasValue)
            {
                throw new VersusDomainException("an attempt needs an id");
            }

            if (!participantId.HasValue)
            {
                throw new VersusDomainException("an attempt needs the participant it was issued to");
            }

            if (gameIndex < 0)
            {
                throw new VersusDomainException("an attempt needs a game index of at least 0");
            }

            if (!rulesetId.HasValue)
            {
                throw new VersusDomainException("an attempt needs the ruleset it is played under");
            }

            if (rulesetVersion < 1)
            {
                throw new VersusDomainException("an attempt needs a ruleset version of at least 1");
            }

            return new Attempt(
                id,
                participantId,
                gameIndex,
                rulesetId,
                rulesetVersion,
                AttemptState.Created,
                issuedAtUtc,
                null,
                null,
                null);
        }

        /// <summary>Rebuilds an attempt from storage without replaying its transitions.</summary>
        public static Attempt Restore(
            AttemptId id,
            ParticipantId participantId,
            int gameIndex,
            RulesetId rulesetId,
            int rulesetVersion,
            AttemptState state,
            DateTime issuedAtUtc,
            DateTime? startedAtUtc,
            DateTime? completedAtUtc,
            AttemptResult result)
        {
            if (state == AttemptState.Completed && result == null)
            {
                throw new VersusDomainException(
                    $"attempt {id} was stored as completed with no result, so the document is corrupt");
            }

            return new Attempt(
                id,
                participantId,
                gameIndex,
                rulesetId,
                rulesetVersion,
                state,
                issuedAtUtc,
                startedAtUtc,
                completedAtUtc,
                result);
        }

        /// <summary>
        /// Hands the attempt to the participant. Returns false when it is already ready, so a
        /// double-tapped button is a no-op rather than an error.
        /// </summary>
        public bool MarkReady()
        {
            if (State == AttemptState.Ready)
            {
                return false;
            }

            RequireState(AttemptState.Created, "make ready");
            State = AttemptState.Ready;
            return true;
        }

        /// <summary>
        /// Records that gameplay began. Returns false if it had already begun - a scene reload
        /// during a run is not a new attempt.
        /// </summary>
        public bool Start(DateTime startedAtUtc)
        {
            if (State == AttemptState.Started)
            {
                return false;
            }

            if (State != AttemptState.Created && State != AttemptState.Ready)
            {
                throw new VersusDomainException(
                    $"attempt {Id} cannot be started from {State}");
            }

            State = AttemptState.Started;
            StartedAtUtc = startedAtUtc;
            return true;
        }

        /// <summary>
        /// Records the run's result and closes the attempt.
        ///
        /// Throws on a second completion rather than ignoring it. The second submission is either a
        /// bug or somebody replaying a run they did not like, and both need to be visible.
        /// </summary>
        public void Complete(AttemptResult result, DateTime completedAtUtc)
        {
            if (result == null)
            {
                throw new VersusDomainException($"attempt {Id} cannot be completed without a result");
            }

            if (State == AttemptState.Completed)
            {
                throw new VersusDomainException(
                    $"attempt {Id} has already been completed; a completed attempt cannot be resubmitted");
            }

            if (State == AttemptState.Abandoned)
            {
                throw new VersusDomainException($"attempt {Id} was abandoned and cannot be completed");
            }

            if (result.RulesetId != RulesetId)
            {
                throw new VersusDomainException(
                    $"attempt {Id} is played under ruleset '{RulesetId.Value}' but the submitted result "
                    + $"was produced under '{result.RulesetId.Value}'");
            }

            if (result.RulesetVersion != RulesetVersion)
            {
                throw new VersusDomainException(
                    $"attempt {Id} is played under '{RulesetId.Value}' version {RulesetVersion} but the "
                    + $"submitted result was produced under version {result.RulesetVersion}");
            }

            State = AttemptState.Completed;
            Result = result;
            CompletedAtUtc = completedAtUtc;
            StartedAtUtc ??= completedAtUtc;
        }

        /// <summary>
        /// Gives the attempt up. Returns false when it was already abandoned; throws when it was
        /// completed, because a finished run cannot be taken back.
        /// </summary>
        public bool Abandon()
        {
            if (State == AttemptState.Abandoned)
            {
                return false;
            }

            if (State == AttemptState.Completed)
            {
                throw new VersusDomainException($"attempt {Id} is completed and cannot be abandoned");
            }

            State = AttemptState.Abandoned;
            return true;
        }

        private void RequireState(AttemptState expected, string operation)
        {
            if (State != expected)
            {
                throw new VersusDomainException(
                    $"attempt {Id} cannot {operation} from {State}; it must be {expected}");
            }
        }

        public override string ToString()
        {
            return $"attempt {Id} for {ParticipantId} (game {GameIndex + 1}, {State})";
        }
    }
}
