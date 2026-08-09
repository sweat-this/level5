using System;
using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Where a series is in its life.
    ///
    /// Every member here has a transition that reaches it and a transition that leaves it (or is
    /// terminal). States that only ever described a situation - "expired", "archived" - are not
    /// here: expiry needs a deadline this project does not have, and archiving is something a
    /// repository does to a completed series, not something the series becomes.
    /// </summary>
    public enum SeriesStatus
    {
        /// <summary>Offered, not yet taken up. A challenge is a series in this state.</summary>
        Invited = 0,

        /// <summary>Being played.</summary>
        Active = 1,

        /// <summary>Played out. Terminal.</summary>
        Completed = 2,

        /// <summary>Turned down before it started. Terminal.</summary>
        Declined = 3,

        /// <summary>Ended early because a participant gave it up. Terminal.</summary>
        Forfeited = 4
    }

    /// <summary>How a series ended.</summary>
    public enum SeriesOutcomeKind
    {
        Decided = 0,

        /// <summary>Every game played and the wins level. Rare, but reachable, so it has a name.</summary>
        Draw = 1,

        Forfeit = 2
    }

    /// <summary>The verdict on a series. Immutable, produced once, and the source of rivalry history.</summary>
    public sealed class SeriesResult
    {
        public SeriesResult(
            SeriesOutcomeKind kind,
            ParticipantId winnerId,
            SeriesScore score,
            DateTime completedAtUtc)
        {
            if (kind == SeriesOutcomeKind.Draw && winnerId.HasValue)
            {
                throw new VersusDomainException("a drawn series cannot have a winner");
            }

            if (kind != SeriesOutcomeKind.Draw && !winnerId.HasValue)
            {
                throw new VersusDomainException($"a {kind} series needs a winner");
            }

            Kind = kind;
            WinnerId = winnerId;
            Score = score;
            CompletedAtUtc = completedAtUtc;
        }

        public SeriesOutcomeKind Kind { get; }

        public ParticipantId WinnerId { get; }

        public SeriesScore Score { get; }

        public DateTime CompletedAtUtc { get; }

        public bool HasWinner => WinnerId.HasValue;

        /// <summary>A win with no games lost. Rivalry history cares; the domain does not.</summary>
        public bool IsSweep => Kind == SeriesOutcomeKind.Decided
            && (Score.FirstWins == 0 || Score.SecondWins == 0);

        public override string ToString()
        {
            return HasWinner ? $"{WinnerId} wins {Score}" : $"drawn {Score}";
        }
    }

    /// <summary>
    /// A best-of-N competition between two participants.
    ///
    /// This is the only thing that decides whether a series is over, and it decides it one way: a
    /// participant reaching the required wins, or the playlist running out. There is no separate
    /// path for best-of-three and best-of-seven, no separate path for local and correspondence, and
    /// no way for a caller to set the status directly - every field that matters is private and
    /// every change goes through an operation that checks its own invariants first.
    ///
    /// It knows nothing about scenes, input devices, networking or storage. A series restored from
    /// a file behaves identically to one created a moment ago, which is what makes a correspondence
    /// turn taken next week indistinguishable from one taken now.
    /// </summary>
    public sealed class VersusSeries
    {
        private readonly List<VersusGame> games = new List<VersusGame>();

        private VersusSeries(
            SeriesId id,
            SeriesSnapshot snapshot,
            VersusParticipants participants,
            SeriesStatus status,
            DateTime createdAtUtc,
            VersusMode mode)
        {
            Id = id;
            Snapshot = snapshot;
            Participants = participants;
            Status = status;
            CreatedAtUtc = createdAtUtc;
            Mode = mode;
        }

        public SeriesId Id { get; }

        /// <summary>The frozen competitive definition. Resolution never looks anywhere else.</summary>
        public SeriesSnapshot Snapshot { get; }

        public VersusParticipants Participants { get; }

        public SeriesStatus Status { get; private set; }

        public DateTime CreatedAtUtc { get; }

        /// <summary>Which kind of competition this is, which is what the rulesets had to support.</summary>
        public VersusMode Mode { get; }

        public SeriesResult Result { get; private set; }

        public DateTime? CompletedAtUtc => Result?.CompletedAtUtc;

        public IReadOnlyList<VersusGame> Games => games;

        public bool IsOver => Status == SeriesStatus.Completed
            || Status == SeriesStatus.Declined
            || Status == SeriesStatus.Forfeited;

        public bool IsActive => Status == SeriesStatus.Active;

        /// <summary>
        /// The game being played, or null when the series has not started or is over.
        ///
        /// Only ever one. Later games exist as objects but stay <see cref="VersusGameStatus.Pending"/>
        /// and never receive an attempt, which is what "games that are no longer necessary must not
        /// create attempts" means in practice.
        /// </summary>
        public VersusGame CurrentGame
        {
            get
            {
                foreach (VersusGame game in games)
                {
                    if (game.IsActive)
                    {
                        return game;
                    }
                }

                return null;
            }
        }

        /// <summary>Wins and draws, counted from the games rather than kept alongside them.</summary>
        public SeriesScore Score
        {
            get
            {
                int firstWins = 0;
                int secondWins = 0;
                int draws = 0;

                foreach (VersusGame game in games)
                {
                    GameResult result = game.Result;
                    if (result == null)
                    {
                        continue;
                    }

                    if (!result.HasWinner)
                    {
                        // A cancelled game is not a played game and does not belong in the score.
                        if (result.Kind == GameOutcomeKind.Draw)
                        {
                            draws++;
                        }

                        continue;
                    }

                    if (result.WinnerId == Participants.First.Id)
                    {
                        firstWins++;
                    }
                    else
                    {
                        secondWins++;
                    }
                }

                return new SeriesScore(firstWins, secondWins, draws);
            }
        }

        /// <summary>
        /// Creates a series and builds every game up front.
        ///
        /// All of them, including ones that will probably never be played. A pending game is cheap,
        /// and having the whole playlist present from the start means the format is fixed at
        /// creation instead of being extended a game at a time by whoever advances it.
        /// </summary>
        public static VersusSeries Create(
            SeriesId id,
            SeriesSnapshot snapshot,
            VersusParticipants participants,
            VersusMode mode,
            DateTime createdAtUtc,
            SeriesStatus initialStatus = SeriesStatus.Active)
        {
            if (!id.HasValue)
            {
                throw new VersusDomainException("a series needs an id");
            }

            if (snapshot == null)
            {
                throw new VersusDomainException("a series needs a snapshot of the rules it is played under");
            }

            if (participants == null)
            {
                throw new VersusDomainException("a series needs two participants");
            }

            if (initialStatus != SeriesStatus.Active && initialStatus != SeriesStatus.Invited)
            {
                throw new VersusDomainException($"a series cannot be created in the {initialStatus} state");
            }

            VersusSeries series = new VersusSeries(id, snapshot, participants, initialStatus, createdAtUtc, mode);

            for (int index = 0; index < snapshot.GameCount; index++)
            {
                series.games.Add(VersusGame.Create(
                    index,
                    snapshot.GameAt(index),
                    snapshot.InformationPolicy,
                    snapshot.FirstAttemptParticipantIndex(index)));
            }

            if (initialStatus == SeriesStatus.Active)
            {
                series.games[0].Activate();
            }

            return series;
        }

        /// <summary>Rebuilds a series from storage. Persistence only; runs no transitions.</summary>
        public static VersusSeries Restore(
            SeriesId id,
            SeriesSnapshot snapshot,
            VersusParticipants participants,
            VersusMode mode,
            SeriesStatus status,
            DateTime createdAtUtc,
            SeriesResult result,
            IEnumerable<VersusGame> storedGames)
        {
            VersusSeries series = new VersusSeries(id, snapshot, participants, status, createdAtUtc, mode)
            {
                Result = result
            };

            if (storedGames != null)
            {
                foreach (VersusGame game in storedGames)
                {
                    if (game != null)
                    {
                        series.games.Add(game);
                    }
                }
            }

            if (series.games.Count != snapshot.GameCount)
            {
                throw new VersusDomainException(
                    $"series {id} was stored with {series.games.Count} games but its snapshot describes "
                    + $"{snapshot.GameCount}, so the document is corrupt");
            }

            return series;
        }

        /// <summary>Takes up an invitation. The challenge becomes a live series.</summary>
        public void Accept()
        {
            RequireStatus(SeriesStatus.Invited, "accept");
            Status = SeriesStatus.Active;
            games[0].Activate();
        }

        /// <summary>Turns an invitation down. Terminal, and no games are ever played.</summary>
        public void Decline()
        {
            RequireStatus(SeriesStatus.Invited, "decline");
            Status = SeriesStatus.Declined;
        }

        /// <summary>
        /// Issues the next attempt for a participant, or returns the one already outstanding.
        ///
        /// Idempotent by design: a failed save, a double-tapped button and a crash between issuing
        /// and loading the scene all end up asking again, and none of them should mint a second
        /// attempt at the same game.
        /// </summary>
        public Attempt IssueAttempt(ParticipantId participantId, IVersusIdSource ids, IVersusClock clock)
        {
            RequireActive("issue an attempt");
            RequireParticipant(participantId);

            VersusGame game = CurrentGame;
            if (game == null)
            {
                throw new VersusDomainException($"series {Id} has no active game to attempt");
            }

            return game.IssueAttempt(participantId, Participants, ids, clock.UtcNow);
        }

        /// <summary>Whether this participant can attempt right now, and the reason when they cannot.</summary>
        public bool CanIssueAttempt(ParticipantId participantId, out string reason)
        {
            if (Status != SeriesStatus.Active)
            {
                reason = $"series {Id} is {Status}";
                return false;
            }

            if (!Participants.Contains(participantId))
            {
                reason = $"'{participantId}' is not a participant in series {Id}";
                return false;
            }

            VersusGame game = CurrentGame;
            if (game == null)
            {
                reason = $"series {Id} has no active game";
                return false;
            }

            return game.CanIssueTo(participantId, Participants, out reason);
        }

        /// <summary>Records that gameplay for an attempt has begun.</summary>
        public bool StartAttempt(AttemptId attemptId, IVersusClock clock)
        {
            RequireActive("start an attempt");
            VersusGame game = FindGameHolding(attemptId);
            return game.StartAttempt(attemptId, clock.UtcNow);
        }

        /// <summary>Gives an outstanding attempt up without forfeiting the game.</summary>
        public bool AbandonAttempt(AttemptId attemptId)
        {
            RequireActive("abandon an attempt");
            VersusGame game = FindGameHolding(attemptId);
            return game.AbandonAttempt(attemptId);
        }

        /// <summary>
        /// Submits a completed run and advances the series if that settled the game.
        ///
        /// This is the single write path for competitive progress. Nothing else moves a series
        /// forward, which is why there is nowhere else for a series to be advanced incorrectly.
        /// </summary>
        public SeriesSubmission SubmitResult(
            AttemptId attemptId,
            ParticipantId participantId,
            AttemptResult result,
            IVersusClock clock)
        {
            RequireActive("submit a result");
            RequireParticipant(participantId);

            VersusGame game = FindGameHolding(attemptId);
            DateTime now = clock.UtcNow;
            GameResult gameResult = game.SubmitResult(attemptId, participantId, result, Participants, now);

            if (gameResult == null)
            {
                return new SeriesSubmission(game, null, null);
            }

            SeriesResult seriesResult = Advance(now);
            return new SeriesSubmission(game, gameResult, seriesResult);
        }

        /// <summary>
        /// Ends the series in the opponent's favour. The current game is forfeited with it and any
        /// game that had not started is cancelled rather than left looking playable.
        /// </summary>
        public SeriesResult Forfeit(ParticipantId forfeitingParticipantId, IVersusClock clock)
        {
            RequireParticipant(forfeitingParticipantId);

            if (Status == SeriesStatus.Invited)
            {
                Decline();
                return null;
            }

            RequireActive("forfeit");

            DateTime now = clock.UtcNow;
            VersusGame current = CurrentGame;
            current?.Forfeit(forfeitingParticipantId, Participants, now);

            Status = SeriesStatus.Forfeited;
            Result = new SeriesResult(
                SeriesOutcomeKind.Forfeit,
                Participants.Opponent(forfeitingParticipantId).Id,
                Score,
                now);
            return Result;
        }

        /// <summary>
        /// What this participant is entitled to see about the whole series.
        ///
        /// Built out of the per-game views, so the sealed guarantee holds at series level for free
        /// rather than being re-implemented here.
        /// </summary>
        public ParticipantSeriesView ViewFor(ParticipantId viewerId)
        {
            RequireParticipant(viewerId);

            List<ParticipantGameView> gameViews = new List<ParticipantGameView>(games.Count);
            foreach (VersusGame game in games)
            {
                gameViews.Add(game.ViewFor(viewerId, Participants));
            }

            int viewerIndex = Participants.IndexOf(viewerId);
            SeriesScore score = Score;

            return new ParticipantSeriesView(
                Id,
                Snapshot.Format,
                Status,
                Mode,
                Snapshot.InformationPolicy,
                Participants.Find(viewerId),
                Participants.Opponent(viewerId),
                score.WinsFor(viewerIndex),
                score.WinsFor(1 - viewerIndex),
                score.Draws,
                gameViews,
                Result,
                CurrentGame?.Index ?? -1);
        }

        /// <summary>
        /// Decides whether the series is over and, if not, which game is next.
        ///
        /// The whole of early termination lives here. A participant on the required wins ends it
        /// immediately, so a 4-0 best of seven stops at four games and games five to seven are never
        /// activated. Running out of playlist ends it too - which is the only thing that stops a
        /// series full of draws from waiting forever for a win that cannot arrive.
        /// </summary>
        private SeriesResult Advance(DateTime nowUtc)
        {
            SeriesScore score = Score;
            int required = Snapshot.Format.RequiredWins;

            if (score.FirstWins >= required || score.SecondWins >= required)
            {
                return Complete(
                    new SeriesResult(
                        SeriesOutcomeKind.Decided,
                        score.FirstWins > score.SecondWins ? Participants.First.Id : Participants.Second.Id,
                        score,
                        nowUtc));
            }

            int nextIndex = -1;
            for (int index = 0; index < games.Count; index++)
            {
                if (games[index].Status == VersusGameStatus.Pending)
                {
                    nextIndex = index;
                    break;
                }
            }

            if (nextIndex < 0)
            {
                // Every game has been played and nobody reached the required wins, which can only
                // happen with draws in the playlist. Whoever won more games takes it; level is a
                // drawn series, and saying so is better than leaving it unfinishable.
                if (score.FirstWins == score.SecondWins)
                {
                    return Complete(new SeriesResult(SeriesOutcomeKind.Draw, ParticipantId.None, score, nowUtc));
                }

                return Complete(
                    new SeriesResult(
                        SeriesOutcomeKind.Decided,
                        score.FirstWins > score.SecondWins ? Participants.First.Id : Participants.Second.Id,
                        score,
                        nowUtc));
            }

            games[nextIndex].Activate();
            return null;
        }

        private SeriesResult Complete(SeriesResult result)
        {
            Result = result;
            Status = SeriesStatus.Completed;
            return result;
        }

        private VersusGame FindGameHolding(AttemptId attemptId)
        {
            foreach (VersusGame game in games)
            {
                if (game.Find(attemptId) != null)
                {
                    return game;
                }
            }

            throw new VersusDomainException($"series {Id} has no attempt {attemptId}");
        }

        private void RequireActive(string operation)
        {
            if (Status != SeriesStatus.Active)
            {
                throw new VersusDomainException(
                    $"series {Id} is {Status} and cannot {operation}");
            }
        }

        private void RequireStatus(SeriesStatus expected, string operation)
        {
            if (Status != expected)
            {
                throw new VersusDomainException(
                    $"series {Id} cannot {operation} from {Status}; it must be {expected}");
            }
        }

        private void RequireParticipant(ParticipantId participantId)
        {
            if (!Participants.Contains(participantId))
            {
                throw new VersusDomainException($"'{participantId}' is not a participant in series {Id}");
            }
        }

        public override string ToString()
        {
            return $"{Id} {Participants} ({Snapshot.Format}, {Status}, {Score})";
        }
    }

    /// <summary>
    /// What a submitted result did: which game took it, whether that settled the game, and whether
    /// that settled the series. Both outcome fields are null when the game is still waiting.
    /// </summary>
    public readonly struct SeriesSubmission
    {
        public SeriesSubmission(VersusGame game, GameResult gameResult, SeriesResult seriesResult)
        {
            Game = game;
            GameResult = gameResult;
            SeriesResult = seriesResult;
        }

        public VersusGame Game { get; }

        /// <summary>Null while the opponent's attempt is still outstanding.</summary>
        public GameResult GameResult { get; }

        /// <summary>Null unless this submission ended the series.</summary>
        public SeriesResult SeriesResult { get; }

        public bool ResolvedGame => GameResult != null;

        public bool CompletedSeries => SeriesResult != null;
    }
}
