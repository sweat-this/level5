using System;
using System.Collections;
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

        [Test]
        public void AProactiveRefreshFailureSendsOnlyTheRefreshRequestAndPreservesTheSession()
        {
            // Access token near expiry, so the proactive check (EnsureFreshAccessToken) is the one
            // that fires the refresh - not the server-401 path. A transient failure here must stop
            // the protected request from ever being sent with an already-known-stale token, and
            // must not sign the player out.
            BackendV2SessionStore.Set(new BackendV2Session(
                "stale-token", DateTimeOffset.UtcNow.AddSeconds(5), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());

            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(client.GetMe(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Network));
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "the protected endpoint must never be sent");
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/auth/refresh"));
            Assert.That(
                BackendV2SessionStore.IsAuthenticated, Is.True,
                "a transient proactive refresh failure must not sign the player out");
        }

        [Test]
        public void A401FollowedByATransientRefreshFailureDoesNotRetryAndPreservesTheSession()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "stale-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            transport.Enqueue(RawApiResponse.NetworkError());

            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(client.GetMe(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(
                result.ErrorKind, Is.EqualTo(ApiErrorKind.Network),
                "the refresh failure is the real blocker, not the original Expired response");
            Assert.That(transport.Requests, Has.Count.EqualTo(2), "no protected retry after a transient refresh failure");
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/players/me"));
            Assert.That(transport.Requests[1].RelativePath, Is.EqualTo("api/v2/auth/refresh"));
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True);
        }

        [Test]
        public void A401FollowedByADefinitiveRefreshFailureClearsTheSessionAndReturnsUnauthenticated()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "stale-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));

            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            CoroutineTestRunner.RunToCompletion(client.GetMe(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(transport.Requests, Has.Count.EqualTo(2), "no protected retry after a definitive refresh failure");
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False);
        }

        [Test]
        public void A401FollowedByAStaleForcedRefreshDoesNotRetryAndReturnsUnauthenticated()
        {
            // The forced-refresh counterpart to
            // Level5BackendV2SessionTests.AStaleSuccessfulRefreshResponseDoesNotOverwriteANewerSession:
            // proves that a session-replacement race during the 401 retry path cannot cause a
            // retry to be silently sent under a different player's session than the one the
            // original request was made for.
            BackendV2SessionStore.Set(new BackendV2Session(
                "stale-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));

            PlayersApiClient client = new PlayersApiClient(
                transport, new BackendV2SessionManager(new AuthApiClient(transport)));

            ApiResponse<Guid> result = null;
            IEnumerator outer = client.GetMe(r => result = r);

            // Proactive EnsureFreshAccessToken: not close to expiry, no-ops synchronously.
            Assert.That(outer.MoveNext(), Is.True);
            CoroutineTestRunner.RunToCompletion((IEnumerator)outer.Current);

            // The initial protected request (players/me) - answers 401.
            Assert.That(outer.MoveNext(), Is.True);
            CoroutineTestRunner.RunToCompletion((IEnumerator)outer.Current);

            // ForceRefresh - stepped manually into RefreshNow so a session swap can be injected
            // before the refresh response arrives.
            Assert.That(outer.MoveNext(), Is.True);
            IEnumerator forceRefresh = (IEnumerator)outer.Current;
            Assert.That(forceRefresh.MoveNext(), Is.True);
            IEnumerator refreshNow = (IEnumerator)forceRefresh.Current;
            Assert.That(refreshNow.MoveNext(), Is.True);
            Assert.That(refreshNow.Current, Is.InstanceOf<IEnumerator>(), "should be suspended on its own Refresh call");

            // A different session replaces the one this request/refresh was made for while the
            // forced refresh is still in flight.
            BackendV2Session replacement = new BackendV2Session(
                "other-access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(),
                "other-refresh-token", DateTimeOffset.UtcNow.AddDays(30));
            BackendV2SessionStore.Set(replacement);

            CoroutineTestRunner.RunToCompletion((IEnumerator)refreshNow.Current);
            CoroutineTestRunner.RunToCompletion(refreshNow);
            CoroutineTestRunner.RunToCompletion(forceRefresh);
            CoroutineTestRunner.RunToCompletion(outer);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(transport.Requests, Has.Count.EqualTo(2), "no protected retry after a stale forced refresh");
            Assert.That(BackendV2SessionStore.Current, Is.SameAs(replacement),
                "the session that replaced the one this request was made for must be untouched");
        }
    }
}
