#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 1c: closes the same coverage gap
/// <see cref="Level5MoneyBallStateCompositionPlayModeTests"/> closes for the money-ball-state
/// migration, for the shot-marker-session migration. Every EditMode test for
/// <c>GameRules.BindShotMarkerSessionToMarkers</c> (<see cref="Level5BasketballShotMarkerSessionTests"/>)
/// invokes it directly via reflection against hand-built markers - proving the binding logic is
/// correct in isolation, but never proving the real <c>GameRules.Awake()</c> -&gt; scene-authored
/// <c>"shot_marker"</c> tag scan actually wires a live scene's real markers before their own
/// <c>Start()</c> runs. That ordering depends on Unity's guarantee that every object's Awake() runs
/// before any object's Start() - Unity itself deciding Awake()/Start() order across objects is exactly
/// what an EditMode test cannot observe.
///
/// This drives the real production flow - the same technique
/// <see cref="Level5MoneyBallStateCompositionPlayModeTests"/> already uses - through the start menu
/// into a real gameplay scene, so the markers under test are the actual scene-authored objects and
/// bound by the actual <c>GameRules.Awake()</c>, not a test double standing in for either.
///
/// Markers a game mode does not need are deactivated later by <c>GameRules.Start()</c>'s
/// <c>SetPositionMarkers()</c> - after this binding pass already ran in <c>Awake()</c> - so inactive
/// markers are included in the scan rather than excluded: deactivation afterward does not unbind them.
/// </summary>
public class Level5ShotMarkerSessionCompositionPlayModeTests
{
    private const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    [SetUp]
    public void IgnoreSceneLogNoise()
    {
        // These drive real scenes end to end, and those scenes log errors of their own that have
        // nothing to do with what is under test - see BasketballVisibilityTests for the same guard.
        LogAssert.ignoreFailingMessages = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        Scene blank = SceneManager.CreateScene("shot-marker-composition-cleanup");
        SceneManager.SetActiveScene(blank);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != blank && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator EveryRealSceneMarkerIsBoundToTheRealGameRulesInstanceBeforeGameplay()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
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

        Assert.That(manager, Is.Not.Null, "start menu never became ready");

        ExecuteEvents.Execute(
            GameObject.Find("press_start"),
            new BaseEventData(EventSystem.current),
            ExecuteEvents.submitHandler);

        deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start)
        {
            yield return null;
        }

        Pause pause = Object.FindAnyObjectByType<Pause>(FindObjectsInactive.Include);
        if (pause != null)
        {
            pause.enabled = false;
        }

        Time.timeScale = 1f;
        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        Assert.That(GameRules.instance, Is.Not.Null, "the real gameplay scene must have produced a live GameRules instance");

        BasketBallShotMarker[] markers = Object.FindObjectsByType<BasketBallShotMarker>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(markers.Length, Is.GreaterThan(0), "no BasketBallShotMarker was found - nothing for this test to verify");

        foreach (BasketBallShotMarker marker in markers)
        {
            object bound = FieldObject(marker, "markerSession");
            Assert.That(bound, Is.Not.Null, $"BasketBallShotMarker '{marker.gameObject.name}' reached play with no bound IShotMarkerSession - GameRules.Awake()'s composition step did not reach it");
            Assert.That(
                ReferenceEquals(bound, GameRules.instance),
                Is.True,
                $"BasketBallShotMarker '{marker.gameObject.name}' is bound to a shot-marker session that is not the real live GameRules instance");
        }
    }

    private static object FieldObject(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, Flags);
        return f == null ? null : f.GetValue(target);
    }

    private static T Invoke<T>(object target, string name)
    {
        MethodInfo mi = target.GetType().GetMethod(name, Flags);
        object v = mi == null ? null : mi.Invoke(target, null);
        return v is T typed ? typed : default;
    }
}
#endif
