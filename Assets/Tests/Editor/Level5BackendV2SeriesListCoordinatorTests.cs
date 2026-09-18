using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2SeriesListCoordinatorTests
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

        private static SeriesListCoordinator ActiveCoordinator(FakeApiTransport transport)
        {
            BackendV2Runtime.Override(transport);
            return new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListActive);
        }

        [Test]
        public void RefreshReplacesItemsAndCapturesTheCursor()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage));
            SeriesListCoordinator coordinator = ActiveCoordinator(transport);

            CoroutineTestRunner.RunToCompletion(coordinator.Refresh());

            Assert.That(coordinator.State.Items, Has.Count.EqualTo(1));
            Assert.That(coordinator.State.NextCursor, Is.EqualTo("opaque-cursor-token"));
            Assert.That(coordinator.State.HasMore, Is.True);
        }

        [Test]
        public void LoadMoreAppendsRatherThanReplacingAndForwardsTheCursorVerbatim()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage.Replace(
                "\"nextCursor\": \"opaque-cursor-token\"", "\"nextCursor\": null")));
            SeriesListCoordinator coordinator = ActiveCoordinator(transport);

            CoroutineTestRunner.RunToCompletion(coordinator.Refresh());
            CoroutineTestRunner.RunToCompletion(coordinator.LoadMore());

            Assert.That(coordinator.State.Items, Has.Count.EqualTo(2));
            Assert.That(coordinator.State.HasMore, Is.False);
            Assert.That(transport.Requests[1].Query["cursor"], Is.EqualTo("opaque-cursor-token"),
                "the cursor from the first page must be forwarded exactly, never parsed or rebuilt");
        }

        [Test]
        public void LoadMoreIsANoOpWhenThereIsNothingMore()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesSummaryPage.Replace(
                "\"nextCursor\": \"opaque-cursor-token\"", "\"nextCursor\": null")));
            SeriesListCoordinator coordinator = ActiveCoordinator(transport);

            CoroutineTestRunner.RunToCompletion(coordinator.Refresh());
            transport.Requests.Clear();
            CoroutineTestRunner.RunToCompletion(coordinator.LoadMore());

            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public void AFailedRefreshLeavesAnErrorAndNoItems()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            SeriesListCoordinator coordinator = ActiveCoordinator(transport);

            CoroutineTestRunner.RunToCompletion(coordinator.Refresh());

            Assert.That(coordinator.State.ErrorMessage, Is.Not.Null);
            Assert.That(coordinator.State.Items, Is.Empty);
        }
    }
}
