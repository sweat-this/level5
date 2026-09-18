using System;
using System.Collections;
using Assets.Scripts.Utility;
using Level5.Core.Match;
using Level5.Core.Versus;

namespace Level5.BackendV2
{
    /// <summary>
    /// The remote correspondence counterpart to <c>VersusLauncher.Launch</c>: starts the attempt on
    /// the server, maps its descriptor into an ordinary local match through
    /// <see cref="RemoteAttemptDescriptorMapper"/>, and launches it through the exact same
    /// <c>ActiveMatch</c> / <c>LegacyGameOptionsBridge</c> / <c>SceneTransition</c> sequence every
    /// other launch path uses. A gameplay scene launched this way is never told a remote series
    /// exists.
    ///
    /// Lives next to <c>VersusLauncher</c> in the default assembly rather than inside
    /// <c>Level5.BackendV2.asmdef</c>: <c>LegacyGameOptionsBridge</c> is unmigrated legacy code with
    /// no assembly definition of its own, and a custom assembly definition cannot reference the
    /// implicit default assembly. The <c>Level5.BackendV2</c> namespace is kept for discoverability
    /// alongside the rest of the correspondence types even though this file compiles elsewhere.
    ///
    /// Runs on <see cref="BackendV2CoroutineHost"/> rather than the calling UI panel's own
    /// MonoBehaviour, since a successful launch immediately unloads that panel's scene.
    /// </summary>
    public static class RemoteAttemptLauncher
    {
        private static Action<string> sceneLoaderOverride;

        public static void Launch(
            Guid seriesId,
            int gameNumber,
            int levelId,
            CharacterSelection character,
            MatchModifiers modifiers,
            Action<RemoteAttemptLaunch> completed)
        {
            BackendV2CoroutineHost.Instance.StartCoroutine(
                Run(seriesId, gameNumber, levelId, character, modifiers, completed));
        }

        /// <summary>Exposed (rather than folded into <see cref="Launch"/>) so EditMode tests can
        /// drive the full sequence - including the scene-load call - synchronously, the same
        /// reasoning <c>RemoteAttemptResultSubmitter.TryClaim</c>/<c>Release</c> are exposed for.
        /// Production code should call <see cref="Launch"/>.</summary>
        public static IEnumerator Run(
            Guid seriesId,
            int gameNumber,
            int levelId,
            CharacterSelection character,
            MatchModifiers modifiers,
            Action<RemoteAttemptLaunch> completed)
        {
            // ActiveRemoteAttempt and PendingRemoteAttemptResult are both single global slots (the
            // same shape as ActiveMatch/ActiveVersusAttempt - "the one match/attempt currently being
            // played"). Beginning a new attempt here would silently overwrite whichever one of those
            // an earlier, still-unretried failed submission left behind, discarding it with no error
            // and no way to resend it - exactly what PendingRemoteAttemptResult exists to prevent.
            // The player must resolve (resend, successfully or definitively) that pending result
            // before starting another remote attempt.
            if (PendingRemoteAttemptResult.HasPending)
            {
                completed?.Invoke(RemoteAttemptLaunch.Failure(
                    "a previous remote attempt's result is still pending - resend it before starting another turn"));
                yield break;
            }

            ApiResponse<AttemptDescriptorDto> response = null;
            yield return BackendV2Runtime.Correspondence.StartAttempt(
                seriesId, gameNumber, result => response = result);

            if (response == null || !response.Success)
            {
                completed?.Invoke(RemoteAttemptLaunch.Failure(DescribeStartFailure(response)));
                yield break;
            }

            ParticipantId participantId = BackendV2ParticipantIdentity.Current();
            RemoteAttemptMapResult mapResult = RemoteAttemptDescriptorMapper.Map(
                response.Value, levelId, participantId, character, modifiers);

            if (!mapResult.Succeeded)
            {
                completed?.Invoke(RemoteAttemptLaunch.Failure(mapResult.Error));
                yield break;
            }

            ActiveMatch.Begin(mapResult.Configuration);
            ActiveRemoteAttempt.Begin(mapResult.Context, mapResult.Configuration);
            LegacyGameOptionsBridge.Apply(mapResult.Configuration);
            (sceneLoaderOverride ?? SceneTransition.LoadScene)(mapResult.Configuration.SceneName);

            completed?.Invoke(RemoteAttemptLaunch.Success(mapResult.Configuration));
        }

        /// <summary>Test-only seam so the success path can be exercised in EditMode without a real
        /// <c>SceneManager.LoadScene</c> call, matching this codebase's <c>Override</c>/<c>Reset</c>
        /// convention (<see cref="BackendV2Runtime"/>, <see cref="BackendV2ApiConfigProvider"/>).</summary>
        public static void OverrideSceneLoader(Action<string> loader)
        {
            sceneLoaderOverride = loader;
        }

        public static void ResetSceneLoader()
        {
            sceneLoaderOverride = null;
        }

        private static string DescribeStartFailure(ApiResponse<AttemptDescriptorDto> response)
        {
            if (response == null)
            {
                return "starting the attempt failed: no response";
            }

            string code = response.Problem?.Code;
            return code != null
                ? $"starting the attempt failed: {response.ErrorKind} ({code})"
                : $"starting the attempt failed: {response.ErrorKind}";
        }
    }
}
