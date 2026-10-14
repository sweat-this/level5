using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level5.BackendV2;
using Level5.Core.Versus;
using Level5.Core.Match;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2RematchTests
    {
        private Guid player;
        private Guid opponent;
        private SeriesResponseDto detail;
        private CompetitiveRulesetCatalog catalog;
        [SetUp] public void SetUp()
        {
            player = Guid.NewGuid(); opponent = Guid.NewGuid();
            BackendV2SessionStore.Set(new BackendV2Session("access", DateTimeOffset.UtcNow.AddHours(1), player, "refresh", DateTimeOffset.UtcNow.AddDays(1)));
            detail = new SeriesResponseDto { Id = Guid.NewGuid(), ChallengerId = opponent, OpponentId = player,
                Status = "Completed", TotalGames = 3, Revision = 9, WinnerId = player,
                Rules = new FrozenRulesDto { CompetitionProtocolVersion = 1, RulesetId = "most-points", RulesetVersion = 1, InformationPolicy = "SealedAttempt" } };
            catalog = new CompetitiveRulesetCatalog(new[] { new CompetitiveRuleset(new RulesetId("most-points"), 1, GameModeId.TotalPoints,
                VersusCapability.Asynchronous, new[] { ComparisonKey.Highest(AttemptMetric.Score) }) });
        }
        [TearDown] public void TearDown() { BackendV2Runtime.Reset(); BackendV2SessionStore.Clear(); }
        private ChallengeFormState Draft() => CorrespondenceRematch.CreateDraft(detail, player, catalog, out _);
        [Test] public void DraftUsesOppositeGuidExactRulesAndNeverMutatesCompletedSeries()
        {
            string before = BackendV2Json.Serialize(detail); var draft = Draft();
            Assert.That(draft.OpponentId, Is.EqualTo(opponent)); Assert.That(draft.RulesetVersion, Is.EqualTo(1)); Assert.That(draft.InformationPolicy, Is.EqualTo("SealedAttempt"));
            Assert.That(draft.ClientRequestId, Is.Null); Assert.That(BackendV2Json.Serialize(detail), Is.EqualTo(before));
            detail.ChallengerId = player; detail.OpponentId = opponent; Assert.That(Draft().OpponentId, Is.EqualTo(opponent));
        }
        [TestCase("Declined")] [TestCase("Cancelled")] [TestCase("Expired")] [TestCase("Active")]
        public void OtherStatusesCannotRematch(string status) { detail.Status = status; Assert.That(Draft(), Is.Null); }
        [Test] public void NonParticipantCannotRematch() { Assert.That(CorrespondenceRematch.CreateDraft(detail, Guid.NewGuid(), catalog, out _), Is.Null); }
        [TestCase(0)] [TestCase(2)] public void UnsupportedHistoricVersionIsNotSilentlyReplaced(int version)
        { detail.Rules.RulesetVersion = version; Assert.That(Draft(), Is.Null); }
        [Test] public void UnsupportedFormatIsNotReplaced() { detail.TotalGames = 9; Assert.That(Draft(), Is.Null); }
        [Test] public void EditingAnySemanticFieldStartsANewLogicalSubmission()
        {
            var form = Draft(); var changes = new Action[] { () => form.OpponentId = Guid.NewGuid(), () => form.RulesetId = "another",
                () => form.RulesetVersion = 2, () => form.TotalGames = 5, () => form.InformationPolicy = "OpenTarget" };
            foreach (var change in changes) { Guid before = form.GetOrBeginClientRequestId(); change(); Assert.That(form.GetOrBeginClientRequestId(), Is.Not.EqualTo(before)); }
        }
        [Test] public void AssigningSamePayloadAndRetryPreserveIdentity()
        { var form = Draft(); Guid before = form.GetOrBeginClientRequestId(); form.OpponentId = opponent; form.TotalGames = 3; form.RulesetVersion = 1; Assert.That(form.GetOrBeginClientRequestId(), Is.EqualTo(before)); }
        [Test] public void RematchUsesNormalCreateAndNetworkRetryHasIdenticalPayload()
        {
            var transport = new FakeApiTransport(); transport.Enqueue(RawApiResponse.NetworkError()); transport.Enqueue(RawApiResponse.Completed(200, BackendV2Json.Serialize(detail))); BackendV2Runtime.Override(transport);
            var form = Draft(); var commands = new ChallengeCoordinator();
            CoroutineTestRunner.RunToCompletion(commands.Create(form, _ => { })); CoroutineTestRunner.RunToCompletion(commands.Create(form, _ => { }));
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
            Assert.That(transport.Requests.All(r => r.RelativePath == "api/v2/series" && r.Method == ApiHttpMethod.Post), Is.True);
            var request = (CreateChallengeDto)transport.Requests[0].Body; Assert.That(request.OpponentId, Is.EqualTo(opponent)); Assert.That(request.RulesetVersion, Is.EqualTo(1));
            Assert.That(BackendV2Json.Serialize(transport.Requests[1].Body), Is.EqualTo(BackendV2Json.Serialize(request)));
        }
        [Test] public void ConcurrentCreateIsRefusedBeforeSecondServerCommand()
        {
            var transport = new FakeApiTransport(); transport.Enqueue(RawApiResponse.NetworkError()); BackendV2Runtime.Override(transport);
            var commands = new ChallengeCoordinator(); var form = Draft(); var first = commands.Create(form, _ => { }); Assert.That(first.MoveNext(), Is.True);
            ApiResponse<SeriesResponseDto> rejected = null; CoroutineTestRunner.RunToCompletion(commands.Create(form, r => rejected = r));
            Assert.That(rejected.ErrorKind, Is.EqualTo(ApiErrorKind.Conflict)); Assert.That(transport.Requests, Is.Empty);
            CoroutineTestRunner.RunToCompletion((IEnumerator)first.Current); CoroutineTestRunner.RunToCompletion(first);
            Assert.That(transport.Requests.Count, Is.EqualTo(1)); Assert.That(commands.IsCreating, Is.False);
        }
        [Test] public void FreshFriendRemovalPreventsPreparingDraftAndSendsNoCreate()
        {
            var transport = new FakeApiTransport(); transport.Enqueue(RawApiResponse.Completed(200, BackendV2Json.Serialize(detail))); transport.Enqueue(RawApiResponse.Completed(200, "[]")); BackendV2Runtime.Override(transport);
            ChallengeFormState form = null; string error = null; CoroutineTestRunner.RunToCompletion(CorrespondenceRematch.Prepare(detail.Id, catalog, (f, e) => { form = f; error = e; }));
            Assert.That(form, Is.Null); Assert.That(error, Does.Contain("accepted friend")); Assert.That(transport.Requests.Count, Is.EqualTo(2)); Assert.That(transport.Requests.All(r => r.Method == ApiHttpMethod.Get), Is.True);
        }
        [Test] public void FreshDetailAndFriendshipPrepareOnlyANewDraft()
        {
            var transport = new FakeApiTransport(); transport.Enqueue(RawApiResponse.Completed(200, BackendV2Json.Serialize(detail)));
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Json.Serialize(new[] { new FriendSummaryDto { PlayerId = opponent } }))); BackendV2Runtime.Override(transport);
            ChallengeFormState form = null; CoroutineTestRunner.RunToCompletion(CorrespondenceRematch.Prepare(detail.Id, catalog, (f, e) => { form = f; Assert.That(e, Is.Null); }));
            Assert.That(form.OpponentId, Is.EqualTo(opponent)); Assert.That(form.ClientRequestId, Is.Null); Assert.That(transport.Requests.Count, Is.EqualTo(2));
        }
        [Test] public void AdditivePublicIdentityDeserializesAndGuidChoosesOpponent()
        {
            var identity = new PlayerProfileResponseDto { PlayerId = opponent, DisplayName = "Grace", Tag = "GRACE#5678" };
            var summary = new SeriesSummaryDto { Id = detail.Id, ChallengerId = opponent, OpponentId = player, Challenger = identity, Status = "Completed" };
            Assert.That(BackendV2Json.TryDeserialize(BackendV2Json.Serialize(summary), out SeriesSummaryDto parsed), Is.True); Assert.That(CorrespondenceInbox.OpponentLabel(parsed, player), Is.EqualTo("Grace (GRACE#5678)"));
            detail.Challenger = identity; Assert.That(BackendV2Json.TryDeserialize(BackendV2Json.Serialize(detail), out SeriesResponseDto parsedDetail), Is.True); Assert.That(parsedDetail.Challenger.PlayerId, Is.EqualTo(opponent));
            parsed.Challenger.PlayerId = player; Assert.That(CorrespondenceInbox.OpponentLabel(parsed, player), Is.EqualTo(opponent.ToString()));
        }
    }
}
