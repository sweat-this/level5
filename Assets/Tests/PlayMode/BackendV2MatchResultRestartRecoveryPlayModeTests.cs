using System;
using System.Collections;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Process-restart proof for general match-result delivery: a result queued before "restart" is
/// retried once the same Backend V2 player's session is restored, driven through the real
/// application-startup seam (<c>UserAccountManager.Awake</c> ->
/// <c>BackendV2SessionPersistenceBootstrap.EnsureInitialized</c>), the same seam
/// <see cref="BackendV2SessionRestorationPlayModeTests"/> already proves session restoration through.
///
/// Unlike the EditMode coordinator tests (which drive <c>MatchResultSubmissionCoordinator.Drain</c>
/// directly, since <c>MonoBehaviour.StartCoroutine</c> does not advance in EditMode), this runs in
/// PlayMode, so <c>TriggerDrain</c>'s real <c>BackendV2CoroutineHost.StartCoroutine</c> call actually
/// executes across the yielded frames below - end-to-end evidence, not a simulated pump.
/// </summary>
public class BackendV2MatchResultRestartRecoveryPlayModeTests
{
    [TearDown]
    public void TearDown()
    {
        PendingMatchResultStore.Clear();
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
    }

    private static SubmitMatchResultDto Request(Guid clientResultId)
    {
        return new SubmitMatchResultDto(
            clientResultId, modeId: 3, levelId: 7, characterId: "12", clientVersion: "1.4.2",
            platform: "Handheld",
            metrics: new Dictionary<string, double> { [MatchResultMetric.TotalPoints.ToString()] = 100 },
            modifiers: new MatchResultModifiersDto());
    }

    private static void PersistSession(Guid playerId)
    {
        BackendV2SessionPersistenceStore.Save(new BackendV2Session(
            "restart-recovery-access-token", DateTimeOffset.UtcNow.AddHours(1), playerId,
            "restart-recovery-refresh-token", DateTimeOffset.UtcNow.AddDays(30)));
    }

    [UnityTest]
    public IEnumerator APendingResultIsRetriedOnceTheSameOwnersSessionIsRestoredAfterRestart()
    {
        Guid owner = Guid.NewGuid();
        Guid clientResultId = Guid.NewGuid();
        PendingMatchResultStore.Enqueue(owner, Request(clientResultId));
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
                + "isolation via -testFilter BackendV2MatchResultRestartRecoveryPlayModeTests.");
        }

        MatchResultSubmissionCoordinator.TriggerDrain();
        yield return null;
        yield return null;
        yield return null;

        Assert.That(transport.Requests, Has.Count.EqualTo(1));
        Assert.That(transport.Requests[0].RelativePath, Is.EqualTo("api/v2/match-results"));
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Is.Empty,
            "a successfully delivered pending result must be removed from the queue");
    }

    [UnityTest]
    public IEnumerator ADifferentAuthenticatedPlayerNeverSubmitsAnotherPlayersPendingResult()
    {
        Guid owner = Guid.NewGuid();
        Guid otherPlayer = Guid.NewGuid();
        PendingMatchResultStore.Enqueue(owner, Request(Guid.NewGuid()));
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
                + "isolation via -testFilter BackendV2MatchResultRestartRecoveryPlayModeTests.");
        }

        MatchResultSubmissionCoordinator.TriggerDrain();
        yield return null;
        yield return null;
        yield return null;

        Assert.That(transport.Requests, Is.Empty,
            "a result owned by a player other than the currently authenticated one must never be sent");
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1),
            "the entry must remain queued, untouched, for its actual owner");
    }

    /// <summary>Local, minimal counterpart to the EditMode-only <c>FakeApiTransport</c>
    /// (<c>Assets/Tests/Editor/FakeApiTransport.cs</c>): Editor-only test doubles live in an implicit
    /// assembly this PlayMode test assembly cannot reference, so this is re-typed rather than
    /// shared - the same approach <see cref="BackendV2SessionRestorationPlayModeTests"/> already
    /// takes.</summary>
    private sealed class RecordingApiTransport : IApiTransport
    {
        public List<ApiRequest> Requests { get; } = new List<ApiRequest>();

        public Func<ApiRequest, RawApiResponse> Handler { get; set; }

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            RawApiResponse response = Handler != null ? Handler(request) : RawApiResponse.Completed(200, MatchResultResponseJson());
            completed?.Invoke(response);
            yield break;
        }

        private static string MatchResultResponseJson()
        {
            return "{"
                + "\"id\":\"" + Guid.NewGuid() + "\","
                + "\"playerId\":\"" + Guid.NewGuid() + "\","
                + "\"clientResultId\":\"" + Guid.NewGuid() + "\","
                + "\"modeId\":3,\"levelId\":7,\"characterId\":\"12\","
                + "\"clientVersion\":\"1.4.2\",\"platform\":\"Handheld\","
                + "\"metrics\":{\"TotalPoints\":100.0},"
                + "\"modifiers\":{\"hardcore\":false,\"trafficEnabled\":false,\"enemiesEnabled\":false,\"sniperEnabled\":false},"
                + "\"createdAt\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\""
                + "}";
        }
    }
}
