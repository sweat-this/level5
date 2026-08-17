#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Start menu -> gameplay level -> unpause, end to end.
///
/// The bug this covers: every gameplay level opens on the start-on-pause screen, and
/// <see cref="Pause"/> dismisses it with <c>Controls.Player.submit</c> read off the shared
/// PlayerControls instance - whose <c>Player</c> map nothing enabled. GameLevelManager enables only
/// <c>Other</c>, and real player input runs on the separate per-player instances from
/// <c>AcquireGameplayControls</c>. Outside sniper levels (the one <c>EnableGameplayMaps</c> caller)
/// both <c>Player.submit</c> and <c>Player.cancel</c> were permanently dead, so the level could
/// never be started and Escape could never toggle pause.
/// </summary>
public class GameplayLevelUnpauseTests
{
    private const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    /// <summary>
    /// This test loads real scenes, so it has to hand the runner a clean slate back. Without this it
    /// leaves level_01_scrapyard resident, and its <c>game_rules</c> MatchController holds the
    /// singleton static that <c>Level5GameplayPlayModeTests</c> asserts on next.
    /// </summary>
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;

        Scene blank = SceneManager.CreateScene("gameplay-unpause-test-cleanup");
        SceneManager.SetActiveScene(blank);

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != blank && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        // give the destroyed managers their OnDestroy frame so the statics they own are released
        yield return null;
    }

    [UnityTest]
    public IEnumerator GameplayLevelCanBeUnpaused()
    {
        SceneManager.LoadScene("level_00_start");
        yield return null;
        yield return null;

        StartManager manager = null;
        float deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline)
        {
            manager = Object.FindAnyObjectByType<StartManager>();
            if (manager != null && Invoke<bool>(manager, "HasLoadedGameSetup"))
            {
                break;
            }

            yield return null;
        }

        Assert.That(manager, Is.Not.Null, "No StartManager.");

        GameObject pressStart = GameObject.Find("press_start");
        ExecuteEvents.Execute(pressStart, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        // wait for the gameplay level
        deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name == "level_00_start")
        {
            yield return null;
        }

        Debug.Log("DIAG gameplay scene     : " + SceneManager.GetActiveScene().name);
        yield return null;
        yield return null;

        Pause pause = Object.FindAnyObjectByType<Pause>();
        Assert.That(pause, Is.Not.Null, "No Pause in the gameplay level.");

        bool startOnPause = Field<bool>(pause, "startOnPause");
        bool playerMapEnabled = PlayerControlsProvider.Controls.Player.enabled;

        Debug.Log("DIAG startOnPause       : " + startOnPause);
        Debug.Log("DIAG Time.timeScale     : " + Time.timeScale);
        Debug.Log("DIAG Player map enabled : " + playerMapEnabled);
        Debug.Log("DIAG   (submit action)  : " + PlayerControlsProvider.Controls.Player.submit.enabled);

        Assert.That(
            playerMapEnabled,
            Is.True,
            "Controls.Player is disabled, so Pause can never see submit/cancel and the level "
                + "can never be unpaused.");

        // with the map live, the dismiss path is reachable; drive it directly to prove the rest
        if (startOnPause)
        {
            pause.StartGame();
            yield return null;
            Debug.Log("DIAG after StartGame()  : timeScale=" + Time.timeScale
                + " startOnPause=" + Field<bool>(pause, "startOnPause"));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "StartGame did not resume the game.");
        }

        Time.timeScale = 1f;
    }

    private static object FieldObject(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, Flags);
        return f == null ? null : f.GetValue(target);
    }

    private static T Field<T>(object target, string name)
    {
        object v = FieldObject(target, name);
        return v is T typed ? typed : default;
    }

    private static T Invoke<T>(object target, string name)
    {
        MethodInfo mi = target.GetType().GetMethod(name, Flags);
        object v = mi == null ? null : mi.Invoke(target, null);
        return v is T typed ? typed : default;
    }
}
#endif
