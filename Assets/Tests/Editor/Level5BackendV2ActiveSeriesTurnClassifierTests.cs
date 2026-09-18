using System;
using System.Collections.Generic;
using System.Linq;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2ActiveSeriesTurnClassifierTests
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

        private static SeriesSummaryDto Summary(Guid id)
        {
            return new SeriesSummaryDto
            {
                Id = id,
                ChallengerId = Guid.NewGuid(),
                OpponentId = Guid.NewGuid(),
                Status = "Active",
                CurrentGameNumber = 1,
                TotalGames = 3,
                Revision = 1,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }

        [Test]
        public void ClassifiesEachRowFromItsOwnDetailFetch()
        {
            Guid yourTurnId = Guid.NewGuid();
            Guid opponentTurnId = Guid.NewGuid();

            FakeApiTransport transport = new FakeApiTransport();
            transport.Handler = request =>
            {
                bool isYourTurnRow = request.RelativePath.Contains(yourTurnId.ToString());
                string yourAttempt = isYourTurnRow
                    ? "null"
                    : @"{ ""id"": """ + Guid.NewGuid() + @""", ""status"": ""Completed"", ""result"": { ""Score"": 1 } }";

                string body = BackendV2Fixtures.SeriesDetail
                    .Replace("\"4c23c3d8-4040-4b1f-8d3f-9b8d3c2e5f6a\"", "\"" + (isYourTurnRow ? yourTurnId : opponentTurnId) + "\"")
                    .Replace("\"yourAttempt\": null", "\"yourAttempt\": " + yourAttempt);
                return RawApiResponse.Completed(200, body);
            };
            BackendV2Runtime.Override(transport);

            ActiveSeriesTurnClassifier classifier = new ActiveSeriesTurnClassifier();
            List<SeriesSummaryDto> items = new List<SeriesSummaryDto> { Summary(yourTurnId), Summary(opponentTurnId) };

            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(items));

            Assert.That(classifier.TurnFor(yourTurnId), Is.EqualTo(ActiveSeriesTurn.YourTurn));
            Assert.That(classifier.TurnFor(opponentTurnId), Is.EqualTo(ActiveSeriesTurn.OpponentTurn));
            Assert.That(classifier.YourTurnItems(items).Select(i => i.Id), Is.EquivalentTo(new[] { yourTurnId }));
            Assert.That(classifier.ErrorMessage, Is.Null);
        }

        [Test]
        public void AlreadyClassifiedRowsAreNotReFetched()
        {
            Guid id = Guid.NewGuid();
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            BackendV2Runtime.Override(transport);

            ActiveSeriesTurnClassifier classifier = new ActiveSeriesTurnClassifier();
            List<SeriesSummaryDto> items = new List<SeriesSummaryDto> { Summary(id) };
            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(items));

            transport.Requests.Clear();
            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(items));

            Assert.That(transport.Requests, Is.Empty, "an already-classified row must not be re-fetched");
        }

        [Test]
        public void ResetForcesReclassification()
        {
            Guid id = Guid.NewGuid();
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            BackendV2Runtime.Override(transport);

            ActiveSeriesTurnClassifier classifier = new ActiveSeriesTurnClassifier();
            List<SeriesSummaryDto> items = new List<SeriesSummaryDto> { Summary(id) };
            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(items));

            classifier.Reset();
            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(items));

            Assert.That(transport.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void AFailedDetailFetchIsReportedButDoesNotThrow()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            ActiveSeriesTurnClassifier classifier = new ActiveSeriesTurnClassifier();
            List<SeriesSummaryDto> items = new List<SeriesSummaryDto> { Summary(Guid.NewGuid()) };

            Assert.DoesNotThrow(() => CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(items)));
            Assert.That(classifier.ErrorMessage, Is.Not.Null);
        }

        [Test]
        public void OnItemClassifiedFiresOnceImmediatelyAfterEachRowRatherThanOnceAtTheEnd()
        {
            // Real network latency means classifying a full page sequentially can take several
            // seconds - the caller must be able to render each row as it settles instead of blocking
            // the whole tab on the slowest one.
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail));
            BackendV2Runtime.Override(transport);

            ActiveSeriesTurnClassifier classifier = new ActiveSeriesTurnClassifier();
            List<SeriesSummaryDto> items = new List<SeriesSummaryDto> { Summary(Guid.NewGuid()), Summary(Guid.NewGuid()) };

            List<int> classifiedCountAtEachCallback = new List<int>();
            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(
                items, () => classifiedCountAtEachCallback.Add(transport.Requests.Count)));

            Assert.That(classifiedCountAtEachCallback, Is.EqualTo(new[] { 1, 2 }),
                "the callback must fire after each row settles, not once after the whole batch");
        }
    }
}
