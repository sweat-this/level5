#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: closes the same coverage gap
/// <see cref="Level5MoneyBallStateCompositionPlayModeTests"/> and
/// <see cref="Level5ShotMarkerSessionCompositionPlayModeTests"/> close for their own migrations, for
/// <c>BasketBallShotMade.BindMatchContext</c>. Every EditMode test for it
/// (<see cref="Level5BasketBallShotMadeTests"/>) calls it directly against a hand-built component -
/// proving the binding logic is correct in isolation, but never proving the real
/// <c>GameLevelManager.Awake()</c> -&gt; <c>FindAnyObjectByType&lt;BasketBallShotMade&gt;()</c> lookup
/// actually reaches a live scene's real, scene-authored hoop before any made shot can be scored. That
/// depends on the scene actually containing a <c>BasketBallShotMade</c> and on Unity's own object
/// discovery finding it - neither of which an EditMode test (which builds components directly, never
/// lets a real scene load happen) can observe.
///
/// This drives the real production flow - the same technique the two tests above already use -
/// through the start menu into a real gameplay scene, so the hoop under test is the actual
/// scene-authored <c>basketball_goal</c> object and bound by the actual <c>GameLevelManager.Awake()</c>,
/// not a test double standing in for either.
/// </summary>
public class Level5BasketBallShotMadeCompositionPlayModeTests
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
        Scene blank = SceneManager.CreateScene("shot-made-composition-cleanup");
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
    public IEnumerator TheRealSceneHoopIsBoundToTheRealGameLevelManagerRulesAndModeBeforeGameplay()
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

        Assert.That(GameLevelManager.instance, Is.Not.Null, "the real gameplay scene must have produced a live GameLevelManager instance");

        BasketBallShotMade shotMade = Object.FindAnyObjectByType<BasketBallShotMade>(FindObjectsInactive.Include);
        Assert.That(shotMade, Is.Not.Null, "no BasketBallShotMade was found in the real scene - nothing for this test to verify");

        Assert.That(
            FieldObject(shotMade, "hasBoundMatchContext"),
            Is.EqualTo(true),
            $"BasketBallShotMade '{shotMade.gameObject.name}' reached play with no bound match context - GameLevelManager.Awake()'s composition step did not reach it");

        object boundRules = FieldObject(shotMade, "matchRules");
        Assert.That(boundRules, Is.Not.Null);
        Assert.That(
            ReferenceEquals(boundRules, GameLevelManager.instance.Rules),
            Is.True,
            $"BasketBallShotMade '{shotMade.gameObject.name}' is bound to a ResolvedMatchRules reference other than the scene's own GameLevelManager.Rules");

        Assert.That(
            FieldObject(shotMade, "gameModeId"),
            Is.EqualTo(MatchRuntime.ModeId),
            $"BasketBallShotMade '{shotMade.gameObject.name}' is bound to a mode identity other than this match's MatchRuntime.ModeId");
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
