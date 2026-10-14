using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2InboxTests
    {
        private Guid player;
        private Guid opponent;
        [SetUp] public void SetUp()
        {
            player = Guid.NewGuid(); opponent = Guid.NewGuid();
            BackendV2SessionStore.Set(Session(player));
        }
        [TearDown] public void TearDown() { BackendV2Runtime.Reset(); BackendV2SessionStore.Clear(); }
        private static BackendV2Session Session(Guid id) => new BackendV2Session("access", DateTimeOffset.UtcNow.AddHours(1), id, "refresh", DateTimeOffset.UtcNow.AddDays(1));
        private SeriesSummaryDto Series(string status = "PendingAcceptance", long revision = 1) => new SeriesSummaryDto
        { Id = Guid.NewGuid(), ChallengerId = opponent, OpponentId = player, Status = status, Revision = revision, CurrentGameNumber = 1, TotalGames = 3 };
        private static ListViewState<SeriesSummaryDto> State(params SeriesSummaryDto[] items)
        { var state = new ListViewState<SeriesSummaryDto>(); state.ReplaceWith(items); return state; }
        private IReadOnlyList<CorrespondenceInboxRow> Build(params ListViewState<SeriesSummaryDto>[] states) => CorrespondenceInbox.Build(player, new ActiveSeriesTurnClassifier(), states);

        [Test] public void EmptySourcesHaveNoRows() => Assert.That(Build(State()), Is.Empty);
        [Test] public void IncomingIsDeduplicatedAcrossPagesAndSources()
        {
            var series = Series(); var state = State(series); state.AppendPage(new[] { series }, "opaque");
            var rows = Build(state, State(series));
            Assert.That(rows.Count, Is.EqualTo(1)); Assert.That(rows[0].Category, Is.EqualTo(InboxCategory.Incoming));
            Assert.That(rows[0].AttentionKey, Is.EqualTo("challenge:" + series.Id.ToString("N")));
            Assert.That(state.NextCursor, Is.EqualTo("opaque"));
        }
        [Test] public void OutgoingIsWaitingWithoutAttention()
        {
            var series = Series(); series.ChallengerId = player; series.OpponentId = opponent;
            var row = Build(State(series)).Single(); Assert.That(row.Category, Is.EqualTo(InboxCategory.Waiting)); Assert.That(row.AttentionKey, Is.Null);
        }
        [TestCase("Declined")] [TestCase("Cancelled")] [TestCase("Expired")]
        public void NewerTerminalHistorySuppressesStalePending(string status)
        {
            var old = Series(); var terminal = Series(status, 2); terminal.Id = old.Id;
            Assert.That(Build(State(old), State(terminal)), Is.Empty);
        }
        [Test] public void CompletionSupersedesOlderIncomingAndDoesNotUseRevisionForAttention()
        {
            var old = Series(); var done = Series("Completed", 5); done.Id = old.Id;
            var row = Build(State(old), State(done)).Single(); Assert.That(row.Category, Is.EqualTo(InboxCategory.Completed));
            string key = row.AttentionKey; done.Revision++; Assert.That(Build(State(done)).Single().AttentionKey, Is.EqualTo(key));
        }
        [Test] public void EqualRevisionConflictsRemainUnconfirmed()
        {
            var a = Series(); var b = Series("Active"); b.Id = a.Id;
            var row = Build(State(a), State(b)).Single(); Assert.That(row.Category, Is.EqualTo(InboxCategory.Unconfirmed)); Assert.That(row.AttentionKey, Is.Null);
        }
        [TestCase(true)] [TestCase(false)] public void FailedOrLoadingSourcesAreNotActionable(bool loading)
        {
            var state = State(Series()); if (loading) state.BeginLoad(); else state.Fail("offline");
            var row = Build(state).Single(); Assert.That(row.Category, Is.EqualTo(InboxCategory.Unconfirmed)); Assert.That(row.AttentionKey, Is.Null);
        }
        [Test] public void ForeignAccountRowsAreExcluded()
        { var series = Series(); series.OpponentId = Guid.NewGuid(); Assert.That(Build(State(series)), Is.Empty); }
        [Test] public void PartialFailureDoesNotHideHealthyIncoming()
        {
            var failed = State(Series("Active")); failed.Fail("offline");
            var rows = Build(State(Series()), failed); Assert.That(rows.Select(r => r.Category), Is.EquivalentTo(new[] { InboxCategory.Incoming, InboxCategory.Unconfirmed }));
        }
        [Test] public void ConfirmedTurnIdentityIsGameSpecificAndDoesNotReadSealedMetrics()
        {
            var summary = Series("Active"); var detail = new SeriesResponseDto { Id = summary.Id,
                ChallengerId = opponent, OpponentId = player, Status = "Active", Revision = 1, CurrentGameNumber = 1, TotalGames = 3,
                Rules = new FrozenRulesDto { InformationPolicy = "SealedAttempt" },
                Games = new List<GameRoundViewDto> { new GameRoundViewDto { GameNumber = 1, OpponentAttempt = new AttemptViewDto { Status = "Completed", Result = null } } } };
            var transport = new FakeApiTransport { Handler = _ => RawApiResponse.Completed(200, BackendV2Json.Serialize(detail)) };
            BackendV2Runtime.Override(transport); var classifier = new ActiveSeriesTurnClassifier();
            CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(new[] { summary }));
            var row = CorrespondenceInbox.Build(player, classifier, State(summary)).Single();
            Assert.That(row.Category, Is.EqualTo(InboxCategory.YourTurn)); Assert.That(row.AttentionKey, Does.EndWith(":1"));
            summary.Revision++; Assert.That(CorrespondenceInbox.Build(player, classifier, State(summary)).Single().Category, Is.EqualTo(InboxCategory.Unconfirmed));
            Assert.That(transport.Requests.Count, Is.EqualTo(1), "projection must not fetch pages or details");
        }
        [Test] public void StaleDetailAndResetDuringFetchCannotConfirmTurn()
        {
            var summary = Series("Active"); var classifier = new ActiveSeriesTurnClassifier();
            var transport = new FakeApiTransport { Handler = _ => RawApiResponse.Completed(200, BackendV2Fixtures.SeriesDetail) };
            BackendV2Runtime.Override(transport); CoroutineTestRunner.RunToCompletion(classifier.ClassifyAll(new[] { summary }));
            Assert.That(classifier.TurnFor(summary), Is.Null); Assert.That(classifier.ErrorMessage, Is.Not.Null);
            var work = classifier.ClassifyAll(new[] { summary }); Assert.That(work.MoveNext(), Is.True);
            classifier.Reset(); CoroutineTestRunner.RunToCompletion((IEnumerator)work.Current); CoroutineTestRunner.RunToCompletion(work);
            Assert.That(classifier.TurnFor(summary), Is.Null);
        }
        [Test] public void NewerRefreshWinsAndOpaqueCursorIsPreserved()
        {
            var callbacks = new List<Action<ApiResponse<SeriesSummaryPageDto>>>();
            var coordinator = new SeriesListCoordinator((limit, cursor, callback) => Capture(callbacks, callback));
            var first = coordinator.Refresh(); first.MoveNext(); CoroutineTestRunner.RunToCompletion((IEnumerator)first.Current);
            var second = coordinator.Refresh(); second.MoveNext(); CoroutineTestRunner.RunToCompletion((IEnumerator)second.Current);
            var fresh = Series("Completed", 3);
            callbacks[1](ApiResponse<SeriesSummaryPageDto>.Ok(new SeriesSummaryPageDto { Items = new List<SeriesSummaryDto> { fresh }, NextCursor = "opaque /+=" }));
            CoroutineTestRunner.RunToCompletion(second);
            callbacks[0](ApiResponse<SeriesSummaryPageDto>.Ok(new SeriesSummaryPageDto { Items = new List<SeriesSummaryDto> { Series() } }));
            CoroutineTestRunner.RunToCompletion(first);
            Assert.That(coordinator.State.Items.Single().Id, Is.EqualTo(fresh.Id)); Assert.That(coordinator.State.NextCursor, Is.EqualTo("opaque /+="));
        }
        [Test] public void APageInFlightCannotAppendAfterANewerRefresh()
        {
            var callbacks = new List<Action<ApiResponse<SeriesSummaryPageDto>>>();
            var coordinator = new SeriesListCoordinator((limit, cursor, callback) => Capture(callbacks, callback));
            coordinator.State.ReplaceWith(new[] { Series() }, "old cursor");
            var more = coordinator.LoadMore(); more.MoveNext(); CoroutineTestRunner.RunToCompletion((IEnumerator)more.Current);
            var refresh = coordinator.Refresh(); refresh.MoveNext(); CoroutineTestRunner.RunToCompletion((IEnumerator)refresh.Current);
            var fresh = Series("Completed", 3);
            callbacks[1](ApiResponse<SeriesSummaryPageDto>.Ok(new SeriesSummaryPageDto { Items = new List<SeriesSummaryDto> { fresh }, NextCursor = "fresh cursor" }));
            CoroutineTestRunner.RunToCompletion(refresh);
            callbacks[0](ApiResponse<SeriesSummaryPageDto>.Ok(new SeriesSummaryPageDto { Items = new List<SeriesSummaryDto> { Series() } }));
            CoroutineTestRunner.RunToCompletion(more);
            Assert.That(coordinator.State.Items.Single().Id, Is.EqualTo(fresh.Id)); Assert.That(coordinator.State.NextCursor, Is.EqualTo("fresh cursor"));
        }

        [Test] public void LogoutAndReloginDiscardPriorAccountResponse()
        {
            var callbacks = new List<Action<ApiResponse<SeriesSummaryPageDto>>>();
            var coordinator = new SeriesListCoordinator((limit, cursor, callback) => Capture(callbacks, callback));
            var work = coordinator.Refresh(); work.MoveNext(); CoroutineTestRunner.RunToCompletion((IEnumerator)work.Current);
            BackendV2SessionStore.Clear(); BackendV2SessionStore.Set(Session(player));
            callbacks.Single()(ApiResponse<SeriesSummaryPageDto>.Ok(new SeriesSummaryPageDto { Items = new List<SeriesSummaryDto> { Series() } }));
            CoroutineTestRunner.RunToCompletion(work); Assert.That(coordinator.State.Items, Is.Empty); Assert.That(coordinator.State.HasLoaded, Is.False);
        }
        [Test] public void TokenRotationDoesNotChangeOwnerGeneration()
        { long before = BackendV2SessionStore.IdentityGeneration; BackendV2SessionStore.Set(Session(player)); Assert.That(BackendV2SessionStore.IdentityGeneration, Is.EqualTo(before)); }
        private static IEnumerator Capture(List<Action<ApiResponse<SeriesSummaryPageDto>>> callbacks, Action<ApiResponse<SeriesSummaryPageDto>> callback)
        { callbacks.Add(callback); yield break; }
    }
}
