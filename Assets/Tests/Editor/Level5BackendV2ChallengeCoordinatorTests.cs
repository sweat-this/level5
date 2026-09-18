using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2ChallengeCoordinatorTests
    {
        [SetUp]
        public void SetUp()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        }

        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
            BackendV2Runtime.Reset();
        }

        private static ChallengeFormState ValidForm()
        {
            return new ChallengeFormState
            {
                OpponentId = Guid.NewGuid(),
                RulesetId = "most-points",
                TotalGames = 3,
            };
        }

        [Test]
        public void CreateIsRefusedLocallyWithoutAnOpponent()
        {
            ChallengeCoordinator coordinator = new ChallengeCoordinator();
            ChallengeFormState form = new ChallengeFormState { RulesetId = "most-points" };

            ApiResponse<SeriesResponseDto> result = null;
            CoroutineTestRunner.RunToCompletion(coordinator.Create(form, r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Validation));
        }

        [Test]
        public void CreateSendsTheFormsClientRequestIdAndLeavesItForACallerDecidedRetry()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            ChallengeCoordinator coordinator = new ChallengeCoordinator();
            ChallengeFormState form = ValidForm();
            Guid idBeforeSend = form.GetOrBeginClientRequestId();

            ApiResponse<SeriesResponseDto> firstResult = null;
            CoroutineTestRunner.RunToCompletion(coordinator.Create(form, r => firstResult = r));

            Assert.That(firstResult.Success, Is.False);
            Assert.That(form.ClientRequestId, Is.EqualTo(idBeforeSend),
                "a transient failure must not be treated as definitive - the id stays for a retry");

            // The caller retries the same logical create after the network failure.
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            ApiResponse<SeriesResponseDto> secondResult = null;
            CoroutineTestRunner.RunToCompletion(coordinator.Create(form, r => secondResult = r));

            Assert.That(secondResult.Success, Is.True);
            Assert.That(transport.Requests, Has.Count.EqualTo(2));
            string firstBody = BackendV2Json.Serialize((CreateChallengeDto)transport.Requests[0].Body);
            string secondBody = BackendV2Json.Serialize((CreateChallengeDto)transport.Requests[1].Body);
            Assert.That(firstBody, Does.Contain(idBeforeSend.ToString()));
            Assert.That(secondBody, Does.Contain(idBeforeSend.ToString()),
                "retrying the same logical create must reuse the same clientRequestId");
        }

        [Test]
        public void AcceptSucceedsRefreshesTheOwnerListAndReleasesTheRowClaim()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail)); // Accept
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage)); // owner list refresh
            BackendV2Runtime.Override(transport);

            ChallengeCoordinator coordinator = new ChallengeCoordinator();
            SeriesListCoordinator incoming = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListIncoming);
            Guid seriesId = Guid.NewGuid();

            string error = "not called";
            CoroutineTestRunner.RunToCompletion(coordinator.Accept(seriesId, incoming, e => error = e));

            Assert.That(error, Is.Null);
            Assert.That(coordinator.Commands.IsInFlight(seriesId), Is.False);
            Assert.That(incoming.State.Items, Has.Count.EqualTo(1), "the owner list must be refreshed on success");
        }

        [Test]
        public void TheRowClaimIsStillHeldWhileTheOwnerListRefreshIsInFlight()
        {
            // The claim must cover the refresh too, not just the network command itself - otherwise
            // a rapid second tap could reach the server again for a row the UI hasn't shown as
            // resolved yet (see FriendsCoordinator's equivalent fix for the same reasoning).
            ChallengeCoordinator coordinator = new ChallengeCoordinator();
            Guid seriesId = Guid.NewGuid();
            List<bool> inFlightWhenEachRequestArrived = new List<bool>();

            FakeApiTransport transport = new FakeApiTransport();
            transport.Handler = request =>
            {
                inFlightWhenEachRequestArrived.Add(coordinator.Commands.IsInFlight(seriesId));
                return transport.Requests.Count == 1
                    ? RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail) // Accept
                    : RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage); // owner list refresh
            };
            BackendV2Runtime.Override(transport);

            SeriesListCoordinator incoming = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListIncoming);
            CoroutineTestRunner.RunToCompletion(coordinator.Accept(seriesId, incoming, _ => { }));

            Assert.That(inFlightWhenEachRequestArrived, Is.EqualTo(new[] { true, true }),
                "the claim must still be held for both the Accept call and the refresh it triggers");
            Assert.That(coordinator.Commands.IsInFlight(seriesId), Is.False, "released once everything settles");
        }

        [Test]
        public void ASecondConcurrentCommandForTheSameSeriesIsRefused()
        {
            ChallengeCoordinator coordinator = new ChallengeCoordinator();
            Guid seriesId = Guid.NewGuid();
            coordinator.Commands.TryBegin(seriesId);

            string error = null;
            CoroutineTestRunner.RunToCompletion(coordinator.Decline(seriesId, null, e => error = e));

            Assert.That(error, Is.Not.Null.And.Contains("already in progress"));
        }
    }
}
