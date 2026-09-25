using System;
using Level5.BackendV2;
using Level5.Core;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="OnlineAccountCoordinator"/> - register/sign-in/sign-out/self-profile for the
    /// Backend V2 online-account screen. Every test also captures
    /// <see cref="LocalAccountIdentity.UserId"/>/<see cref="LocalAccountIdentity.UserName"/> before and
    /// after, asserting neither changes - the online account layer must stay fully independent of the
    /// local profile selection.
    /// </summary>
    public class Level5BackendV2OnlineAccountCoordinatorTests
    {
        private const string InvalidCredentials = @"{
            ""type"": ""https://level5.game/errors/invalid_credentials"",
            ""title"": ""The username or password is incorrect."",
            ""status"": 401,
            ""code"": ""invalid_credentials"",
            ""traceId"": ""00-trace-onlineaccount-01""
        }";

        [SetUp]
        public void SetUp()
        {
            BackendV2SessionStore.Clear();
            LocalAccountIdentity.UserId = 42;
            LocalAccountIdentity.UserName = "local-guest";
        }

        /// <summary>Unlike <see cref="BackendV2Fixtures.TokenResponse"/> (a fixed past date, fine for
        /// a login call tested in isolation), this uses a relative future expiry - these tests chain
        /// straight into an authorized <c>GetMyProfile</c> call in the same coordinator call, and a
        /// stale fixed <c>expiresAt</c> would trigger an unplanned proactive refresh
        /// (<see cref="BackendV2SessionManager.EnsureFreshAccessToken"/>) that consumes an extra
        /// transport response neither test enqueues.</summary>
        private static string FreshTokenResponse()
        {
            return "{"
                + "\"accessToken\": \"eyJhbGciOiJIUzI1NiJ9.fake.token\","
                + "\"expiresAt\": \"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O") + "\","
                + "\"playerId\": \"8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a\","
                + "\"refreshToken\": \"r-8f14e45f-ceea-467e\","
                + "\"refreshTokenExpiresAt\": \"" + DateTimeOffset.UtcNow.AddDays(30).ToString("O") + "\""
                + "}";
        }

        [TearDown]
        public void TearDown()
        {
            BackendV2SessionStore.Clear();
            BackendV2Runtime.Reset();
        }

        [Test]
        public void SignInStoresSessionAndFetchesProfileOnSuccess()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, FreshTokenResponse()));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.PlayerProfile));
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            string error = "not called";
            CoroutineTestRunner.RunToCompletion(coordinator.SignIn("ada", "hunter2", e => error = e));

            Assert.That(error, Is.Null);
            Assert.That(coordinator.IsSignedIn, Is.True);
            Assert.That(coordinator.Profile, Is.Not.Null);
            Assert.That(coordinator.Profile.DisplayName, Is.EqualTo("Ada"));
            Assert.That(coordinator.Profile.Tag, Is.EqualTo("ADA#1234"));
            Assert.That(coordinator.OperationInProgress, Is.False);
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void SignInFailsWithoutCreatingASessionOnInvalidCredentials()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(401, InvalidCredentials));
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            string error = null;
            CoroutineTestRunner.RunToCompletion(coordinator.SignIn("ada", "wrong-password", e => error = e));

            Assert.That(error, Is.Not.Null);
            Assert.That(coordinator.IsSignedIn, Is.False);
            Assert.That(coordinator.Profile, Is.Null);
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "an invalid login must never fetch a profile");
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void RegisterStoresSessionAndFetchesProfileOnSuccess()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, FreshTokenResponse()));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.PlayerProfile));
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            string error = "not called";
            CoroutineTestRunner.RunToCompletion(
                coordinator.Register("ada", "hunter2", "Ada", e => error = e));

            Assert.That(error, Is.Null);
            Assert.That(coordinator.IsSignedIn, Is.True);
            Assert.That(coordinator.Profile.DisplayName, Is.EqualTo("Ada"));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/auth/register"));
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void EnterScreenSkipsLoginAndFetchesProfileWhenASessionAlreadyExists()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.PlayerProfile));
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            CoroutineTestRunner.RunToCompletion(coordinator.EnterScreen());

            Assert.That(coordinator.IsSignedIn, Is.True);
            Assert.That(coordinator.Profile.DisplayName, Is.EqualTo("Ada"));
            Assert.That(transport.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/players/me/profile"));
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void ProfileFetchFailurePreservesTheSignedInSessionAndReportsARetryableError()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            CoroutineTestRunner.RunToCompletion(coordinator.RefreshProfile());

            Assert.That(coordinator.IsSignedIn, Is.True, "a transient profile-read failure must not sign the player out");
            Assert.That(coordinator.Profile, Is.Null);
            Assert.That(coordinator.ProfileError, Is.Not.Null);
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void SignOutClearsTheSessionOnServerSuccess()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(204, string.Empty));
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            string error = "not called";
            CoroutineTestRunner.RunToCompletion(coordinator.SignOut(e => error = e));

            Assert.That(error, Is.Null);
            Assert.That(coordinator.IsSignedIn, Is.False);
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void SignOutClearsTheLocalSessionEvenWhenTheServerCallFails()
        {
            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));

            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            CoroutineTestRunner.RunToCompletion(coordinator.SignOut(e => { }));

            Assert.That(
                coordinator.IsSignedIn,
                Is.False,
                "a player must always be able to sign out locally, even offline");
            AssertLocalIdentityUnchanged();
        }

        [Test]
        public void SignInCannotStartWhileAlreadyAuthenticated()
        {
            BackendV2Session existing = new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30));
            BackendV2SessionStore.Set(existing);

            FakeApiTransport transport = new FakeApiTransport();
            BackendV2Runtime.Override(transport);

            OnlineAccountCoordinator coordinator = new OnlineAccountCoordinator();
            string error = null;
            CoroutineTestRunner.RunToCompletion(coordinator.SignIn("someone-else", "hunter2", e => error = e));

            Assert.That(error, Is.Not.Null);
            Assert.That(transport.Requests, Is.Empty, "switching accounts must require an explicit sign-out first");
            Assert.That(BackendV2SessionStore.Current, Is.SameAs(existing));
            AssertLocalIdentityUnchanged();
        }

        private static void AssertLocalIdentityUnchanged()
        {
            Assert.That(LocalAccountIdentity.UserId, Is.EqualTo(42));
            Assert.That(LocalAccountIdentity.UserName, Is.EqualTo("local-guest"));
        }
    }
}
