using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.database;
using Level5.BackendV2;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Request-path proof for the general/competitive exclusion in
/// <see cref="BackendV2MatchResultSubmission.TryQueue"/>: an active remote correspondence attempt's
/// match end must produce zero requests to the general <c>api/v2/match-results</c> endpoint and
/// exactly one to its own <c>.../attempts/{attempt}/complete</c> endpoint.
///
/// Runs in PlayMode because <see cref="RemoteAttemptResultSubmitter.TrySubmit"/> fires its network
/// call on a real <c>BackendV2CoroutineHost.StartCoroutine</c>, which does not advance in EditMode -
/// the same reasoning <c>BackendV2MatchResultRestartRecoveryPlayModeTests</c> documents for the
/// general-result side of this same call pair.
/// </summary>
public class BackendV2CompetitiveAttemptExclusionPlayModeTests
{
    private readonly List<GameObject> hosts = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject host in hosts)
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        hosts.Clear();
        PendingMatchResultStore.Clear();
        PendingRemoteAttemptResult.Clear();
        ActiveRemoteAttempt.Clear();
        ActiveMatch.Clear();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
    }

    [UnityTest]
    public IEnumerator ARemoteCorrespondenceMatchEndNeverReachesTheGeneralResultEndpoint()
    {
        Guid playerId = Guid.NewGuid();
        BackendV2SessionStore.Set(new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30)));

        // launchedMatch: null keeps the attempt active regardless of ActiveMatch.Configuration -
        // appropriate here, since which exact match object is current is not what this test is
        // proving (the stale-attempt edge case has its own focused EditMode coverage in
        // Level5BackendV2MatchResultSubmissionTests).
        RemoteAttemptContext context = new RemoteAttemptContext(
            seriesId: Guid.NewGuid(),
            gameNumber: 1,
            attemptId: Guid.NewGuid(),
            playerId: playerId,
            rulesetId: "most-points",
            rulesetVersion: 1,
            competitionProtocolVersion: 1,
            comparisonKeys: Array.Empty<ComparisonKeySummaryDto>(),
            requiredResultMetrics: new[] { "Score" });
        ActiveRemoteAttempt.Begin(context, launchedMatch: null);

        RecordingApiTransport transport = new RecordingApiTransport();
        BackendV2Runtime.Override(transport);

        // The exact two calls GameRules.HandleMatchEnded makes at match end, in order: the general
        // submission first, then the correspondence submission.
        HighScoreModel score = new HighScoreModel
        {
            Scoreid = Guid.NewGuid().ToString("N"),
            Modeid = 3,
            Levelid = 7,
            Characterid = 12,
            Version = "1.4.2",
            Platform = "Handheld",
            TotalPoints = 120,
        };
        BackendV2MatchResultSubmission.TryQueue(score);

        GameObject statsHost = new GameObject("competitive-exclusion-stats");
        hosts.Add(statsHost);
        GameStats stats = statsHost.AddComponent<GameStats>();
        stats.TotalPoints = 120;
        stats.ShotMade = 12;
        stats.ShotAttempt = 20;

        RemoteAttemptResultSubmitter.TrySubmit(stats, GameModeId.TotalPoints, 60f);

        yield return null;
        yield return null;
        yield return null;

        Assert.That(
            transport.Requests.Count(r => r.RelativePath == "api/v2/match-results"),
            Is.EqualTo(0),
            "a remote correspondence attempt must never reach the general match-result endpoint");
        Assert.That(
            transport.Requests.Count(r => r.RelativePath == $"api/v2/series/{context.SeriesId}/games/{context.GameNumber}/attempts/{context.AttemptId}/complete"),
            Is.EqualTo(1),
            "the correspondence attempt must still be completed through its own endpoint");
        Assert.That(PendingMatchResultStore.GetRetryable(playerId), Is.Empty,
                "TryQueue must not have queued a general result for this attempt");
    }

    /// <summary>Local, minimal counterpart to the EditMode-only <c>FakeApiTransport</c>
    /// (<c>Assets/Tests/Editor/FakeApiTransport.cs</c>): Editor-only test doubles live in an
    /// implicit assembly this PlayMode test assembly cannot reference, so this is re-typed rather
    /// than shared - the same approach <c>BackendV2MatchResultRestartRecoveryPlayModeTests</c>
    /// already takes.</summary>
    private sealed class RecordingApiTransport : IApiTransport
    {
        public List<ApiRequest> Requests { get; } = new List<ApiRequest>();

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            completed?.Invoke(RawApiResponse.Completed(200, SeriesResponseJson()));
            yield break;
        }

        private static string SeriesResponseJson()
        {
            return @"{
                ""id"": ""4c23c3d8-4040-4b1f-8d3f-9b8d3c2e5f6a"",
                ""challengerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
                ""opponentId"": ""1b645389-2473-467d-9073-72d45eb05abc"",
                ""status"": ""Completed"",
                ""totalGames"": 3,
                ""gamesToWin"": 2,
                ""currentGameNumber"": 1,
                ""revision"": 5,
                ""winnerId"": ""8f14e45f-ceea-467e-a4d9-b3e5c76f1a3a"",
                ""createdAt"": ""2026-09-01T00:00:00+00:00"",
                ""completedAt"": ""2026-09-02T00:00:00+00:00"",
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
    }
}
