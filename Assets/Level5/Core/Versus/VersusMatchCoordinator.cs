using System;
using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// The application-level entry point for everything competitive.
    ///
    /// Its job is narrow and it is kept narrow on purpose. Every method does the same four things:
    /// load the series, call one domain operation on it, save it, and announce what happened. It
    /// holds no series in a field, decides no rules, compares no results and knows nothing about
    /// scenes. A coordinator that starts making decisions is the God object this architecture exists
    /// to avoid, so anything that looks like a rule belongs in <see cref="VersusSeries"/>,
    /// <see cref="VersusGame"/> or <see cref="CompetitiveRuleset"/> instead.
    ///
    /// Saving after every mutation and holding nothing between calls is what makes correspondence
    /// work: the stored document is always the truth, so the application can stop at any point and
    /// the next session picks up exactly where it left off.
    /// </summary>
    public sealed class VersusMatchCoordinator
    {
        private readonly IVersusSeriesRepository repository;
        private readonly CompetitiveRulesetCatalog catalog;
        private readonly VersusSeriesValidator validator;
        private readonly IVersusClock clock;
        private readonly IVersusIdSource ids;

        public VersusMatchCoordinator(
            IVersusSeriesRepository repository,
            CompetitiveRulesetCatalog catalog,
            IVersusClock clock = null,
            IVersusIdSource ids = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.catalog = catalog ?? CompetitiveRulesetCatalog.Empty();
            this.clock = clock ?? SystemVersusClock.Instance;
            this.ids = ids ?? GuidVersusIdSource.Instance;
            validator = new VersusSeriesValidator(this.catalog);
        }

        /// <summary>Raised after a series has been created and stored.</summary>
        public event Action<VersusSeries> SeriesCreated;

        /// <summary>Raised after an attempt has been issued and stored.</summary>
        public event Action<VersusSeries, Attempt> AttemptIssued;

        /// <summary>Raised after gameplay for an attempt has been recorded as started.</summary>
        public event Action<VersusSeries, Attempt> AttemptStarted;

        /// <summary>Raised after a result has been accepted, whether or not it resolved the game.</summary>
        public event Action<VersusSeries, Attempt> AttemptCompleted;

        /// <summary>Raised when a game gets its verdict.</summary>
        public event Action<VersusSeries, VersusGame> GameResolved;

        /// <summary>Raised when the next game becomes the current one.</summary>
        public event Action<VersusSeries, VersusGame> SeriesAdvanced;

        /// <summary>Raised when a series ends, however it ended.</summary>
        public event Action<VersusSeries> SeriesCompleted;

        public CompetitiveRulesetCatalog Catalog => catalog;

        /// <summary>
        /// Creates a series from a request, or explains why it cannot exist.
        ///
        /// The series is stored before it is returned. A caller that got a series back can rely on
        /// it surviving the application closing a moment later, which is the assumption the whole
        /// correspondence flow rests on.
        /// </summary>
        public SeriesOperation CreateSeries(SeriesRequest request)
        {
            SeriesValidation validation = validator.Validate(request);
            if (!validation.Succeeded)
            {
                return SeriesOperation.Failure(validation.Validation);
            }

            VersusSeries series = VersusSeries.Create(
                ids.NewSeriesId(),
                validation.Snapshot,
                new VersusParticipants(request.Challenger, request.Opponent),
                request.Mode,
                clock.UtcNow,
                request.RequiresInvitation ? SeriesStatus.Invited : SeriesStatus.Active);

            if (!repository.Save(series))
            {
                return SeriesOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.PersistenceFailed,
                    "the series could not be saved, so it was not started"));
            }

            VersusLog.SeriesCreated(series, request);
            SeriesCreated?.Invoke(series);
            return SeriesOperation.Success(series);
        }

        public VersusSeries Load(SeriesId seriesId)
        {
            return repository.Load(seriesId);
        }

        public IReadOnlyList<SeriesSummary> ListSeries()
        {
            return repository.ListSummaries();
        }

        /// <summary>Every unfinished series a participant is in. The basis of a turn list.</summary>
        public List<SeriesSummary> ListActiveFor(ParticipantId participantId)
        {
            List<SeriesSummary> mine = new List<SeriesSummary>();
            foreach (SeriesSummary summary in repository.ListSummaries())
            {
                if (summary.Archived || !summary.Involves(participantId))
                {
                    continue;
                }

                if (summary.Status == SeriesStatus.Active || summary.Status == SeriesStatus.Invited)
                {
                    mine.Add(summary);
                }
            }

            return mine;
        }

        /// <summary>Takes up an invitation.</summary>
        public SeriesOperation AcceptChallenge(SeriesId seriesId)
        {
            return Mutate(seriesId, series => series.Accept());
        }

        /// <summary>Turns an invitation down.</summary>
        public SeriesOperation DeclineChallenge(SeriesId seriesId)
        {
            return Mutate(seriesId, series => series.Decline());
        }

        /// <summary>Gives a series up, handing it to the opponent.</summary>
        public SeriesOperation Forfeit(SeriesId seriesId, ParticipantId participantId)
        {
            SeriesOperation operation = Mutate(seriesId, series => series.Forfeit(participantId, clock));
            if (operation.Succeeded && operation.Series.IsOver)
            {
                VersusLog.SeriesCompleted(operation.Series);
                SeriesCompleted?.Invoke(operation.Series);
            }

            return operation;
        }

        /// <summary>
        /// Issues the next attempt for a participant, or hands back the one already outstanding.
        ///
        /// This is where a series that this build can no longer score is refused. Doing it here
        /// rather than at load time means an aged-out series can still be read and its history
        /// shown; only playing on is blocked.
        /// </summary>
        public AttemptOperation IssueAttempt(SeriesId seriesId, ParticipantId participantId)
        {
            VersusSeries series = repository.Load(seriesId);
            if (series == null)
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.SeriesNotFound,
                    $"there is no series '{seriesId}'"));
            }

            VersusValidationResult playable = validator.ValidatePlayable(series.Snapshot);
            if (!playable.IsValid)
            {
                return AttemptOperation.Failure(playable);
            }

            if (!series.CanIssueAttempt(participantId, out string reason))
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    series.IsActive ? VersusValidationCode.AttemptNotAvailable : VersusValidationCode.SeriesNotPlayable,
                    reason));
            }

            Attempt attempt;
            try
            {
                attempt = series.IssueAttempt(participantId, ids, clock);
            }
            catch (VersusDomainException exception)
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.AttemptNotAvailable,
                    exception.Message));
            }

            if (!repository.Save(series))
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.PersistenceFailed,
                    "the attempt could not be saved, so it was not started"));
            }

            VersusLog.AttemptIssued(series, attempt);
            AttemptIssued?.Invoke(series, attempt);
            return AttemptOperation.Success(series, attempt);
        }

        /// <summary>Records that gameplay for an attempt has begun.</summary>
        public AttemptOperation StartAttempt(SeriesId seriesId, AttemptId attemptId)
        {
            VersusSeries series = repository.Load(seriesId);
            if (series == null)
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.SeriesNotFound,
                    $"there is no series '{seriesId}'"));
            }

            try
            {
                series.StartAttempt(attemptId, clock);
            }
            catch (VersusDomainException exception)
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.AttemptNotAvailable,
                    exception.Message));
            }

            if (!repository.Save(series))
            {
                return AttemptOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.PersistenceFailed,
                    "the attempt could not be saved"));
            }

            Attempt attempt = FindAttempt(series, attemptId);
            VersusLog.AttemptStarted(series, attempt);
            AttemptStarted?.Invoke(series, attempt);
            return AttemptOperation.Success(series, attempt);
        }

        /// <summary>Gives an outstanding attempt up without conceding the game.</summary>
        public SeriesOperation AbandonAttempt(SeriesId seriesId, AttemptId attemptId)
        {
            return Mutate(seriesId, series => series.AbandonAttempt(attemptId));
        }

        /// <summary>
        /// Submits a finished run.
        ///
        /// The one write path for competitive progress, and the only place a game can resolve or a
        /// series advance. A result arriving for an attempt that is already complete, for the wrong
        /// participant, or under the wrong rules version comes back as a refusal rather than being
        /// quietly accepted - each of those is either a bug or somebody having a second go.
        /// </summary>
        public SubmissionOperation SubmitResult(
            SeriesId seriesId,
            AttemptId attemptId,
            ParticipantId participantId,
            AttemptResult result)
        {
            VersusSeries series = repository.Load(seriesId);
            if (series == null)
            {
                return SubmissionOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.SeriesNotFound,
                    $"there is no series '{seriesId}'"));
            }

            SeriesSubmission submission;
            try
            {
                submission = series.SubmitResult(attemptId, participantId, result, clock);
            }
            catch (VersusDomainException exception)
            {
                VersusLog.SubmissionRejected(seriesId, attemptId, exception.Message);
                return SubmissionOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.AttemptNotAvailable,
                    exception.Message));
            }

            if (!repository.Save(series))
            {
                // The domain object moved on but the document did not. Reporting failure lets the
                // caller retry from its own persisted state; nothing here caches the mutated series,
                // so the next load returns the last durable version rather than this one.
                return SubmissionOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.PersistenceFailed,
                    "the result could not be saved"));
            }

            Attempt attempt = FindAttempt(series, attemptId);
            VersusLog.AttemptCompleted(series, attempt);
            AttemptCompleted?.Invoke(series, attempt);

            if (submission.ResolvedGame)
            {
                VersusLog.GameResolved(series, submission.Game);
                GameResolved?.Invoke(series, submission.Game);
            }

            if (submission.CompletedSeries)
            {
                VersusLog.SeriesCompleted(series);
                SeriesCompleted?.Invoke(series);
            }
            else if (submission.ResolvedGame && series.CurrentGame != null)
            {
                VersusLog.SeriesAdvanced(series, series.CurrentGame);
                SeriesAdvanced?.Invoke(series, series.CurrentGame);
            }

            return SubmissionOperation.Success(series, submission);
        }

        /// <summary>Moves a completed series out of the active list, keeping it readable.</summary>
        public bool Archive(SeriesId seriesId)
        {
            return repository.Archive(seriesId);
        }

        /// <summary>
        /// Head-to-head history between two participants, folded from completed series.
        ///
        /// Recomputed on demand rather than stored. An aggregate kept alongside the series it
        /// summarises is an aggregate that will eventually disagree with them, and the series are
        /// the record of what actually happened.
        /// </summary>
        public RivalryRecord Rivalry(ParticipantId left, ParticipantId right)
        {
            List<VersusSeries> completed = new List<VersusSeries>();
            foreach (SeriesSummary summary in repository.ListSummaries())
            {
                if (summary.Status != SeriesStatus.Completed && summary.Status != SeriesStatus.Forfeited)
                {
                    continue;
                }

                if (!summary.Involves(left) || !summary.Involves(right))
                {
                    continue;
                }

                VersusSeries series = repository.Load(summary.Id);
                if (series != null)
                {
                    completed.Add(series);
                }
            }

            return RivalryRecord.Build(left, right, completed);
        }

        private SeriesOperation Mutate(SeriesId seriesId, Action<VersusSeries> operation)
        {
            VersusSeries series = repository.Load(seriesId);
            if (series == null)
            {
                return SeriesOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.SeriesNotFound,
                    $"there is no series '{seriesId}'"));
            }

            try
            {
                operation(series);
            }
            catch (VersusDomainException exception)
            {
                return SeriesOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.SeriesNotPlayable,
                    exception.Message));
            }

            if (!repository.Save(series))
            {
                return SeriesOperation.Failure(VersusValidationResult.Invalid(
                    VersusValidationCode.PersistenceFailed,
                    "the series could not be saved"));
            }

            return SeriesOperation.Success(series);
        }

        private static Attempt FindAttempt(VersusSeries series, AttemptId attemptId)
        {
            foreach (VersusGame game in series.Games)
            {
                Attempt attempt = game.Find(attemptId);
                if (attempt != null)
                {
                    return attempt;
                }
            }

            return null;
        }
    }

    /// <summary>The outcome of an operation on a series: the series, or the reasons there is none.</summary>
    public readonly struct SeriesOperation
    {
        private SeriesOperation(VersusSeries series, VersusValidationResult validation)
        {
            Series = series;
            Validation = validation;
        }

        public VersusSeries Series { get; }

        public VersusValidationResult Validation { get; }

        public bool Succeeded => Series != null;

        public static SeriesOperation Success(VersusSeries series)
        {
            return new SeriesOperation(series, VersusValidationResult.Valid());
        }

        public static SeriesOperation Failure(VersusValidationResult validation)
        {
            return new SeriesOperation(null, validation);
        }
    }

    /// <summary>The outcome of issuing or starting an attempt.</summary>
    public readonly struct AttemptOperation
    {
        private AttemptOperation(VersusSeries series, Attempt attempt, VersusValidationResult validation)
        {
            Series = series;
            Attempt = attempt;
            Validation = validation;
        }

        public VersusSeries Series { get; }

        public Attempt Attempt { get; }

        public VersusValidationResult Validation { get; }

        public bool Succeeded => Attempt != null;

        public static AttemptOperation Success(VersusSeries series, Attempt attempt)
        {
            return new AttemptOperation(series, attempt, VersusValidationResult.Valid());
        }

        public static AttemptOperation Failure(VersusValidationResult validation)
        {
            return new AttemptOperation(null, null, validation);
        }
    }

    /// <summary>The outcome of submitting a result: what it settled, if anything.</summary>
    public readonly struct SubmissionOperation
    {
        private SubmissionOperation(VersusSeries series, SeriesSubmission submission, VersusValidationResult validation)
        {
            Series = series;
            Submission = submission;
            Validation = validation;
        }

        public VersusSeries Series { get; }

        public SeriesSubmission Submission { get; }

        public VersusValidationResult Validation { get; }

        public bool Succeeded => Series != null;

        public bool ResolvedGame => Succeeded && Submission.ResolvedGame;

        public bool CompletedSeries => Succeeded && Submission.CompletedSeries;

        public static SubmissionOperation Success(VersusSeries series, SeriesSubmission submission)
        {
            return new SubmissionOperation(series, submission, VersusValidationResult.Valid());
        }

        public static SubmissionOperation Failure(VersusValidationResult validation)
        {
            return new SubmissionOperation(null, default, validation);
        }
    }
}
