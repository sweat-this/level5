using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2CorrespondenceClientTests
    {
        [SetUp]
        public void SetUp()
        {
            // An authenticated, non-expiring session: these tests exercise what a client sends once
            // authorized, not the separate "not authenticated at all" fail-fast path.
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        }

        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
        }

        private static CorrespondenceApiClient Client(FakeApiTransport transport)
        {
            return new CorrespondenceApiClient(transport, new BackendV2SessionManager(new AuthApiClient(transport)));
        }

        [Test]
        public void CreateChallengeIsRefusedLocallyWithoutAClientRequestId()
        {
            FakeApiTransport transport = new FakeApiTransport();
            CorrespondenceApiClient client = Client(transport);

            ApiResponse<SeriesResponseDto> result = null;
            CreateChallengeDto request = new CreateChallengeDto(
                Guid.NewGuid(), 3, "most-points", clientRequestId: Guid.Empty);

            CoroutineTestRunner.RunToCompletion(client.CreateChallenge(request, r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Validation));
            Assert.That(transport.Requests, Is.Empty, "an invalid create must never reach the network");
        }

        [Test]
        public void CreateChallengeSendsTheSameClientRequestIdOnEveryAttempt()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            CorrespondenceApiClient client = Client(transport);

            Guid clientRequestId = ClientRequestIdGenerator.NewId();
            CreateChallengeDto request = new CreateChallengeDto(Guid.NewGuid(), 3, "most-points", clientRequestId);

            CoroutineTestRunner.RunToCompletion(client.CreateChallenge(request, _ => { }));
            // Retried with the *same* dto/request object after the network failure above, exactly
            // as a caller should: a fresh id here would turn a safe retry into a duplicate create.
            CoroutineTestRunner.RunToCompletion(client.CreateChallenge(request, _ => { }));

            Assert.That(transport.Requests, Has.Count.EqualTo(2));
            string firstBody = BackendV2Json.Serialize(((CreateChallengeDto)transport.Requests[0].Body));
            string secondBody = BackendV2Json.Serialize(((CreateChallengeDto)transport.Requests[1].Body));
            Assert.That(firstBody, Does.Contain(clientRequestId.ToString()));
            Assert.That(secondBody, Does.Contain(clientRequestId.ToString()));
        }

        [Test]
        public void ListIncomingParsesItemsLimitAndTreatsCursorAsOpaque()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage));
            CorrespondenceApiClient client = Client(transport);

            ApiResponse<SeriesSummaryPageDto> result = null;
            CoroutineTestRunner.RunToCompletion(client.ListIncoming(20, null, r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.Items, Has.Count.EqualTo(1));
            Assert.That(result.Value.Limit, Is.EqualTo(20));
            Assert.That(result.Value.NextCursor, Is.EqualTo("opaque-cursor-token"));

            // The cursor must be forwarded exactly as received, never parsed or reconstructed.
            transport.Requests.Clear();
            CoroutineTestRunner.RunToCompletion(
                client.ListIncoming(20, result.Value.NextCursor, _ => { }));
            Assert.That(transport.Requests[0].Query["cursor"], Is.EqualTo("opaque-cursor-token"));
        }

        [Test]
        public void StartAttemptParsesTheDescriptor()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.AttemptDescriptor));
            CorrespondenceApiClient client = Client(transport);

            ApiResponse<AttemptDescriptorDto> result = null;
            CoroutineTestRunner.RunToCompletion(
                client.StartAttempt(Guid.NewGuid(), 1, r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.RulesetId, Is.EqualTo("most-points"));
            Assert.That(result.Value.RequiredResultMetrics, Is.EquivalentTo(new[] { "Score" }));
            Assert.That(result.Value.ComparisonKeys[0].Metric, Is.EqualTo("Score"));
            Assert.That(result.Value.ComparisonKeys[0].Direction, Is.EqualTo("HigherWins"));
        }

        [Test]
        public void CompleteAttemptSendsOnlyNamedMetrics()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            CorrespondenceApiClient client = Client(transport);

            IReadOnlyDictionary<string, double> metrics = new Dictionary<string, double> { ["Score"] = 42.0 };
            CoroutineTestRunner.RunToCompletion(
                client.CompleteAttempt(Guid.NewGuid(), 1, Guid.NewGuid(), metrics, _ => { }));

            string sentBody = BackendV2Json.Serialize(transport.Requests[0].Body);
            Assert.That(sentBody, Does.Contain("\"metrics\""));
            Assert.That(sentBody, Does.Not.Contain("winner"));
            Assert.That(sentBody, Does.Not.Contain("revision"));
            Assert.That(sentBody, Does.Not.Contain("currentGame"));
        }
    }
}
