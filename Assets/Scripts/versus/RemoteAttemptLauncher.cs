using System;
using System.Collections;
using Assets.Scripts.Utility;
using Level5.Core.Match;
using Level5.Core.Progression;
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
    ///
    /// Takes the caller's current <see cref="UnlockSnapshot"/> (issue #198) and checks the local
    /// level against it twice: once here, before <c>StartAttempt</c>, so an unknown/non-selectable/
    /// locked level never reaches the server; and again inside <see cref="RemoteAttemptDescriptorMapper"/>,
    /// through the ordinary <c>MatchConfigurationBuilder.Build</c> gate, so the final
    /// <c>MatchConfiguration</c> is never produced any other way than every other launch path uses.
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
            UnlockSnapshot unlock,
            Action<RemoteAttemptLaunch> completed)
        {
            BackendV2CoroutineHost.Instance.StartCoroutine(
                Run(seriesId, gameNumber, levelId, character, modifiers, unlock, completed));
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
            UnlockSnapshot unlock,
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

            // Issue #198: the same local level-eligibility recheck an ordinary launch performs
            // (MatchCatalogs.Builder.Build(request, unlockSnapshot)) is enforced here too, before
            // anything is spent on the attempt it would be used for - an unknown, non-selectable or
            // locked level must never reach StartAttempt. unlock is required (not optional the way
            // MatchConfigurationBuilder.Build's own parameter is for unmigrated callers): the remote
            // path must never silently fall back to permissive null-unlock behavior.
            if (unlock == null)
            {
                completed?.Invoke(RemoteAttemptLaunch.Failure(
                    "no local unlock snapshot was provided - refusing to start a remote attempt without a level eligibility check"));
                yield break;
            }

            LevelDefinition level = MatchCatalogs.Levels.Find(levelId);
            ValidationResult levelValidation = LevelEligibility.ValidateForLaunch(level, levelId, unlock);
            if (!levelValidation.IsValid)
            {
                completed?.Invoke(RemoteAttemptLaunch.Failure(levelValidation.ToString()));
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
                response.Value, levelId, participantId, character, unlock, modifiers);

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
