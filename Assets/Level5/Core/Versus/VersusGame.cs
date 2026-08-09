using System;
using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// How much a participant may learn about the opponent's run before both are finished.
    ///
    /// This belongs to the competition, not to a screen. A screen that decides to hide a number is
    /// one refactor away from showing it; a domain that never returns the number cannot.
    /// </summary>
    public enum InformationPolicy
    {
        /// <summary>
        /// Neither run is visible until both are finished, then both reveal together. The format
        /// the product is built around.
        /// </summary>
        SealedAttempt = 0,

        /// <summary>
        /// The first run finished sets a target the responder can see before starting. A different
        /// competition, not a friendlier presentation of the same one - the responder knows exactly
        /// what is needed and the first player does not.
        /// </summary>
        OpenTarget = 1
    }

    /// <summary>Where one game of a series is in its life.</summary>
    public enum VersusGameStatus
    {
        /// <summary>Not reached yet. May never be, if the series is decided first.</summary>
        Pending = 0,

        /// <summary>The current game. Attempts may be issued.</summary>
        Active = 1,

        /// <summary>Both attempts are in and a winner (or a draw) has been decided.</summary>
        Resolved = 2,

        /// <summary>Ended because a participant gave it up rather than by being played out.</summary>
        Forfeited = 3,

        /// <summary>Ended without a result and without blame - the series stopped around it.</summary>
        Cancelled = 4
    }

    /// <summary>How a game ended.</summary>
    public enum GameOutcomeKind
    {
        Decided = 0,
        Draw = 1,
        Forfeit = 2,
        Cancelled = 3
    }

    /// <summary>The verdict on one game. Immutable, and produced exactly once per game.</summary>
    public sealed class GameResult
    {
        public GameResult(GameOutcomeKind kind, ParticipantId winnerId, DateTime resolvedAtUtc)
        {
            if (kind == GameOutcomeKind.Decided && !winnerId.HasValue)
            {
                throw new VersusDomainException("a decided game needs a winner");
            }

            if (kind == GameOutcomeKind.Draw && winnerId.HasValue)
            {
                throw new VersusDomainException("a drawn game cannot have a winner");
            }

            Kind = kind;
            WinnerId = winnerId;
            ResolvedAtUtc = resolvedAtUtc;
        }

        public GameOutcomeKind Kind { get; }

        /// <summary>The winner, or <see cref="ParticipantId.None"/> for a draw or a cancellation.</summary>
        public ParticipantId WinnerId { get; }

        public DateTime ResolvedAtUtc { get; }

        /// <summary>Whether this game contributes a win to somebody's series score.</summary>
        public bool HasWinner => WinnerId.HasValue;

        public override string ToString()
        {
            return HasWinner ? $"{Kind}: {WinnerId}" : Kind.ToString();
        }
    }

    /// <summary>
    /// One contest inside a series: one ruleset, two attempts, one verdict.
    ///
    /// The attempts are private and there is no accessor that returns them. Everything a screen can
    /// ask goes through <see cref="ViewFor"/>, which answers as that participant and omits what
    /// they are not entitled to. That is what makes a sealed attempt sealed: not a rule somebody
    /// has to remember, but the absence of any method that would leak it.
    ///
    /// The ruleset held here is the series' frozen copy, never the catalog's current one, so
    /// re-balancing a mode cannot change a game that is already under way.
    /// </summary>
    public sealed class VersusGame
    {
        private readonly List<Attempt> attempts = new List<Attempt>();

        private VersusGame(
            int index,
            CompetitiveRuleset ruleset,
            InformationPolicy informationPolicy,
            int firstAttemptParticipantIndex,
            VersusGameStatus status,
            GameResult result)
        {
            Index = index;
            Ruleset = ruleset;
            InformationPolicy = informationPolicy;
            FirstAttemptParticipantIndex = firstAttemptParticipantIndex;
            Status = status;
            Result = result;
        }

        /// <summary>Zero-based position in the series playlist.</summary>
        public int Index { get; }

        /// <summary>Human-facing position: game 1, game 2.</summary>
        public int Number => Index + 1;

        /// <summary>The frozen rules for this game.</summary>
        public CompetitiveRuleset Ruleset { get; }

        public InformationPolicy InformationPolicy { get; }

        /// <summary>
        /// Which participant position attempts first. Only binding under
        /// <see cref="InformationPolicy.OpenTarget"/>, where going first is a real disadvantage and
        /// alternating it is the only fair arrangement.
        /// </summary>
        public int FirstAttemptParticipantIndex { get; }

        public VersusGameStatus Status { get; private set; }

        public GameResult Result { get; private set; }

        public bool IsResolved => Status == VersusGameStatus.Resolved
            || Status == VersusGameStatus.Forfeited
            || Status == VersusGameStatus.Cancelled;

        public bool IsActive => Status == VersusGameStatus.Active;

        public static VersusGame Create(
            int index,
            CompetitiveRuleset ruleset,
            InformationPolicy informationPolicy,
            int firstAttemptParticipantIndex)
        {
            if (ruleset == null)
            {
                throw new VersusDomainException($"game {index + 1} needs a ruleset");
            }

            return new VersusGame(
                index,
                ruleset,
                informationPolicy,
                firstAttemptParticipantIndex,
                VersusGameStatus.Pending,
                null);
        }

        /// <summary>Rebuilds a game from storage. Persistence only; runs no transitions.</summary>
        public static VersusGame Restore(
            int index,
            CompetitiveRuleset ruleset,
            InformationPolicy informationPolicy,
            int firstAttemptParticipantIndex,
            VersusGameStatus status,
            GameResult result,
            IEnumerable<Attempt> storedAttempts)
        {
            VersusGame game = new VersusGame(
                index,
                ruleset,
                informationPolicy,
                firstAttemptParticipantIndex,
                status,
                result);

            if (storedAttempts != null)
            {
                foreach (Attempt attempt in storedAttempts)
                {
                    if (attempt != null)
                    {
                        game.attempts.Add(attempt);
                    }
                }
            }

            return game;
        }

        /// <summary>Makes this the current game. Called by the series when it advances.</summary>
        internal void Activate()
        {
            if (Status == VersusGameStatus.Active)
            {
                return;
            }

            if (Status != VersusGameStatus.Pending)
            {
                throw new VersusDomainException($"game {Number} cannot be activated from {Status}");
            }

            Status = VersusGameStatus.Active;
        }

        /// <summary>
        /// Whether an attempt can be issued to this participant right now, and why not if it cannot.
        ///
        /// Under <see cref="InformationPolicy.OpenTarget"/> the second participant has to wait for
        /// the first to finish, otherwise there is no target and the format is just a sealed attempt
        /// wearing its name.
        /// </summary>
        internal bool CanIssueTo(ParticipantId participantId, VersusParticipants participants, out string reason)
        {
            reason = null;

            if (Status != VersusGameStatus.Active)
            {
                reason = $"game {Number} is {Status}, so no attempt can be issued";
                return false;
            }

            if (FindCompleted(participantId) != null)
            {
                reason = $"{participantId} has already completed game {Number}";
                return false;
            }

            if (InformationPolicy != InformationPolicy.OpenTarget)
            {
                return true;
            }

            bool isDesignatedFirst = participants.IndexOf(participantId) == FirstAttemptParticipantIndex;
            if (isDesignatedFirst)
            {
                return true;
            }

            ParticipantId opponent = participants.Opponent(participantId).Id;
            if (FindCompleted(opponent) == null)
            {
                reason = $"game {Number} is an open-target game and {opponent} has not set the target yet";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Issues an attempt, or hands back the one already outstanding.
        ///
        /// Reissuing matters more than it looks. A save that failed after the attempt was created, a
        /// double-tapped button, or the application dying between "issued" and "scene loaded" would
        /// otherwise each leave a stranded attempt and mint a second one. Returning the live attempt
        /// makes issuing idempotent, which is what lets the caller retry freely.
        /// </summary>
        internal Attempt IssueAttempt(
            ParticipantId participantId,
            VersusParticipants participants,
            IVersusIdSource ids,
            DateTime nowUtc)
        {
            if (!CanIssueTo(participantId, participants, out string reason))
            {
                throw new VersusDomainException(reason);
            }

            Attempt existing = FindLive(participantId);
            if (existing != null)
            {
                existing.MarkReady();
                return existing;
            }

            Attempt attempt = Attempt.Issue(
                ids.NewAttemptId(),
                participantId,
                Index,
                Ruleset.Id,
                Ruleset.Version,
                nowUtc);
            attempt.MarkReady();
            attempts.Add(attempt);
            return attempt;
        }

        /// <summary>
        /// Records a result against an outstanding attempt, then resolves the game if both are in.
        ///
        /// Returns the result when this submission resolved the game, or null when it is still
        /// waiting for the opponent.
        /// </summary>
        internal GameResult SubmitResult(
            AttemptId attemptId,
            ParticipantId participantId,
            AttemptResult result,
            VersusParticipants participants,
            DateTime nowUtc)
        {
            if (Status != VersusGameStatus.Active)
            {
                throw new VersusDomainException(
                    $"game {Number} is {Status} and cannot accept a result");
            }

            Attempt attempt = Find(attemptId);
            if (attempt == null)
            {
                throw new VersusDomainException($"game {Number} has no attempt {attemptId}");
            }

            if (attempt.ParticipantId != participantId)
            {
                // The submitting participant is not the one the attempt was issued to. Under
                // correspondence that is somebody submitting on the opponent's behalf.
                throw new VersusDomainException(
                    $"attempt {attemptId} belongs to {attempt.ParticipantId}, not {participantId}");
            }

            attempt.Complete(result, nowUtc);

            return TryResolve(participants, nowUtc);
        }

        /// <summary>Marks the run started, so an interrupted attempt is distinguishable from an untouched one.</summary>
        internal bool StartAttempt(AttemptId attemptId, DateTime nowUtc)
        {
            Attempt attempt = Find(attemptId);
            if (attempt == null)
            {
                throw new VersusDomainException($"game {Number} has no attempt {attemptId}");
            }

            return attempt.Start(nowUtc);
        }

        internal bool AbandonAttempt(AttemptId attemptId)
        {
            Attempt attempt = Find(attemptId);
            if (attempt == null)
            {
                throw new VersusDomainException($"game {Number} has no attempt {attemptId}");
            }

            return attempt.Abandon();
        }

        /// <summary>
        /// Resolves the game when both participants have finished, otherwise leaves it alone.
        ///
        /// Resolution happens once. A game already carrying a result is never recomputed, so a
        /// replayed submission cannot change a verdict that a series score has already counted.
        /// </summary>
        private GameResult TryResolve(VersusParticipants participants, DateTime nowUtc)
        {
            Attempt first = FindCompleted(participants.First.Id);
            Attempt second = FindCompleted(participants.Second.Id);
            if (first == null || second == null)
            {
                return null;
            }

            int comparison = Ruleset.Compare(first.Result, second.Result);
            GameResult result = comparison == 0
                ? new GameResult(GameOutcomeKind.Draw, ParticipantId.None, nowUtc)
                : new GameResult(
                    GameOutcomeKind.Decided,
                    comparison > 0 ? participants.First.Id : participants.Second.Id,
                    nowUtc);

            Result = result;
            Status = VersusGameStatus.Resolved;
            return result;
        }

        /// <summary>Ends the game in the opponent's favour because this participant gave it up.</summary>
        internal GameResult Forfeit(ParticipantId forfeitingParticipantId, VersusParticipants participants, DateTime nowUtc)
        {
            if (IsResolved)
            {
                throw new VersusDomainException($"game {Number} is already {Status} and cannot be forfeited");
            }

            foreach (Attempt attempt in attempts)
            {
                if (attempt.IsLive)
                {
                    attempt.Abandon();
                }
            }

            Result = new GameResult(
                GameOutcomeKind.Forfeit,
                participants.Opponent(forfeitingParticipantId).Id,
                nowUtc);
            Status = VersusGameStatus.Forfeited;
            return Result;
        }

        /// <summary>Ends the game with no verdict, because the series stopped around it.</summary>
        internal void Cancel(DateTime nowUtc)
        {
            if (IsResolved || Status == VersusGameStatus.Pending)
            {
                return;
            }

            foreach (Attempt attempt in attempts)
            {
                if (attempt.IsLive)
                {
                    attempt.Abandon();
                }
            }

            Result = new GameResult(GameOutcomeKind.Cancelled, ParticipantId.None, nowUtc);
            Status = VersusGameStatus.Cancelled;
        }

        /// <summary>
        /// What this participant is allowed to know about this game.
        ///
        /// The only read path. Note what it does not take: a "show everything" flag, a debug
        /// override, an admin mode. Adding one would put the sealed guarantee back into the hands of
        /// whoever calls it.
        /// </summary>
        public ParticipantGameView ViewFor(ParticipantId viewerId, VersusParticipants participants)
        {
            if (!participants.Contains(viewerId))
            {
                throw new VersusDomainException($"'{viewerId}' is not a participant in this series");
            }

            ParticipantId opponentId = participants.Opponent(viewerId).Id;
            Attempt own = FindLatest(viewerId);
            Attempt opponent = FindLatest(opponentId);

            bool revealed = Status == VersusGameStatus.Resolved
                || Status == VersusGameStatus.Forfeited;

            float? target = null;
            if (!revealed
                && InformationPolicy == InformationPolicy.OpenTarget
                && opponent != null
                && opponent.IsCompleted)
            {
                // Only the primary metric, only under open target. Handing over the whole result
                // would leak accuracy, shot count and completion time - none of which the format
                // says the responder is entitled to.
                target = opponent.Result.Get(Ruleset.PrimaryMetric);
            }

            return new ParticipantGameView(
                Index,
                Ruleset.Id,
                Ruleset.Version,
                Status,
                InformationPolicy,
                own == null ? AttemptState.Created : own.State,
                own != null && own.IsCompleted ? own.Result : null,
                own == null ? AttemptId.None : own.Id,
                opponent == null ? AttemptState.Created : opponent.State,
                revealed && opponent != null && opponent.IsCompleted ? opponent.Result : null,
                revealed ? Result : null,
                target,
                Ruleset.PrimaryMetric);
        }

        /// <summary>Every attempt on this game, for the persistence layer only.</summary>
        internal IReadOnlyList<Attempt> AttemptsForPersistence => attempts;

        /// <summary>The attempt currently outstanding for a participant, if any.</summary>
        internal Attempt FindLive(ParticipantId participantId)
        {
            foreach (Attempt attempt in attempts)
            {
                if (attempt.ParticipantId == participantId && attempt.IsLive)
                {
                    return attempt;
                }
            }

            return null;
        }

        internal Attempt FindCompleted(ParticipantId participantId)
        {
            foreach (Attempt attempt in attempts)
            {
                if (attempt.ParticipantId == participantId && attempt.IsCompleted)
                {
                    return attempt;
                }
            }

            return null;
        }

        internal Attempt Find(AttemptId attemptId)
        {
            foreach (Attempt attempt in attempts)
            {
                if (attempt.Id == attemptId)
                {
                    return attempt;
                }
            }

            return null;
        }

        /// <summary>The completed attempt if there is one, otherwise the live one.</summary>
        private Attempt FindLatest(ParticipantId participantId)
        {
            return FindCompleted(participantId) ?? FindLive(participantId);
        }

        public override string ToString()
        {
            return $"game {Number} ({Ruleset.Id.Value}, {Status})";
        }
    }
}
