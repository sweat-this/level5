using System;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="LeaderboardsApiClient"/>: request shape, route, auth requirement, query
    /// translation and response decoding for <c>GET api/v2/leaderboards/{modeId}</c>. The shared
    /// refresh-on-expiry/retry-once path is already exercised generically against
    /// <see cref="AuthenticatedApiClientBase"/>, so it is not repeated here.
    /// </summary>
    public class Level5BackendV2LeaderboardsClientTests
    {
        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
        }

        private static LeaderboardsApiClient AuthenticatedClient(FakeApiTransport transport)
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
            return new LeaderboardsApiClient(transport, new BackendV2SessionManager(new AuthApiClient(transport)));
        }

        [Test]
        public void GetPageHitsTheExactRouteAndRequiresAuth()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, null, null, null, null, _ => { }));

            Assert.That(transport.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.Requests[0].Method, Is.EqualTo(ApiHttpMethod.Get));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/leaderboards/3"));
            Assert.That(transport.Requests[0].RequiresAuth, Is.True);
        }

        [Test]
        public void GetPageAlwaysSendsTheLimitParameter()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, null, null, null, null, _ => { }));

            Assert.That(transport.Requests[0].Query["limit"], Is.EqualTo("10"));
        }

        [Test]
        public void FirstPageOmitsTheCursorParameterEntirely()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, null, null, null, null, _ => { }));

            Assert.That(transport.Requests[0].Query.ContainsKey("cursor"), Is.False);
        }

        [Test]
        public void ALaterCursorIsForwardedVerbatim()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);
            const string opaqueCursor = "opaque-leaderboard-cursor-page-2==weird/chars+here";

            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, opaqueCursor, null, null, null, null, _ => { }));

            Assert.That(transport.Requests[0].Query["cursor"], Is.EqualTo(opaqueCursor));
        }

        [Test]
        public void NullOptionalFiltersAreOmittedFromTheQuery()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, null, null, null, null, _ => { }));

            Assert.That(transport.Requests[0].Query.ContainsKey("hardcore"), Is.False);
            Assert.That(transport.Requests[0].Query.ContainsKey("traffic"), Is.False);
            Assert.That(transport.Requests[0].Query.ContainsKey("enemies"), Is.False);
            Assert.That(transport.Requests[0].Query.ContainsKey("sniper"), Is.False);
        }

        [Test]
        public void ExplicitlySuppliedFalseFiltersAreTransmitted()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, true, false, false, false, _ => { }));

            Assert.That(transport.Requests[0].Query["hardcore"], Is.EqualTo("true"));
            Assert.That(transport.Requests[0].Query["traffic"], Is.EqualTo("false"));
            Assert.That(transport.Requests[0].Query["enemies"], Is.EqualTo("false"));
            Assert.That(transport.Requests[0].Query["sniper"], Is.EqualTo("false"));
        }

        [Test]
        public void DecodesAllFieldsOfASuccessfulPage()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPage));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            ApiResponse<LeaderboardPageDto> result = null;
            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, null, null, null, null, r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.ModeId, Is.EqualTo(3));
            Assert.That(result.Value.Metric, Is.EqualTo("ShotsMade"));
            Assert.That(result.Value.Direction, Is.EqualTo("HigherWins"));
            Assert.That(result.Value.Limit, Is.EqualTo(10));
            Assert.That(result.Value.NextCursor, Is.EqualTo("opaque-leaderboard-cursor-page-2"));
            Assert.That(result.Value.Items, Has.Count.EqualTo(1));

            LeaderboardEntryDto entry = result.Value.Items[0];
            Assert.That(entry.MatchResultId, Is.EqualTo(Guid.Parse("6e45e5fa-6262-4d3f-8f5f-1baf5e4f7b8c")));
            Assert.That(entry.Player.DisplayName, Is.EqualTo("Ada"));
            Assert.That(entry.Player.Tag, Is.EqualTo("ADA#1234"));
            Assert.That(entry.CharacterId, Is.EqualTo("12"));
            Assert.That(entry.LevelId, Is.EqualTo(7));
            Assert.That(entry.Value, Is.EqualTo(42.0));
            Assert.That(entry.Modifiers.TrafficEnabled, Is.True);
            Assert.That(entry.Modifiers.Hardcore, Is.False);
        }

        [Test]
        public void ATerminalPageDecodesANullNextCursor()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.LeaderboardPageTerminal));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            ApiResponse<LeaderboardPageDto> result = null;
            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, "some-cursor", null, null, null, null, r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value.NextCursor, Is.Null);
            Assert.That(result.Value.Items, Is.Empty);
        }

        [Test]
        public void AnUnsupportedModeIsClassifiedAsValidationWithTheServerCode()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(400, BackendV2Fixtures.ProblemDetailsUnsupportedLeaderboardMode));
            LeaderboardsApiClient client = AuthenticatedClient(transport);

            ApiResponse<LeaderboardPageDto> result = null;
            CoroutineTestRunner.RunToCompletion(
                client.GetPage(27, 10, null, null, null, null, null, r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Validation));
            Assert.That(result.Problem.Code, Is.EqualTo("unsupported_leaderboard_mode"));
        }

        [Test]
        public void GetPageFailsFastWithNoSessionAndSendsNothing()
        {
            BackendV2SessionStore.Clear();
            FakeApiTransport transport = new FakeApiTransport();
            LeaderboardsApiClient client = new LeaderboardsApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<LeaderboardPageDto> result = null;
            CoroutineTestRunner.RunToCompletion(
                client.GetPage(3, 10, null, null, null, null, null, r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(transport.Requests, Is.Empty);
        }
    }
}
