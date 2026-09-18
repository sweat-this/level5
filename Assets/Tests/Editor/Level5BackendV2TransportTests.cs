using System;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2TransportTests
    {
        [SetUp]
        public void SetUp()
        {
            BackendV2SessionStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
        }

        [Test]
        public void ASuccessfulResponseDeserializesTheBody()
        {
            RawApiResponse raw = RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse);

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: false);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Value.PlayerId.ToString(), Is.EqualTo("8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"));
            Assert.That(response.Value.AccessToken, Is.EqualTo("eyJhbGciOiJIUzI1NiJ9.fake.token"));
        }

        [Test]
        public void ANoContentResponseSucceedsAsApiVoid()
        {
            RawApiResponse raw = RawApiResponse.Completed(204, string.Empty);

            ApiResponse<ApiVoid> response = ApiResponseMapper.Map<ApiVoid>(raw, requestRequiredAuth: true);

            Assert.That(response.Success, Is.True);
        }

        [TestCase(400, ApiErrorKind.Validation)]
        [TestCase(403, ApiErrorKind.Forbidden)]
        [TestCase(404, ApiErrorKind.NotFound)]
        [TestCase(409, ApiErrorKind.Conflict)]
        [TestCase(429, ApiErrorKind.RateLimited)]
        [TestCase(500, ApiErrorKind.ServerError)]
        [TestCase(503, ApiErrorKind.ServerError)]
        public void EachStatusFamilyIsClassifiedDistinctly(int status, ApiErrorKind expected)
        {
            RawApiResponse raw = RawApiResponse.Completed(status, BackendV2Fixtures.ProblemDetailsNotFound);

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: false);

            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorKind, Is.EqualTo(expected));
        }

        [Test]
        public void A401OnAnAuthenticatedRequestIsClassifiedAsExpired()
        {
            RawApiResponse raw = RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound);

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: true);

            Assert.That(response.ErrorKind, Is.EqualTo(ApiErrorKind.Expired));
        }

        [Test]
        public void A401OnAnUnauthenticatedRequestIsClassifiedAsUnauthenticated()
        {
            RawApiResponse raw = RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound);

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: false);

            Assert.That(response.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
        }

        [Test]
        public void ProblemDetailsFieldsArePreserved()
        {
            RawApiResponse raw = RawApiResponse.Completed(409, BackendV2Fixtures.ProblemDetailsConflict);

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: true);

            Assert.That(response.Problem.Status, Is.EqualTo(409));
            Assert.That(response.Problem.Code, Is.EqualTo("conflicting_attempt_result"));
            Assert.That(response.Problem.TraceId, Is.EqualTo("00-trace-5678-00"));
            Assert.That(response.Problem.Type, Is.EqualTo("https://level5.game/errors/conflicting_attempt_result"));
        }

        [Test]
        public void ANetworkFailureIsClassifiedAsNetwork()
        {
            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(RawApiResponse.NetworkError(), requestRequiredAuth: false);

            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorKind, Is.EqualTo(ApiErrorKind.Network));
            Assert.That(response.Problem, Is.Null);
        }

        [Test]
        public void ATimeoutIsClassifiedAsTimeout()
        {
            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(RawApiResponse.Timeout(), requestRequiredAuth: false);

            Assert.That(response.ErrorKind, Is.EqualTo(ApiErrorKind.Timeout));
        }

        [Test]
        public void MalformedJsonOnA2xxIsClassifiedAsMalformedResponse()
        {
            RawApiResponse raw = RawApiResponse.Completed(200, "{ not json");

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: false);

            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorKind, Is.EqualTo(ApiErrorKind.MalformedResponse));
        }

        [Test]
        public void ANonProblemBodyOnAFailureStillProducesASyntheticProblem()
        {
            RawApiResponse raw = RawApiResponse.Completed(500, "<html>gateway error</html>");

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: false);

            Assert.That(response.Problem, Is.Not.Null);
            Assert.That(response.Problem.Status, Is.EqualTo(500));
            Assert.That(response.Problem.Code, Is.Null);
        }

        [Test]
        public void RequestBodiesSerializeAsCamelCase()
        {
            string json = BackendV2Json.Serialize(new LoginRequestDto("ada", "hunter2"));

            Assert.That(json, Does.Contain("\"username\":\"ada\""));
            Assert.That(json, Does.Contain("\"password\":\"hunter2\""));
            Assert.That(json, Does.Not.Contain("\"Username\""));
        }

        [Test]
        public void ARawGuidBodyDeserializesCorrectly()
        {
            RawApiResponse raw = RawApiResponse.Completed(200, "\"8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a\"");

            ApiResponse<System.Guid> response = ApiResponseMapper.Map<System.Guid>(raw, requestRequiredAuth: true);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Value.ToString(), Is.EqualTo("8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"));
        }

        [Test]
        public void PlayersRequestsAreMarkedAsRequiringAuthAndAuthRequestsAreNot()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, "\"8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a\""));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));

            AuthApiClient authClient = new AuthApiClient(transport);
            PlayersApiClient playersClient = new PlayersApiClient(transport, new BackendV2SessionManager(authClient));

            CoroutineTestRunner.RunToCompletion(playersClient.GetMe(_ => { }));
            CoroutineTestRunner.RunToCompletion(authClient.Login(new LoginRequestDto("ada", "hunter2"), _ => { }));

            ApiRequest playersRequest = transport.Requests.Find(r => r.RelativePath == "api/v2/players/me");
            ApiRequest loginRequest = transport.Requests.Find(r => r.RelativePath == "api/v2/auth/login");

            Assert.That(playersRequest, Is.Not.Null);
            Assert.That(playersRequest.RequiresAuth, Is.True);
            Assert.That(loginRequest, Is.Not.Null);
            Assert.That(loginRequest.RequiresAuth, Is.False);
        }

        [Test]
        public void AnAuthorizedCallWithNoSessionFailsFastWithoutTouchingTheTransport()
        {
            BackendV2SessionStore.Clear();
            FakeApiTransport transport = new FakeApiTransport();
            PlayersApiClient playersClient = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(playersClient.GetMe(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(transport.Requests, Is.Empty, "an unauthenticated call must never reach the network");
        }

        [Test]
        public void CorrelationIdsAreUniquePerCall()
        {
            string first = CorrelationIdGenerator.NewId();
            string second = CorrelationIdGenerator.NewId();

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Is.Not.Empty);
        }

        [Test]
        public void TheCorrelationIdOnTheRawResponseFlowsIntoTheTypedResponse()
        {
            RawApiResponse raw = RawApiResponse.Completed(500, "boom", correlationId: "corr-123");

            ApiResponse<AccessTokenResponseDto> response =
                ApiResponseMapper.Map<AccessTokenResponseDto>(raw, requestRequiredAuth: false);

            Assert.That(response.CorrelationId, Is.EqualTo("corr-123"));
        }
    }
}
