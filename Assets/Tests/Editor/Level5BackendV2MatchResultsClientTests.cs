using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="MatchResultsApiClient"/>: request shape, route, auth requirement and response
    /// decoding for <c>POST api/v2/match-results</c>. The shared refresh-on-expiry/retry-once path
    /// is already exercised generically by <see cref="Level5BackendV2AuthRetryTests"/> against
    /// <see cref="AuthenticatedApiClientBase"/>, so it is not repeated here.
    /// </summary>
    public class Level5BackendV2MatchResultsClientTests
    {
        private static readonly Guid ClientResultId = Guid.Parse("9c1e2d3f-4a5b-4c6d-8e7f-0a1b2c3d4e5f");

        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
        }

        private static MatchResultsApiClient AuthenticatedClient(FakeApiTransport transport)
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
            return new MatchResultsApiClient(transport, new BackendV2SessionManager(new AuthApiClient(transport)));
        }

        private static SubmitMatchResultDto Request()
        {
            return new SubmitMatchResultDto(
                ClientResultId,
                modeId: 3,
                levelId: 7,
                characterId: "12",
                clientVersion: "1.4.2",
                platform: "Handheld",
                metrics: new Dictionary<string, double>
                {
                    [MatchResultMetric.TotalPoints.ToString()] = 120.0,
                    [MatchResultMetric.ShotsMade.ToString()] = 18.0,
                    [MatchResultMetric.TotalDistance.ToString()] = 342.5,
                    [MatchResultMetric.CompletionTimeSeconds.ToString()] = 95.2,
                    [MatchResultMetric.LongestStreak.ToString()] = 6.0,
                    [MatchResultMetric.EnemiesKilled.ToString()] = 4.0,
                },
                modifiers: new MatchResultModifiersDto { TrafficEnabled = true, EnemiesEnabled = true });
        }

        [Test]
        public void SubmitPostsToTheExactMatchResultsRoute()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.MatchResultResponse));
            MatchResultsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(client.Submit(Request(), _ => { }));

            Assert.That(transport.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.Requests[0].Method, Is.EqualTo(ApiHttpMethod.Post));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/match-results"));
            Assert.That(transport.Requests[0].RequiresAuth, Is.True);
        }

        [Test]
        public void SubmitSerializesEveryFieldOfTheRequest()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.MatchResultResponse));
            MatchResultsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(client.Submit(Request(), _ => { }));

            string body = BackendV2Json.Serialize((SubmitMatchResultDto)transport.Requests[0].Body);
            Assert.That(body, Does.Contain("\"clientResultId\":\"" + ClientResultId + "\""));
            Assert.That(body, Does.Contain("\"modeId\":3"));
            Assert.That(body, Does.Contain("\"levelId\":7"));
            Assert.That(body, Does.Contain("\"characterId\":\"12\""));
            Assert.That(body, Does.Contain("\"clientVersion\":\"1.4.2\""));
            Assert.That(body, Does.Contain("\"platform\":\"Handheld\""));
            // Dictionary keys are never camelCased, unlike this DTO's own declared property names
            // above - see BackendV2Json's own doc comment. MatchResultMetric.ToString() (PascalCase)
            // is exactly what Backend V2's MatchResultsController.MetricsByName name lookup expects.
            Assert.That(body, Does.Contain("\"TotalPoints\":120"));
            Assert.That(body, Does.Contain("\"ShotsMade\":18"));
            Assert.That(body, Does.Contain("\"TotalDistance\":342.5"));
            Assert.That(body, Does.Contain("\"CompletionTimeSeconds\":95.2"));
            Assert.That(body, Does.Contain("\"LongestStreak\":6"));
            Assert.That(body, Does.Contain("\"EnemiesKilled\":4"));
            Assert.That(body, Does.Contain("\"trafficEnabled\":true"));
            Assert.That(body, Does.Contain("\"enemiesEnabled\":true"));
            // PlayerId is never part of the outgoing payload - Backend V2 derives ownership from the
            // authenticated principal, never from client-supplied data.
            Assert.That(body, Does.Not.Contain("playerId"));
        }

        [Test]
        public void SubmitDecodesASuccessfulResponse()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.MatchResultResponse));
            MatchResultsApiClient client = AuthenticatedClient(transport);

            ApiResponse<MatchResultResponseDto> result = null;
            CoroutineTestRunner.RunToCompletion(client.Submit(Request(), r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.ClientResultId, Is.EqualTo(ClientResultId));
            Assert.That(result.Value.ModeId, Is.EqualTo(3));
            Assert.That(result.Value.Metrics["TotalPoints"], Is.EqualTo(120.0));
            Assert.That(result.Value.Modifiers.TrafficEnabled, Is.True);
        }

        [Test]
        public void SubmitClassifiesA409AsConflict()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(409, BackendV2Fixtures.ProblemDetailsMatchResultConflict));
            MatchResultsApiClient client = AuthenticatedClient(transport);

            ApiResponse<MatchResultResponseDto> result = null;
            CoroutineTestRunner.RunToCompletion(client.Submit(Request(), r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Conflict));
        }

        [Test]
        public void SubmitFailsFastWithNoSessionAndSendsNothing()
        {
            BackendV2SessionStore.Clear();
            FakeApiTransport transport = new FakeApiTransport();
            MatchResultsApiClient client = new MatchResultsApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<MatchResultResponseDto> result = null;
            CoroutineTestRunner.RunToCompletion(client.Submit(Request(), r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public void SubmitIsRefusedLocallyWithAnEmptyClientResultId()
        {
            FakeApiTransport transport = new FakeApiTransport();
            MatchResultsApiClient client = AuthenticatedClient(transport);
            SubmitMatchResultDto request = new SubmitMatchResultDto(
                Guid.Empty, 3, 7, "12", "1.4.2", "Handheld", new Dictionary<string, double>(), new MatchResultModifiersDto());

            ApiResponse<MatchResultResponseDto> result = null;
            CoroutineTestRunner.RunToCompletion(client.Submit(request, r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Validation));
            Assert.That(transport.Requests, Is.Empty, "an invalid submission must never reach the network");
        }
    }
}
