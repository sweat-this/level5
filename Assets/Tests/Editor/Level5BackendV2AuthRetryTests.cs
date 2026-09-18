using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// The 401-Expired -&gt; refresh -&gt; retry-once path in <see cref="AuthenticatedApiClientBase"/> -
    /// the riskiest piece of client logic in the whole boundary, and previously untested.
    /// </summary>
    public class Level5BackendV2AuthRetryTests
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
        public void An401ExpiredResponseTriggersOneRefreshAndOneRetry()
        {
            // Not close to expiring by the client's own clock, so the proactive check no-ops and
            // the server's 401 is the only signal that anything is wrong - exactly the case
            // ForceRefresh (unconditional) exists for, as opposed to EnsureFreshAccessToken.
            BackendV2SessionStore.Set(new BackendV2Session(
                "stale-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            transport.Enqueue(RawApiResponse.Completed(200, "\"8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a\""));

            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(client.GetMe(r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(transport.Requests, Has.Count.EqualTo(3));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/players/me"));
            Assert.That(transport.Requests[1].RelativePath, Is.EqualTo("api/v2/auth/refresh"));
            Assert.That(transport.Requests[2].RelativePath, Is.EqualTo("api/v2/players/me"));
        }

        [Test]
        public void ARetryHappensOnlyOnceEvenIfTheRetriedRequestAlsoFails()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "stale-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));

            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(client.GetMe(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(transport.Requests, Has.Count.EqualTo(3), "no third attempt after a failed retry");
        }

        [Test]
        public void NoSessionAtAllFailsFastAsUnauthenticatedNotExpired()
        {
            BackendV2SessionStore.Clear();
            FakeApiTransport transport = new FakeApiTransport();
            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(client.GetMe(r => result = r));

            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(transport.Requests, Is.Empty);
        }
    }
}
