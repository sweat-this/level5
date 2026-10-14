using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Level5.BackendV2;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Opt-in, bounded real Backend V2 calls using newly registered disposable accounts only.</summary>
public class BackendV2LiveEngagementCertificationTests
{
    private readonly List<BackendV2Session> sessions = new List<BackendV2Session>();
    private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
    private BackendV2Session previous;
    private bool enabled;

    [SetUp]
    public void SetUp()
    {
        enabled = Environment.GetEnvironmentVariable("LEVEL5_ENGAGEMENT_LIVE") == "1";
        if (!enabled) Assert.Ignore("Requires explicit local disposable-account live certification.");
        previous = BackendV2SessionStore.Current;
        string path = Path.Combine(Application.persistentDataPath, "backendv2_session.json");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) files[path + suffix] = File.Exists(path + suffix) ? File.ReadAllBytes(path + suffix) : null;
        BackendV2SessionPersistenceBootstrap.EnsureInitialized();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
        BackendV2ApiConfigProvider.Override(BackendV2ApiConfig.Development());
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!enabled) yield break;
        foreach (BackendV2Session session in sessions)
        {
            BackendV2SessionStore.Set(session);
            yield return BackendV2Runtime.Session.Logout();
        }
        BackendV2SessionStore.Clear();
        if (previous != null) BackendV2SessionStore.Set(previous);
        foreach (var file in files)
        {
            if (file.Value == null) { if (File.Exists(file.Key)) File.Delete(file.Key); }
            else File.WriteAllBytes(file.Key, file.Value);
        }
        sessions.Clear(); files.Clear();
        BackendV2Runtime.Reset(); BackendV2ApiConfigProvider.Reset();
    }

    private static T Require<T>(ApiResponse<T> response)
    {
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True, "Backend call failed: " + response.ErrorKind + " / " + response.Problem?.Code);
        return response.Value;
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator PublicIdentitySealedTurnsCompletedHistoryAndExactVersionRematchUseRealServer()
    {
        string suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
        ApiResponse<BackendV2Session> registration = null;
        yield return BackendV2Runtime.Session.Register("engagea" + suffix, "Engagement!" + Guid.NewGuid().ToString("N"), "Engagement A", r => registration = r);
        BackendV2Session a = Require(registration); sessions.Add(a);
        yield return BackendV2Runtime.Session.Register("engageb" + suffix, "Engagement!" + Guid.NewGuid().ToString("N"), "Engagement B", r => registration = r);
        BackendV2Session b = Require(registration); sessions.Add(b);
        BackendV2SessionStore.Set(a);
        ApiResponse<FriendRequestResponseDto> friendRequest = null;
        yield return BackendV2Runtime.Friends.SendRequest(b.PlayerId, r => friendRequest = r);
        Guid friendRequestId = Require(friendRequest).Id;
        BackendV2SessionStore.Set(b);
        ApiResponse<ApiVoid> empty = null;
        yield return BackendV2Runtime.Friends.Accept(friendRequestId, r => empty = r); Require(empty);
        BackendV2SessionStore.Set(a);
        var form = new ChallengeFormState { OpponentId = b.PlayerId, RulesetId = "most-points", RulesetVersion = 1, TotalGames = 1, InformationPolicy = "SealedAttempt" };
        var commands = new ChallengeCoordinator();
        ApiResponse<SeriesResponseDto> series = null;
        yield return commands.Create(form, r => series = r);
        Guid id = Require(series).Id;
        BackendV2SessionStore.Set(b);
        ApiResponse<SeriesSummaryPageDto> page = null;
        yield return BackendV2Runtime.Correspondence.ListIncoming(20, null, r => page = r);
        var inboxState = new ListViewState<SeriesSummaryDto>(); inboxState.ReplaceWith(Require(page).Items);
        Assert.That(CorrespondenceInbox.Build(b.PlayerId, new ActiveSeriesTurnClassifier(), inboxState).Single(r => r.Series.Id == id).Category,
            Is.EqualTo(InboxCategory.Incoming), "current PendingAcceptance summaries must be actionable");
        yield return BackendV2Runtime.Correspondence.Accept(id, r => series = r); Require(series);
        foreach (BackendV2Session account in new[] { a, b })
        {
            BackendV2SessionStore.Set(account);
            yield return BackendV2Runtime.Correspondence.Get(id, r => series = r);
            var detail = Require(series);
            Assert.That(detail.Challenger.PlayerId, Is.EqualTo(a.PlayerId)); Assert.That(detail.Opponent.PlayerId, Is.EqualTo(b.PlayerId));
            Assert.That(SeriesRowClassifier.ClassifyTurn(detail, account.PlayerId), Is.EqualTo(ActiveSeriesTurn.YourTurn));
            if (account == b) Assert.That(detail.Games.Single().OpponentAttempt.Result, Is.Null, "sealed result must remain hidden");
            ApiResponse<AttemptDescriptorDto> started = null;
            yield return BackendV2Runtime.Correspondence.StartAttempt(id, 1, r => started = r);
            var descriptor = Require(started);
            var metrics = descriptor.RequiredResultMetrics.ToDictionary(metric => metric, _ => 1d);
            foreach (var key in descriptor.ComparisonKeys)
                metrics[key.Metric] = (key.Direction == "HigherWins") == (account == a) ? 100 : 1;
            yield return BackendV2Runtime.Correspondence.CompleteAttempt(id, 1, descriptor.AttemptId, metrics, r => series = r); Require(series);
        }
        BackendV2SessionStore.Set(a);
        yield return BackendV2Runtime.Correspondence.Get(id, r => series = r);
        var completed = Require(series); Assert.That(completed.Status, Is.EqualTo("Completed")); Assert.That(completed.WinnerId, Is.EqualTo(a.PlayerId));
        long revision = completed.Revision;
        yield return BackendV2Runtime.Correspondence.ListCompleted(20, null, r => page = r);
        var summary = Require(page).Items.Single(s => s.Id == id);
        Assert.That(CorrespondenceInbox.OpponentLabel(summary, a.PlayerId), Does.Contain("Engagement B"));
        ChallengeFormState rematch = null; string error = null;
        yield return CorrespondenceRematch.Prepare(id, VersusCatalogs.Rulesets, (f, e) => { rematch = f; error = e; });
        Assert.That(error, Is.Null); Assert.That(rematch, Is.Not.Null); Assert.That(rematch.RulesetVersion, Is.EqualTo(completed.Rules.RulesetVersion));
        yield return commands.Create(rematch, r => series = r);
        Guid rematchId = Require(series).Id; Assert.That(rematchId, Is.Not.EqualTo(id));
        yield return commands.Create(rematch, r => series = r); Assert.That(Require(series).Id, Is.EqualTo(rematchId), "same logical retry must return original rematch");
        yield return BackendV2Runtime.Correspondence.Get(id, r => series = r); Assert.That(Require(series).Revision, Is.EqualTo(revision));
        yield return BackendV2Runtime.Correspondence.Cancel(rematchId, r => series = r); Require(series);
        yield return BackendV2Runtime.Friends.Remove(b.PlayerId, r => empty = r); Require(empty);
        yield return CorrespondenceRematch.Prepare(id, VersusCatalogs.Rulesets, (f, e) => { rematch = f; error = e; });
        Assert.That(rematch, Is.Null); Assert.That(error, Does.Contain("accepted friend"));
        // Audit-only probe: no production Unity notification API is introduced by this feature.
        foreach (BackendV2Session account in new[] { a, b })
        {
            var probe = new UnityWebRequestTransport(BackendV2ApiConfig.Development(), () => account.AccessToken);
            RawApiResponse notifications = null;
            yield return probe.Send(new ApiRequest(ApiHttpMethod.Get, "api/v2/platform/me/notifications", requiresAuth: true), r => notifications = r);
            Assert.That(notifications.StatusCode, Is.EqualTo(200));
            Assert.That(BackendV2Json.TryDeserialize(notifications.Body, out NotificationPageProbe notificationPage), Is.True);
            Assert.That(notificationPage.Items.Count, Is.EqualTo(account == a ? 0 : 1));
            Assert.That(notificationPage.Items.All(n => n.Source == "platform" && n.Kind == "friend-request-received"), Is.True,
                "correspondence has no verified notification producer and must not unexpectedly emit one");
        }
        Debug.Log("[ENGAGEMENT-LIVE] PASS: two disposable players, public identity, sealed turns, completed history, exact-version rematch, idempotent replay, immutable completed revision, and removed-friend rejection. Tokens revoked in teardown.");
    }
    private sealed class NotificationPageProbe
    {
        public List<NotificationProbe> Items { get; set; }
    }
    private sealed class NotificationProbe
    {
        public string Source { get; set; }
        public string Kind { get; set; }
    }

}
