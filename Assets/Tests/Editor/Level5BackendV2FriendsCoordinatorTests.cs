using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2FriendsCoordinatorTests
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

        [Test]
        public void RefreshFriendsPopulatesTheListOnSuccess()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.FriendSummary));
            BackendV2Runtime.Override(transport);

            FriendsCoordinator coordinator = new FriendsCoordinator();
            CoroutineTestRunner.RunToCompletion(coordinator.RefreshFriends());

            Assert.That(coordinator.Friends.Items, Has.Count.EqualTo(1));
            Assert.That(coordinator.Friends.Items[0].DisplayName, Is.EqualTo("Grace"));
            Assert.That(coordinator.Friends.IsLoading, Is.False);
            Assert.That(coordinator.Friends.ErrorMessage, Is.Null);
        }

        [Test]
        public void RefreshFriendsSurfacesAFailureWithoutThrowing()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            FriendsCoordinator coordinator = new FriendsCoordinator();
            CoroutineTestRunner.RunToCompletion(coordinator.RefreshFriends());

            Assert.That(coordinator.Friends.ErrorMessage, Is.Not.Null);
            Assert.That(coordinator.Friends.IsLoading, Is.False);
        }

        [Test]
        public void SendRequestByTagLooksUpThenSendsThenRefreshesOutgoing()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.PlayerProfile));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.FriendRequest));
            transport.Enqueue(RawApiResponse.Completed(200, "[]")); // RefreshOutgoing
            BackendV2Runtime.Override(transport);

            FriendsCoordinator coordinator = new FriendsCoordinator();
            string error = "not called";
            CoroutineTestRunner.RunToCompletion(
                coordinator.SendRequestByTag("ADA#1234", e => error = e));

            Assert.That(error, Is.Null);
            Assert.That(transport.Requests, Has.Count.EqualTo(3));
        }

        [Test]
        public void SendRequestByTagStopsAfterAFailedLookupAndNeverSendsARequest()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(404, BackendV2Fixtures.ProblemDetailsNotFound));
            BackendV2Runtime.Override(transport);

            FriendsCoordinator coordinator = new FriendsCoordinator();
            string error = null;
            CoroutineTestRunner.RunToCompletion(
                coordinator.SendRequestByTag("NOBODY#0000", e => error = e));

            Assert.That(error, Is.Not.Null);
            Assert.That(transport.Requests, Has.Count.EqualTo(1), "a failed lookup must never reach SendRequest");
        }

        [Test]
        public void AcceptRefreshesIncomingAndFriendsOnSuccess()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, string.Empty)); // Accept
            transport.Enqueue(RawApiResponse.Completed(200, "[]")); // RefreshIncoming
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.FriendSummary)); // RefreshFriends
            BackendV2Runtime.Override(transport);

            FriendsCoordinator coordinator = new FriendsCoordinator();
            string error = "not called";
            Guid requestId = Guid.NewGuid();
            CoroutineTestRunner.RunToCompletion(coordinator.Accept(requestId, e => error = e));

            Assert.That(error, Is.Null);
            Assert.That(coordinator.Friends.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void ARowCommandAlreadyInFlightRefusesASecondConcurrentCallForTheSameRow()
        {
            FriendsCoordinator coordinator = new FriendsCoordinator();
            Guid requestId = Guid.NewGuid();
            coordinator.Commands.TryBegin(requestId);

            string error = null;
            CoroutineTestRunner.RunToCompletion(coordinator.Accept(requestId, e => error = e));

            Assert.That(error, Is.Not.Null.And.Contains("already in progress"));
        }
    }
}
