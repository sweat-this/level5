#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only black-box gameplay bootstrap shared by PlayMode fixtures that need a live, resumed
/// gameplay level and do not care how the player got there.
///
/// <c>StartManager</c> and <c>Pause</c> are incidental scene-bootstrap plumbing for those fixtures,
/// not the thing under test - but both still live in <c>Assembly-CSharp</c>, so a fixture on the
/// <c>Level5.PlayModeTests</c> asmdef cannot name either type at compile time. This drives the same
/// public "press start" UI path a player uses instead of reflecting into <c>StartManager</c>'s private
/// readiness check, and suppresses <c>Pause</c> by matching a live component's runtime type name
/// rather than referencing the type.
/// </summary>
public static class GameplayScenePlayModeHarness
{
    private const string StartButtonName = "press_start";

    /// <summary>
    /// Budget for reaching gameplay at all: waiting for the start menu's UI to exist and submitting
    /// "press_start" (possibly more than once, if production bounces back to the start scene because
    /// game setup was not ready yet). Generous because it stands in for what used to be two
    /// independent 30s budgets (menu readiness, then scene transition).
    /// </summary>
    private const float LaunchTimeoutSeconds = 90f;

    /// <summary>
    /// Minimum budget guaranteed for waiting for gameplay to produce a player once the active scene
    /// actually leaves the start scene, applied on top of whatever remains of
    /// <see cref="LaunchTimeoutSeconds"/> so a launch phase that used most of its budget on retries
    /// cannot starve this final step, which used to be unbounded.
    /// </summary>
    private const float MinimumGameplayReadySeconds = 30f;

    /// <summary>
    /// Frames to wait after a submit before resubmitting, so a launch that is already progressing does
    /// not get a second "press_start" queued behind it every single frame.
    /// </summary>
    private const int SubmitRetryFrames = 5;

    /// <summary>
    /// Frames to wait after the active scene first leaves the start scene, before touching anything,
    /// so newly loaded incidental systems (Pause included) have a chance to run their own Awake/Start
    /// before this looks for them.
    /// </summary>
    private const int SettleFramesAfterLeavingStartScene = 2;

    /// <summary>
    /// Loads the start scene, launches through the real "press_start" UI (retrying if production
    /// bounces back to the start scene because game setup was not ready), waits for gameplay to
    /// produce a live <see cref="PlayerController"/>, suppresses the incidental start-on-pause
    /// behaviour, and restores <see cref="Time.timeScale"/>. Returns only once gameplay is usable.
    /// </summary>
    public static IEnumerator EnterPlayableGameplayScene(Action<PlayerController> onReady)
    {
        yield return EnterGameplayScene(suppressInitialPause: true, onReady);
    }

    /// <summary>
    /// Same real launch path as <see cref="EnterPlayableGameplayScene"/>, but for fixtures that need to
    /// observe production's actual initial pause state instead of having it suppressed: it never
    /// disables the live <c>Pause</c> component and never touches <see cref="Time.timeScale"/>, so
    /// gameplay is handed back exactly as production left it - paused or not.
    /// </summary>
    public static IEnumerator EnterGameplayScenePreservingInitialPause(Action<PlayerController> onReady)
    {
        yield return EnterGameplayScene(suppressInitialPause: false, onReady);
    }

    /// <summary>
    /// Loads a gameplay scene for a match the fixture has already made current with
    /// <c>ActiveMatch.Begin</c> - the path for a launch shape the start menu cannot produce yet, such
    /// as two local humans - then applies the same settle, pause suppression and player wait as
    /// <see cref="EnterPlayableGameplayScene"/>. Nothing here builds or validates the match.
    /// </summary>
    public static IEnumerator LoadConfiguredGameplayScene(string sceneName, Action<PlayerController> onReady)
    {
        SceneManager.LoadScene(sceneName);
        for (int frame = 0; frame < SettleFramesAfterLeavingStartScene; frame++)
        {
            yield return null;
        }

        SuppressPauseComponent();
        Time.timeScale = 1f;
        yield return null;

        float deadline = Time.realtimeSinceStartup + MinimumGameplayReadySeconds;
        PlayerController player = null;
        while (player == null && Time.realtimeSinceStartup < deadline)
        {
            player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                yield return null;
            }
        }

        Assert.That(player, Is.Not.Null, "gameplay scene '" + sceneName + "' never produced a PlayerController");
        onReady(player);
    }

    /// <summary>
    /// Shared launch/retry state machine behind both public entry points. <paramref name="suppressInitialPause"/>
    /// is the only behavioural difference between them: whether the incidental start-on-pause screen is
    /// dismissed and <see cref="Time.timeScale"/> restored once gameplay is reached, or left exactly as
    /// production produced it.
    /// </summary>
    private static IEnumerator EnterGameplayScene(bool suppressInitialPause, Action<PlayerController> onReady)
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return null;
        yield return null;

        float deadline = Time.realtimeSinceStartup + LaunchTimeoutSeconds;

        // True once this launch attempt has settled (settle-waited, suppressed Pause, restored
        // timeScale) after leaving the start scene. Reset whenever the active scene is the start scene
        // again, so a bounce back through the loading scene re-settles on the next real departure
        // instead of skipping straight to the player search with a stale Pause/timeScale state.
        bool settled = false;
        PlayerController player = null;

        // True once at least one "press_start" submission was actually issued, so the failure
        // message below can tell "submitted but the scene never moved" apart from "never even found
        // an EventSystem/press_start to submit to".
        bool everSubmitted = false;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start)
            {
                settled = false;

                if (EventSystem.current != null)
                {
                    GameObject pressStart = GameObject.Find(StartButtonName);
                    if (pressStart != null)
                    {
                        ExecuteEvents.Execute(
                            pressStart,
                            new BaseEventData(EventSystem.current),
                            ExecuteEvents.submitHandler);
                        everSubmitted = true;
                    }
                }

                // Bounded submission cadence: give production a few frames to react (either by
                // launching gameplay or by bouncing back through the loading scene because setup was
                // not ready) before considering another "press_start" submission.
                for (int frame = 0;
                    frame < SubmitRetryFrames
                        && Time.realtimeSinceStartup < deadline
                        && SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start;
                    frame++)
                {
                    yield return null;
                }

                continue;
            }

            // The active scene left the start scene - issue no further submissions. If production
            // bounced through the loading scene because setup was not ready, the loop above resumes
            // submitting once the active scene is the start scene again.
            if (!settled)
            {
                for (int frame = 0;
                    frame < SettleFramesAfterLeavingStartScene
                        && Time.realtimeSinceStartup < deadline
                        && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_start;
                    frame++)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start)
                {
                    // bounced back to the start scene during the settle wait - resume submitting
                    continue;
                }

                if (suppressInitialPause)
                {
                    SuppressPauseComponent();
                    Time.timeScale = 1f;
                }

                yield return null;
                settled = true;

                // Guarantee the player-spawn wait gets its own minimum budget, independent of how
                // much of the launch budget earlier retries/bounces already consumed. Only ever
                // extends the deadline, never shortens it.
                float minimumGameplayReadyDeadline = Time.realtimeSinceStartup + MinimumGameplayReadySeconds;
                if (minimumGameplayReadyDeadline > deadline)
                {
                    deadline = minimumGameplayReadyDeadline;
                }
            }

            player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                break;
            }

            yield return null;
        }

        // Read fresh rather than latched, so a run that bounces back to the start scene and is still
        // there at the deadline reports the accurate "never left" failure instead of a stale "left"
        // from an earlier, transient departure.
        bool leftStartScene = SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_start;
        string launchFailureMessage = everSubmitted
            ? "the start menu never launched gameplay - 'press_start' submissions never moved the "
                + "active scene away from " + Constants.SCENE_NAME_level_00_start
            : "the start menu never launched gameplay - never found an EventSystem and a '"
                + StartButtonName + "' object to submit to";
        Assert.That(leftStartScene, Is.True, launchFailureMessage);
        Assert.That(
            player,
            Is.Not.Null,
            "gameplay never produced a PlayerController within the timeout");

        onReady(player);
    }

    /// <summary>
    /// Disables a live component whose runtime type is named exactly "Pause", if one exists, so the
    /// start-on-pause screen does not leave <see cref="Time.timeScale"/> at zero for fixtures that are
    /// not exercising pause behaviour. Looked up by name rather than by type so this stays compilable
    /// without a compile-time reference to <c>Assembly-CSharp</c>'s <c>Pause</c> type. Tolerant of no
    /// such component existing; touches nothing else about it.
    /// </summary>
    private static void SuppressPauseComponent()
    {
        MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetType().Name == "Pause")
            {
                all[i].enabled = false;
            }
        }
    }
}
#endif
