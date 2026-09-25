using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Level5.BackendV2;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Proves the durability-ordering guarantee <c>RemoteAttemptResultSubmitter</c>'s own doc comment
/// states: the exact remote-attempt result is durably persisted to
/// <see cref="PendingRemoteAttemptResultStore"/> before the first <c>CompleteAttempt</c> network
/// request is ever sent, and a persistence failure never sends that request at all.
///
/// Runs in PlayMode because <see cref="RemoteAttemptResultSubmitter.TrySubmit"/> fires its network
/// call on a real <c>BackendV2CoroutineHost.StartCoroutine</c>, which does not advance in EditMode -
/// the same reasoning <c>BackendV2CompetitiveAttemptExclusionPlayModeTests</c> and
/// <c>BackendV2MatchResultRestartRecoveryPlayModeTests</c> document for the same call pair.
/// </summary>
public class BackendV2RemoteAttemptResultDurabilityPlayModeTests
{
    private readonly List<GameObject> hosts = new List<GameObject>();
    private string blockedStorePath;

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject host in hosts)
        {
            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        hosts.Clear();

        if (blockedStorePath != null && Directory.Exists(blockedStorePath))
        {
            Directory.Delete(blockedStorePath, recursive: true);
        }

        blockedStorePath = null;
        PendingRemoteAttemptResultStore.Clear();
        ActiveRemoteAttempt.Clear();
        ActiveMatch.Clear();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
    }

    private static RemoteAttemptContext Context(Guid playerId, Guid seriesId, Guid attemptId)
    {
        return new RemoteAttemptContext(
            seriesId: seriesId,
            gameNumber: 1,
            attemptId: attemptId,
            playerId: playerId,
            rulesetId: "most-points",
            rulesetVersion: 1,
            competitionProtocolVersion: 1,
            comparisonKeys: Array.Empty<ComparisonKeySummaryDto>(),
            requiredResultMetrics: new[] { "Score" });
    }

    private GameStats CreateStats(int totalPoints)
    {
        GameObject statsHost = new GameObject("durability-ordering-stats");
        hosts.Add(statsHost);
        GameStats stats = statsHost.AddComponent<GameStats>();
        stats.TotalPoints = totalPoints;
        return stats;
    }

    [UnityTest]
    public IEnumerator ThePendingResultIsDurablyReadableBeforeTheFirstNetworkRequestIsSent()
    {
        Guid playerId = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid attemptId = Guid.NewGuid();
        BackendV2SessionStore.Set(new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30)));

        RemoteAttemptContext context = Context(playerId, seriesId, attemptId);
        ActiveRemoteAttempt.Begin(context, launchedMatch: null);

        bool storeWasDurableWhenRequestArrived = false;
        RecordingApiTransport transport = new RecordingApiTransport();
        transport.Handler = _ =>
        {
            // The whole point of this test: check the durable store's own state (never the
            // in-process facade, which could be satisfied by an in-memory-only shortcut) at the
            // exact moment the first network request is sent.
            storeWasDurableWhenRequestArrived =
                PendingRemoteAttemptResultStore.TryGetForOwner(playerId, out PendingRemoteAttemptResultEntry entry)
                && entry.Context.AttemptId == attemptId
                && entry.Metrics.TryGetValue("Score", out double score) && score == 150;
            return RawApiResponse.Completed(200, SeriesResponseJson());
        };
        BackendV2Runtime.Override(transport);

        RemoteAttemptResultSubmitter.TrySubmit(CreateStats(150), GameModeId.TotalPoints, 60f);

        yield return null;
        yield return null;
        yield return null;

        Assert.That(transport.Requests, Has.Count.EqualTo(1),
            "the submission must have actually reached the transport for this test to be conclusive");
        Assert.That(storeWasDurableWhenRequestArrived, Is.True,
            "the exact result must already be durably persisted before the first CompleteAttempt " +
            "request is sent, not merely stashed in memory");
    }

    [UnityTest]
    public IEnumerator APersistenceFailureNeverSendsTheNetworkRequest()
    {
        Guid playerId = Guid.NewGuid();
        BackendV2SessionStore.Set(new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30)));

        RemoteAttemptContext context = Context(playerId, Guid.NewGuid(), Guid.NewGuid());
        ActiveRemoteAttempt.Begin(context, launchedMatch: null);

        // Forces PendingRemoteAttemptResultStore.Save to fail deterministically: AtomicFile.WriteAllText
        // ultimately calls File.Move(tempPath, path) when no file exists yet at path - pre-creating a
        // directory at that exact path makes that move throw, which Save's own try/catch turns into a
        // reported Failed outcome rather than a silent loss. LogAssert.ignoreFailingMessages covers the
        // resulting Debug.LogError calls (the store's own save failure, retried once, plus the
        // submitter's refusal to submit) - this test is about the network call never firing, not about
        // matching those diagnostic messages verbatim.
        blockedStorePath = Path.Combine(Application.persistentDataPath, "backendv2_pending_remote_attempt_results.json");
        Directory.CreateDirectory(blockedStorePath);

        RecordingApiTransport transport = new RecordingApiTransport();
        BackendV2Runtime.Override(transport);

        LogAssert.ignoreFailingMessages = true;
        try
        {
            RemoteAttemptResultSubmitter.TrySubmit(CreateStats(150), GameModeId.TotalPoints, 60f);

            yield return null;
            yield return null;
            yield return null;
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }

        Assert.That(transport.Requests, Is.Empty,
            "a durable-persistence failure must never be followed by an ambiguous network submission");

        // try/finally rather than a bare claim+release: if the claim assertion below fails - the
        // exact regression this checks for - Release must still run, or this static claim table
        // leaks into whatever test happens to run next in this process.
        bool reclaimed = RemoteAttemptResultSubmitter.TryClaim(context.AttemptId);
        try
        {
            Assert.That(reclaimed, Is.True,
                "the in-flight claim must have been released so a later retry is not permanently locked out");
        }
        finally
        {
            RemoteAttemptResultSubmitter.Release(context.AttemptId);
        }
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
            ""currentGameNumber"": 1,
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
                { ""gameNumber"": 1, ""yourAttempt"": null, ""opponentAttempt"": null }
            ]
        }";
    }

    /// <summary>Local, minimal counterpart to the EditMode-only <c>FakeApiTransport</c>
    /// (<c>Assets/Tests/Editor/FakeApiTransport.cs</c>): Editor-only test doubles live in an implicit
    /// assembly this PlayMode test assembly cannot reference, so this is re-typed rather than
    /// shared - the same approach <c>BackendV2MatchResultRestartRecoveryPlayModeTests</c> already
    /// takes.</summary>
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
    }
}
