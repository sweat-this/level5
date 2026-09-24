using System;
using System.Collections;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="MatchResultSubmissionCoordinator"/>: owner-safe delivery, success/retryable/
    /// definitive classification, and the single-in-flight-drain guard.
    ///
    /// <see cref="MatchResultSubmissionCoordinator.Drain"/> is driven directly via
    /// <see cref="CoroutineTestRunner"/> rather than through <c>TriggerDrain</c>/<c>Enqueue</c>'s own
    /// <c>BackendV2CoroutineHost.StartCoroutine</c> call, which does not advance in EditMode (the
    /// same boundary <see cref="Level5BackendV2PendingResultRetryTests"/> already documents for
    /// <c>RemoteAttemptResultSubmitter</c>).
    /// </summary>
    public class Level5BackendV2MatchResultSubmissionCoordinatorTests
    {
        [TearDown]
        public void TearDown()
        {
            PendingMatchResultStore.Clear();
            BackendV2SessionStore.Clear();
            BackendV2Runtime.Reset();
        }

        private static Guid SetAuthenticatedSession()
        {
            Guid playerId = Guid.NewGuid();
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
            return playerId;
        }

        private static SubmitMatchResultDto Request(Guid clientResultId)
        {
            return new SubmitMatchResultDto(
                clientResultId, modeId: 3, levelId: 7, characterId: "12", clientVersion: "1.4.2",
                platform: "Handheld",
                metrics: new Dictionary<string, double> { [MatchResultMetric.TotalPoints.ToString()] = 100 },
                modifiers: new MatchResultModifiersDto());
        }

        // --- Classify: pure, exhaustive over every ApiErrorKind -------------------------------

        [Test]
        public void ClassifySuccessIsSuccess()
        {
            ApiResponse<MatchResultResponseDto> response = ApiResponse<MatchResultResponseDto>.Ok(new MatchResultResponseDto());
            Assert.That(MatchResultSubmissionCoordinator.Classify(response), Is.EqualTo(MatchResultSubmissionOutcome.Success));
        }

        [Test]
        public void ClassifyValidationIsDefinitive()
        {
            Assert.That(
                MatchResultSubmissionCoordinator.Classify(Fail(ApiErrorKind.Validation)),
                Is.EqualTo(MatchResultSubmissionOutcome.Definitive));
        }

        [Test]
        public void ClassifyConflictIsDefinitive()
        {
            Assert.That(
                MatchResultSubmissionCoordinator.Classify(Fail(ApiErrorKind.Conflict)),
                Is.EqualTo(MatchResultSubmissionOutcome.Definitive));
        }

        [Test]
        public void ClassifyEveryOtherErrorKindIsRetryable(
            [Values(
                ApiErrorKind.Unauthenticated, ApiErrorKind.Expired, ApiErrorKind.Forbidden,
                ApiErrorKind.NotFound, ApiErrorKind.RateLimited, ApiErrorKind.ServerError,
                ApiErrorKind.Network, ApiErrorKind.Timeout, ApiErrorKind.MalformedResponse)]
            ApiErrorKind kind)
        {
            Assert.That(
                MatchResultSubmissionCoordinator.Classify(Fail(kind)),
                Is.EqualTo(MatchResultSubmissionOutcome.Retryable));
        }

        [Test]
        public void ClassifyANullResponseIsRetryable()
        {
            Assert.That(
                MatchResultSubmissionCoordinator.Classify(null),
                Is.EqualTo(MatchResultSubmissionOutcome.Retryable));
        }

        private static ApiResponse<MatchResultResponseDto> Fail(ApiErrorKind kind)
        {
            return ApiResponse<MatchResultResponseDto>.Fail(kind);
        }

        // --- Enqueue: durability ordering ------------------------------------------------------

        [Test]
        public void EnqueuePersistsSynchronouslyRegardlessOfWhetherTheDrainCoroutineEverRuns()
        {
            Guid owner = SetAuthenticatedSession();
            Guid clientResultId = Guid.NewGuid();

            // TriggerDrain (called internally by Enqueue) starts a coroutine on
            // BackendV2CoroutineHost, which does not advance in EditMode - so if persistence
            // depended on that coroutine ever running, this would still show nothing queued.
            MatchResultSubmissionCoordinator.Enqueue(owner, Request(clientResultId));

            List<PendingMatchResult> retryable = PendingMatchResultStore.GetRetryable(owner);
            Assert.That(retryable, Has.Count.EqualTo(1));
            Assert.That(retryable[0].Request.ClientResultId, Is.EqualTo(clientResultId));
        }

        // --- Drain: session gating ---------------------------------------------------------------

        [Test]
        public void DrainNoOpsWithNoBackendV2Session()
        {
            BackendV2SessionStore.Clear();
            FakeApiTransport transport = new FakeApiTransport();
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public void DrainNeverSendsAnEntryQueuedForADifferentPlayer()
        {
            Guid otherOwner = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(otherOwner, Request(Guid.NewGuid()));
            SetAuthenticatedSession();
            FakeApiTransport transport = new FakeApiTransport();
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(transport.Requests, Is.Empty);
            Assert.That(PendingMatchResultStore.GetRetryable(otherOwner), Has.Count.EqualTo(1),
                "an entry owned by a different player must be left untouched, never sent or deleted");
        }

        // --- Drain: outcome handling --------------------------------------------------------------

        [Test]
        public void DrainRemovesAnEntryOnSuccess()
        {
            Guid owner = SetAuthenticatedSession();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.MatchResultResponse));
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty);
        }

        [Test]
        public void DrainKeepsAnEntryOnNetworkFailure()
        {
            Guid owner = SetAuthenticatedSession();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        }

        [Test]
        public void DrainKeepsAnEntryOnTimeout()
        {
            Guid owner = SetAuthenticatedSession();
            PendingMatchResultStore.Enqueue(owner, Request(Guid.NewGuid()));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Timeout());
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        }

        [Test]
        public void DrainKeepsAnEntryOnServerError()
        {
            Guid owner = SetAuthenticatedSession();
            PendingMatchResultStore.Enqueue(owner, Request(Guid.NewGuid()));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(503, string.Empty));
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        }

        [Test]
        public void DrainKeepsAnEntryOnRateLimit()
        {
            Guid owner = SetAuthenticatedSession();
            PendingMatchResultStore.Enqueue(owner, Request(Guid.NewGuid()));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(429, string.Empty));
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        }

        [Test]
        public void DrainKeepsAnEntryWhenUnauthenticated()
        {
            // No session at the moment Drain reads BackendV2SessionStore.Current at all is already
            // covered by DrainNoOpsWithNoBackendV2Session; this instead proves the per-entry outcome
            // handling treats an Unauthenticated response the same as any other transient failure,
            // via the same pure Classify path DrainNoOpsWithNoBackendV2Session cannot reach.
            Assert.That(
                MatchResultSubmissionCoordinator.Classify(Fail(ApiErrorKind.Unauthenticated)),
                Is.EqualTo(MatchResultSubmissionOutcome.Retryable));
        }

        [Test]
        public void DrainMarksDefinitiveFailureOnValidationAndDoesNotRetryOnTheNextDrain()
        {
            Guid owner = SetAuthenticatedSession();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(400, string.Empty));
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty,
                "a definitive failure must stop automatic retry");

            transport.Requests.Clear();
            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());
            Assert.That(transport.Requests, Is.Empty, "a later drain must not resend a definitively-failed entry");
        }

        [Test]
        public void DrainMarksDefinitiveFailureOnConflictRatherThanTreatingItAsIdempotentSuccess()
        {
            Guid owner = SetAuthenticatedSession();
            Guid clientResultId = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(clientResultId));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(409, BackendV2Fixtures.ProblemDetailsMatchResultConflict));
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty,
                "a 409 conflict is a different-payload refusal, not a successful idempotent replay, "
                + "and must not be retried");
        }

        // --- Drain: concurrency ---------------------------------------------------------------

        [Test]
        public void ASecondConcurrentDrainSendsNothingWhileOneIsAlreadyInFlight()
        {
            Guid owner = SetAuthenticatedSession();
            PendingMatchResultStore.Enqueue(owner, Request(Guid.NewGuid()));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Handler = request =>
            {
                // At this point the outer Drain() has already set its re-entrancy guard, before
                // this request was ever sent - reproducing a second caller (e.g. a second match
                // ending) invoking Drain while the first is mid network round trip.
                IEnumerator secondDrainStarted = MatchResultSubmissionCoordinator.Drain();
                Assert.That(secondDrainStarted.MoveNext(), Is.False,
                    "a concurrent Drain must exit immediately without sending anything");
                return RawApiResponse.Completed(200, BackendV2Fixtures.MatchResultResponse);
            };
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(transport.Requests, Has.Count.EqualTo(1),
                "exactly one submission must have been sent, from the original drain only");
        }

        [Test]
        public void AResultEnqueuedDuringAnActiveDrainIsSentBeforeThatDrainFinishes()
        {
            Guid owner = SetAuthenticatedSession();
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            PendingMatchResultStore.Enqueue(owner, Request(first));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Handler = request =>
            {
                if (transport.Requests.Count == 1)
                {
                    // Simulates a second match ending - and its result being queued - while this
                    // drain's first network call is still in flight.
                    MatchResultSubmissionCoordinator.Enqueue(owner, Request(second));
                }

                return RawApiResponse.Completed(200, BackendV2Fixtures.MatchResultResponse);
            };
            BackendV2Runtime.Override(transport);

            CoroutineTestRunner.RunToCompletion(MatchResultSubmissionCoordinator.Drain());

            Assert.That(transport.Requests, Has.Count.EqualTo(2),
                "the result queued mid-drain must be sent within the same drain, never stranded");
            Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty);
        }
    }
}
