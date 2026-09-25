using System;
using System.Collections;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2SessionTests
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

        private static BackendV2Session Session(DateTimeOffset expiresAt)
        {
            return new BackendV2Session(
                "access-token", expiresAt, Guid.NewGuid(), "refresh-token", expiresAt.AddDays(30));
        }

        [Test]
        public void LoginStoresTheReturnedSession()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<BackendV2Session> result = null;
            CoroutineTestRunner.RunToCompletion(manager.Login("ada", "hunter2", r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True);
            Assert.That(
                BackendV2SessionStore.Current.PlayerId.ToString(),
                Is.EqualTo("8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"));
        }

        [Test]
        public void ALoginFailureNeverStoresASession()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<BackendV2Session> result = null;
            CoroutineTestRunner.RunToCompletion(manager.Login("ada", "wrong", r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False);
        }

        [Test]
        public void LogoutClearsTheStoreEvenWhenTheServerCallFails()
        {
            BackendV2SessionStore.Set(Session(DateTimeOffset.UtcNow.AddHours(1)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            CoroutineTestRunner.RunToCompletion(manager.Logout());

            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False);
        }

        [Test]
        public void EnsureFreshAccessTokenNoOpsWhenNotCloseToExpiring()
        {
            BackendV2SessionStore.Set(Session(DateTimeOffset.UtcNow.AddHours(1)));
            FakeApiTransport transport = new FakeApiTransport();
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            CoroutineTestRunner.RunToCompletion(manager.EnsureFreshAccessToken(r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.False, "no refresh should have been attempted");
            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public void EnsureFreshAccessTokenRefreshesWhenExpiringSoon()
        {
            BackendV2SessionStore.Set(Session(DateTimeOffset.UtcNow.AddSeconds(5)));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            CoroutineTestRunner.RunToCompletion(manager.EnsureFreshAccessToken(r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(transport.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/auth/refresh"));
        }

        [Test]
        public void ForceRefreshRefreshesEvenWhenNotCloseToExpiring()
        {
            // ForceRefresh backs the retry-after-a-server-401 path, which must not be silently
            // skipped just because this client's own clock thought the token still looked fresh.
            BackendV2SessionStore.Set(Session(DateTimeOffset.UtcNow.AddHours(1)));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            CoroutineTestRunner.RunToCompletion(manager.ForceRefresh(r => result = r));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(transport.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/auth/refresh"));
        }

        [Test]
        public void ConcurrentRefreshCallsShareOneRefreshRequest()
        {
            // Two callers (e.g. two authorized requests firing close together) both find the
            // access token expiring soon at the same time. Without single-flight coordination this
            // would issue two concurrent refresh calls against the same refresh token - and a
            // losing one's failure would clear a session a winning one had just legitimately set.
            BackendV2SessionStore.Set(Session(DateTimeOffset.UtcNow.AddSeconds(5)));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> resultA = null;
            ApiResponse<bool> resultB = null;

            // EnsureFreshAccessToken's single `yield return RefreshNow(...)` merely hands back the
            // nested enumerator as Current - it does not drive it. Unity's real coroutine engine
            // (and CoroutineTestRunner) auto-recurses into a yielded IEnumerator, but a raw
            // MoveNext() here does not, so the guard itself - inside RefreshNow - has to be reached
            // by stepping into that nested enumerator explicitly.
            IEnumerator outerA = manager.EnsureFreshAccessToken(r => resultA = r);
            Assert.That(outerA.MoveNext(), Is.True);
            IEnumerator refreshA = (IEnumerator)outerA.Current;

            // Advance A's RefreshNow far enough to claim the in-flight guard and suspend on its own
            // call to authClient.Refresh - simulating B arriving mid-refresh.
            Assert.That(refreshA.MoveNext(), Is.True);
            Assert.That(refreshA.Current, Is.InstanceOf<IEnumerator>(), "A should be suspended on its own Refresh call");

            IEnumerator outerB = manager.EnsureFreshAccessToken(r => resultB = r);
            Assert.That(outerB.MoveNext(), Is.True);
            IEnumerator refreshB = (IEnumerator)outerB.Current;

            // B's RefreshNow must see the guard and wait rather than starting a second refresh.
            Assert.That(refreshB.MoveNext(), Is.True);
            Assert.That(refreshB.Current, Is.Null, "B should be waiting on A's in-flight refresh, not starting its own");

            // refreshA's Current is already sitting on the nested authClient.Refresh(...) call from
            // the MoveNext() above; RunToCompletion only drains Current values produced by its own
            // MoveNext() calls, so that already-pending one must be drained explicitly first.
            DrainPending(refreshA);
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "only one refresh request should ever be sent");
            Assert.That(resultA.Success, Is.True);

            DrainPending(refreshB);
            Assert.That(resultB.Success, Is.True);
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "B must not have issued a second refresh");
        }

        [Test]
        public void ConcurrentRefreshCallsShareOneTransientFailure()
        {
            // The failure-path counterpart to ConcurrentRefreshCallsShareOneRefreshRequest above:
            // two callers racing a refresh that fails transiently must both observe the same
            // failure from a single refresh request, and the session must survive it.
            BackendV2Session original = Session(DateTimeOffset.UtcNow.AddSeconds(5));
            BackendV2SessionStore.Set(original);
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> resultA = null;
            ApiResponse<bool> resultB = null;

            IEnumerator outerA = manager.EnsureFreshAccessToken(r => resultA = r);
            Assert.That(outerA.MoveNext(), Is.True);
            IEnumerator refreshA = (IEnumerator)outerA.Current;
            Assert.That(refreshA.MoveNext(), Is.True);
            Assert.That(refreshA.Current, Is.InstanceOf<IEnumerator>(), "A should be suspended on its own Refresh call");

            IEnumerator outerB = manager.EnsureFreshAccessToken(r => resultB = r);
            Assert.That(outerB.MoveNext(), Is.True);
            IEnumerator refreshB = (IEnumerator)outerB.Current;
            Assert.That(refreshB.MoveNext(), Is.True);
            Assert.That(refreshB.Current, Is.Null, "B should be waiting on A's in-flight refresh");

            DrainPending(refreshA);
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "only one refresh request should ever be sent");
            Assert.That(resultA.Success, Is.False);
            Assert.That(resultA.ErrorKind, Is.EqualTo(ApiErrorKind.Network));

            DrainPending(refreshB);
            Assert.That(resultB.Success, Is.False);
            Assert.That(resultB.ErrorKind, Is.EqualTo(ApiErrorKind.Network));
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "B must not have issued a second refresh");

            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True, "a transient failure must not clear the session");
            Assert.That(BackendV2SessionStore.Current, Is.SameAs(original));
        }

        [Test]
        public void ANetworkRefreshFailurePreservesTheSession()
        {
            AssertTransientRefreshFailurePreservesTheSession(RawApiResponse.NetworkError(), ApiErrorKind.Network);
        }

        [Test]
        public void ATimeoutRefreshFailurePreservesTheSession()
        {
            AssertTransientRefreshFailurePreservesTheSession(RawApiResponse.Timeout(), ApiErrorKind.Timeout);
        }

        [Test]
        public void AServerErrorRefreshFailurePreservesTheSession()
        {
            AssertTransientRefreshFailurePreservesTheSession(
                RawApiResponse.Completed(500, BackendV2Fixtures.ProblemDetailsNotFound), ApiErrorKind.ServerError);
        }

        [Test]
        public void ARateLimitedRefreshFailurePreservesTheSession()
        {
            AssertTransientRefreshFailurePreservesTheSession(
                RawApiResponse.Completed(429, BackendV2Fixtures.ProblemDetailsNotFound), ApiErrorKind.RateLimited);
        }

        [Test]
        public void AMalformedRefreshResponsePreservesTheSession()
        {
            AssertTransientRefreshFailurePreservesTheSession(
                RawApiResponse.Completed(200, "not valid json"), ApiErrorKind.MalformedResponse);
        }

        /// <summary>None of these are evidence Backend V2 rejected the refresh credential itself -
        /// only a definitive server 401 (<see cref="ApiErrorKind.Unauthenticated"/>) or a locally
        /// expired refresh token is. Every other failure class must leave the exact same session
        /// object in place, not merely an equal-looking replacement.</summary>
        private static void AssertTransientRefreshFailurePreservesTheSession(
            RawApiResponse failureResponse, ApiErrorKind expectedKind)
        {
            BackendV2Session original = Session(DateTimeOffset.UtcNow.AddSeconds(5));
            BackendV2SessionStore.Set(original);
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(failureResponse);
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            CoroutineTestRunner.RunToCompletion(manager.ForceRefresh(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(expectedKind));
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.True);
            Assert.That(BackendV2SessionStore.Current, Is.SameAs(original));
            Assert.That(BackendV2SessionStore.Current.PlayerId, Is.EqualTo(original.PlayerId));
            Assert.That(BackendV2SessionStore.Current.RefreshToken, Is.EqualTo(original.RefreshToken));
        }

        [Test]
        public void ADefinitiveUnauthenticatedRefreshFailureClearsTheSession()
        {
            BackendV2SessionStore.Set(Session(DateTimeOffset.UtcNow.AddSeconds(5)));
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, BackendV2Fixtures.ProblemDetailsNotFound));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            CoroutineTestRunner.RunToCompletion(manager.ForceRefresh(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False);
        }

        [Test]
        public void AnAlreadyExpiredRefreshTokenClearsLocallyWithoutAnyNetworkCall()
        {
            BackendV2Session expiredRefresh = new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddMinutes(-1));
            BackendV2SessionStore.Set(expiredRefresh);
            FakeApiTransport transport = new FakeApiTransport();
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            CoroutineTestRunner.RunToCompletion(manager.ForceRefresh(r => result = r));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorKind, Is.EqualTo(ApiErrorKind.Unauthenticated));
            Assert.That(BackendV2SessionStore.IsAuthenticated, Is.False);
            Assert.That(transport.Requests, Is.Empty, "a locally expired refresh token must never reach the network");
        }

        [Test]
        public void AStaleSuccessfulRefreshResponseDoesNotOverwriteANewerSession()
        {
            BackendV2Session sessionA = Session(DateTimeOffset.UtcNow.AddHours(1));
            BackendV2SessionStore.Set(sessionA);
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.TokenResponse));
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<bool> result = null;
            IEnumerator outer = manager.ForceRefresh(r => result = r);
            Assert.That(outer.MoveNext(), Is.True);
            IEnumerator refresh = (IEnumerator)outer.Current;
            Assert.That(refresh.MoveNext(), Is.True);
            Assert.That(refresh.Current, Is.InstanceOf<IEnumerator>(), "should be suspended on its own Refresh call");

            // Session B replaces A while A's refresh is still in flight - e.g. a fresh login
            // completing before the old refresh response arrives.
            BackendV2Session sessionB = Session(DateTimeOffset.UtcNow.AddHours(2));
            BackendV2SessionStore.Set(sessionB);

            DrainPending(refresh);

            Assert.That(BackendV2SessionStore.Current, Is.SameAs(sessionB),
                "a refresh response for a superseded session must not overwrite the session that replaced it");

            // The caller whose refresh got superseded must not be told it has a usable rotated
            // token - session B's authentication is not this caller's to use. Reporting success
            // here would let AuthenticatedApiClientBase retry a protected request under session
            // B's identity even though it was made for session A.
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.False,
                "a stale refresh must never report a usable token, even though some other session is authenticated");
        }

        [Test]
        public void ALogoutReplacementRaceDoesNotClearANewerSession()
        {
            BackendV2Session sessionA = Session(DateTimeOffset.UtcNow.AddHours(1));
            BackendV2SessionStore.Set(sessionA);
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2SessionManager manager = new BackendV2SessionManager(new AuthApiClient(transport));

            ApiResponse<ApiVoid> result = null;
            IEnumerator logout = manager.Logout(r => result = r);
            Assert.That(logout.MoveNext(), Is.True);
            Assert.That(logout.Current, Is.InstanceOf<IEnumerator>(), "should be suspended on the logout HTTP call");

            // A new session is established (e.g. the player logged back in) before A's offline
            // logout request finishes.
            BackendV2Session sessionB = Session(DateTimeOffset.UtcNow.AddHours(2));
            BackendV2SessionStore.Set(sessionB);

            DrainPending(logout);

            Assert.That(BackendV2SessionStore.Current, Is.SameAs(sessionB),
                "a stale logout response must not clear a session that replaced the one it logged out");
        }

        /// <summary>Finishes draining an enumerator that has already had <c>MoveNext()</c> called on
        /// it at least once (so its <see cref="IEnumerator.Current"/> may already be a nested
        /// enumerator <see cref="CoroutineTestRunner.RunToCompletion"/> has not seen yet).</summary>
        private static void DrainPending(IEnumerator routine)
        {
            if (routine.Current is IEnumerator pendingNested)
            {
                CoroutineTestRunner.RunToCompletion(pendingNested);
            }

            CoroutineTestRunner.RunToCompletion(routine);
        }

        [Test]
        public void ASessionNeverTouchesGameOptionsOrUserModel()
        {
            // Regression guard for the same discipline legacy APIHelper.bearerToken already
            // follows: nothing here is reachable from GameOptions/UserModel, because
            // BackendV2SessionStore is the only place the token lives, in memory, for the life of
            // the session.
            BackendV2Session session = Session(DateTimeOffset.UtcNow.AddHours(1));
            BackendV2SessionStore.Set(session);

            Assert.That(BackendV2SessionStore.Current, Is.SameAs(session));
        }
    }
}
