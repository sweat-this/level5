using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Process-restart proof for correspondence-attempt result delivery: a result durably persisted
/// before "restart" is resendable once the same Backend V2 player's session is restored, driven
/// through the real application-startup seam (<c>UserAccountManager.Awake</c> -&gt;
/// <c>BackendV2SessionPersistenceBootstrap.EnsureInitialized</c>), the same seam
/// <see cref="BackendV2MatchResultRestartRecoveryPlayModeTests"/> already proves session restoration
/// through for the separate general-match-result queue.
///
/// No active gameplay context (<c>ActiveRemoteAttempt</c>, a loaded match scene, live
/// <c>GameStats</c>) is set up or required here - <see cref="RemoteAttemptResultSubmitter.TryRetryPending"/>
/// resends the exact payload already durably persisted by <see cref="PendingRemoteAttemptResultStore"/>,
/// never anything rebuilt from gameplay state.
/// </summary>
public class BackendV2RemoteAttemptResultRestartRecoveryPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        PendingRemoteAttemptResultStore.Clear();
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
    }

    private static RemoteAttemptContext Context(Guid ownerPlayerId, Guid seriesId, Guid attemptId)
    {
        return new RemoteAttemptContext(
            seriesId: seriesId,
            gameNumber: 2,
            attemptId: attemptId,
            playerId: ownerPlayerId,
            rulesetId: "most-points",
            rulesetVersion: 1,
            competitionProtocolVersion: 1,
            comparisonKeys: Array.Empty<ComparisonKeySummaryDto>(),
            requiredResultMetrics: new[] { "Score" });
    }

    private static void PersistSession(Guid playerId)
    {
        BackendV2SessionPersistenceStore.Save(new BackendV2Session(
            "restart-recovery-access-token", DateTimeOffset.UtcNow.AddHours(1), playerId,
            "restart-recovery-refresh-token", DateTimeOffset.UtcNow.AddDays(30)));
    }

    [UnityTest]
    public IEnumerator APendingResultIsResendableOnceTheSameOwnersSessionIsRestoredAfterRestart()
    {
        Guid owner = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid attemptId = Guid.NewGuid();
        PendingRemoteAttemptResultStore.Enqueue(
            owner, Context(owner, seriesId, attemptId), new Dictionary<string, double> { ["Score"] = 77 });
        PersistSession(owner);
        BackendV2SessionStore.Clear();

        RecordingApiTransport transport = new RecordingApiTransport();
        BackendV2Runtime.Override(transport);

        yield return SceneManager.LoadSceneAsync("level_00_account_loginLocal");
        yield return null;
        yield return null;
        yield return null;

        if (!BackendV2SessionStore.IsAuthenticated)
        {
            Assert.Ignore(
                "BackendV2SessionPersistenceBootstrap.EnsureInitialized() had already run earlier in "
                + "this process - see BackendV2SessionRestorationPlayModeTests' own doc comment for "
                + "why this run cannot prove restoration from a fresh process. Run this fixture in "
                + "isolation via -testFilter BackendV2RemoteAttemptResultRestartRecoveryPlayModeTests.");
        }

        bool started = RemoteAttemptResultSubmitter.TryRetryPending();
        Assert.That(started, Is.True, "a pending result owned by the now-restored session must be resendable");
        yield return null;
        yield return null;
        yield return null;

        Assert.That(transport.Requests, Has.Count.EqualTo(1));
        Assert.That(
            transport.Requests[0].RelativePath,
            Is.EqualTo($"api/v2/series/{seriesId}/games/2/attempts/{attemptId}/complete"),
            "the resend must submit the exact original attempt, never a rebuilt or substitute one");
        Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(owner, out _), Is.False,
            "a successfully delivered pending result must be removed from the store");
    }

    [UnityTest]
    public IEnumerator ADifferentAuthenticatedPlayerNeverResendsAnotherPlayersPendingResult()
    {
        Guid owner = Guid.NewGuid();
        Guid otherPlayer = Guid.NewGuid();
        PendingRemoteAttemptResultStore.Enqueue(
            owner, Context(owner, Guid.NewGuid(), Guid.NewGuid()), new Dictionary<string, double> { ["Score"] = 1 });
        PersistSession(otherPlayer);
        BackendV2SessionStore.Clear();

        RecordingApiTransport transport = new RecordingApiTransport();
        BackendV2Runtime.Override(transport);

        yield return SceneManager.LoadSceneAsync("level_00_account_loginLocal");
        yield return null;
        yield return null;
        yield return null;

        if (!BackendV2SessionStore.IsAuthenticated || BackendV2SessionStore.Current.PlayerId != otherPlayer)
        {
            Assert.Ignore(
                "BackendV2SessionPersistenceBootstrap.EnsureInitialized() had already run earlier in "
                + "this process with a different persisted session - see "
                + "BackendV2SessionRestorationPlayModeTests' own doc comment. Run this fixture in "
                + "isolation via -testFilter BackendV2RemoteAttemptResultRestartRecoveryPlayModeTests.");
        }

        bool started = RemoteAttemptResultSubmitter.TryRetryPending();

        Assert.That(started, Is.False,
            "a result owned by a player other than the currently authenticated one must never be resendable");
        yield return null;
        yield return null;

        Assert.That(transport.Requests, Is.Empty,
            "a result owned by a player other than the currently authenticated one must never be sent");
        Assert.That(PendingRemoteAttemptResultStore.TryGetForOwner(owner, out _), Is.True,
            "the entry must remain persisted, untouched, for its actual owner");
    }

    /// <summary>Local, minimal counterpart to the EditMode-only <c>FakeApiTransport</c>
    /// (<c>Assets/Tests/Editor/FakeApiTransport.cs</c>): Editor-only test doubles live in an implicit
    /// assembly this PlayMode test assembly cannot reference, so this is re-typed rather than shared -
    /// the same approach <c>BackendV2MatchResultRestartRecoveryPlayModeTests</c> already takes.</summary>
    private sealed class RecordingApiTransport : IApiTransport
    {
        public List<ApiRequest> Requests { get; } = new List<ApiRequest>();

        public Func<ApiRequest, RawApiResponse> Handler { get; set; }

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            RawApiResponse response = Handler != null ? Handler(request) : RawApiResponse.Completed(200, SeriesResponseJson());
            completed?.Invoke(response);
            yield break;
        }

        private static string SeriesResponseJson()
        {
            return @"{
                ""id"": ""4c23c3d8-4040-4b1f-8d3f-9b8d3c2e5f6a"",
                ""challengerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
                ""opponentId"": ""1b645389-2473-467d-9073-72d45eb05abc"",
                ""status"": ""Active"",
                ""totalGames"": 3,
                ""gamesToWin"": 2,
                ""currentGameNumber"": 2,
                ""revision"": 5,
                ""winnerId"": null,
                ""createdAt"": ""2026-09-01T00:00:00+00:00"",
                ""completedAt"": null,
                ""rules"": {
                    ""competitionProtocolVersion"": 1,
                    ""rulesetId"": ""most-points"",
                    ""rulesetVersion"": 1,
                    ""minimumCompatibleVersion"": 1,
                    ""modeId"": ""most-points"",
                    ""informationPolicy"": ""SealedAttempt"",
                    ""alternatesFirstAttempt"": true,
                    ""comparisonKeys"": [{ ""metric"": ""Score"", ""direction"": ""HigherWins"" }]
                },
                ""games"": [
                    { ""gameNumber"": 2, ""yourAttempt"": null, ""opponentAttempt"": null }
                ]
            }";
        }
    }
}
