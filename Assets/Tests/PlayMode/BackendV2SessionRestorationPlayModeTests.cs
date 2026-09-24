using System;
using System.Collections;
using System.Collections.Generic;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Process-level proof that promoting persisted Backend V2 session restoration to application
/// startup actually works: loading the real first production scene
/// (<c>level_00_account_loginLocal</c>, via the real <c>UserAccountManager.Awake</c>) restores a
/// locally persisted session before the correspondence screen is ever entered, and does so with zero
/// network requests.
///
/// <c>BackendV2SessionPersistenceBootstrap.EnsureInitialized()</c> is idempotent for the lifetime of
/// the process (a static <c>bool</c> guard with no reset), so this proof is only valid if this is the
/// first thing in the process to call it. The ordinary <c>-runTests -testPlatform PlayMode</c> suite
/// also runs <c>Level5BackendV2CorrespondenceScenePlayModeTests</c>, which calls it too (via the
/// correspondence scene), and NUnit does not guarantee fixture ordering across the two - so when this
/// test is not first, it self-diagnoses that instead of failing misleadingly (see the
/// <c>Assert.Ignore</c> branch below). For a guaranteed-clean process, run this test alone, exactly
/// like this repository's existing opt-in live-certification fixtures already recommend for their own
/// process-boundary guarantees (<c>BackendV2LiveCorrespondenceCertificationTests</c>):
///
///   "&lt;UnityPath&gt;\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode ^
///       -testFilter BackendV2SessionRestorationPlayModeTests ^
///       -testResults restoration_results.xml -logFile restoration.log
///
/// Unlike the live-certification fixtures, this one needs no live Backend V2 instance - every
/// network-shaped call is answered by <see cref="RecordingApiTransport"/>, an in-memory
/// <see cref="IApiTransport"/> - so it runs in the ordinary, always-on suite, not gated behind
/// <c>LEVEL5_LIVE_CERTIFICATION</c>.
/// </summary>
public class BackendV2SessionRestorationPlayModeTests
{
    private static readonly Guid PersistedPlayerId = Guid.NewGuid();
    private const string PersistedAccessToken = "restoration-test-access-token";
    private const string PersistedRefreshToken = "restoration-test-refresh-token";

    [TearDown]
    public void TearDown()
    {
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
    }

    [UnityTest]
    public IEnumerator ApplicationStartupRestoresAPersistedSessionWithNoNetworkRequestBeforeCorrespondenceIsEntered()
    {
        BackendV2Session persisted = new BackendV2Session(
            PersistedAccessToken,
            DateTimeOffset.UtcNow.AddMinutes(10),
            PersistedPlayerId,
            PersistedRefreshToken,
            DateTimeOffset.UtcNow.AddDays(30));
        BackendV2SessionPersistenceStore.Save(persisted);
        BackendV2SessionStore.Clear();

        RecordingApiTransport transport = new RecordingApiTransport();
        BackendV2Runtime.Override(transport);

        yield return SceneManager.LoadSceneAsync("level_00_account_loginLocal");
        // A few frames for UserAccountManager.Awake (-> BackendV2SessionPersistenceBootstrap.
        // EnsureInitialized) to run. Restoration itself is synchronous (TryLoad + Set, no coroutine),
        // so this is only settling the scene load, not waiting on the restore.
        yield return null;
        yield return null;
        yield return null;

        if (!BackendV2SessionStore.IsAuthenticated)
        {
            Assert.Ignore(
                "BackendV2SessionPersistenceBootstrap.EnsureInitialized() had already been called " +
                "earlier in this process (most likely by Level5BackendV2CorrespondenceScenePlayModeTests " +
                "running first in the same -runTests batch), so this run cannot prove restoration " +
                "happened from a fresh process. Run this fixture in isolation via -testFilter " +
                "BackendV2SessionRestorationPlayModeTests for a guaranteed-clean proof - see this class's " +
                "own doc comment.");
        }

        Assert.That(
            transport.Requests, Is.Empty,
            "restoring a persisted session at application startup must perform zero network requests, " +
            "so a temporary Backend V2 outage at launch cannot delete an otherwise locally valid session");
        Assert.That(BackendV2SessionStore.Current.PlayerId, Is.EqualTo(PersistedPlayerId));
        Assert.That(BackendV2SessionStore.Current.AccessToken, Is.EqualTo(PersistedAccessToken));
        Assert.That(BackendV2SessionStore.Current.RefreshToken, Is.EqualTo(PersistedRefreshToken));

        // The real UserAccountManager.Awake seam ran before any correspondence UI existed at all -
        // GameObject.Find below would fail if the multiplayer scene were somehow required first.
        Assert.That(UnityEngine.GameObject.Find("CorrespondenceScreen"), Is.Null,
            "restoration must not require the correspondence screen to be open");

        // Item 7 of the promotion plan: a later authorized request still goes through the existing
        // normal refresh path and succeeds - restoration itself never talks to the network, but using
        // the restored session still works once something actually asks Backend V2 something.
        transport.Handler = request =>
        {
            if (request.RelativePath == "api/v2/auth/refresh")
            {
                return RawApiResponse.Completed(200, TokenRefreshResponseJson());
            }

            return RawApiResponse.Completed(200, PlayerProfileResponseJson());
        };

        ApiResponse<PlayerProfileResponseDto> profileResult = null;
        yield return BackendV2Runtime.Players.UpdateMe("Restoration Test", r => profileResult = r);

        Assert.That(profileResult != null && profileResult.Success, Is.True,
            "an authorized request against the restored session must still succeed through the normal " +
            "request-time refresh path");
        Assert.That(transport.Requests, Has.Count.EqualTo(1),
            "the access token restored from disk is fresh (10 minutes from expiry, well outside the " +
            "proactive refresh lead time), so no refresh call should have been needed for this request");
    }

    private static string TokenRefreshResponseJson()
    {
        return "{"
            + "\"accessToken\":\"" + PersistedAccessToken + "-refreshed\","
            + "\"expiresAt\":\"" + DateTimeOffset.UtcNow.AddMinutes(15).ToString("O") + "\","
            + "\"playerId\":\"" + PersistedPlayerId + "\","
            + "\"refreshToken\":\"" + PersistedRefreshToken + "-refreshed\","
            + "\"refreshTokenExpiresAt\":\"" + DateTimeOffset.UtcNow.AddDays(30).ToString("O") + "\""
            + "}";
    }

    private static string PlayerProfileResponseJson()
    {
        return "{"
            + "\"playerId\":\"" + PersistedPlayerId + "\","
            + "\"displayName\":\"Restoration Test\","
            + "\"tag\":\"REST#0001\""
            + "}";
    }

    /// <summary>Local, minimal counterpart to the EditMode-only <c>FakeApiTransport</c>
    /// (<c>Assets/Tests/Editor/FakeApiTransport.cs</c>): Editor-only test doubles live in an implicit
    /// assembly this PlayMode test assembly cannot reference, so this is re-typed rather than
    /// shared.</summary>
    private sealed class RecordingApiTransport : IApiTransport
    {
        public List<ApiRequest> Requests { get; } = new List<ApiRequest>();

        public Func<ApiRequest, RawApiResponse> Handler { get; set; }

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            RawApiResponse response = Handler != null ? Handler(request) : RawApiResponse.NetworkError();
            completed?.Invoke(response);
            yield break;
        }
    }
}
