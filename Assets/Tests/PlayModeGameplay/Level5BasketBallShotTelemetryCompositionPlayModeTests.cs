#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: proves the real <c>SpawnCoordinator.GiveBall</c> -&gt;
/// <c>BasketBall.BindShotTelemetry</c> wiring holds in an actual scene load, the same gap
/// <see cref="Level5MoneyBallStateCompositionPlayModeTests"/>'s own header comment explains no
/// EditMode test can observe: every EditMode test for this binding
/// (<see cref="Level5BasketBallShotTelemetryTests"/>) either calls <c>BindShotTelemetry</c> directly
/// or drives <c>SpawnCoordinator.GiveBall</c> against a hand-built <c>PlayerRegistry</c>, never the
/// real start-menu -&gt; gameplay-scene load this file drives instead.
/// </summary>
public class Level5BasketBallShotTelemetryCompositionPlayModeTests
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
        Scene blank = SceneManager.CreateScene("shot-telemetry-composition-cleanup");
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
    public IEnumerator EveryRealSpawnedHumanBasketballIsBoundToAnaylticsManagerPlayerShoot()
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

        BasketBall[] balls = Object.FindObjectsByType<BasketBall>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        BasketBallAuto[] autoBalls = Object.FindObjectsByType<BasketBallAuto>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Debug.Log("DIAG human balls (BasketBall): " + balls.Length + ", cpu balls (BasketBallAuto): " + autoBalls.Length);

        Assert.That(balls.Length, Is.GreaterThan(0), "no BasketBall was spawned - nothing for this test to verify");

        foreach (BasketBall ball in balls)
        {
            object bound = FieldObject(ball, "shotTelemetryCallback");
            Assert.That(bound, Is.Not.Null,
                $"BasketBall '{ball.gameObject.name}' reached play with no bound shot-telemetry callback - SpawnCoordinator.GiveBall's composition step did not reach it");

            System.Delegate callback = (System.Delegate)bound;
            Assert.That(callback.Method.DeclaringType, Is.EqualTo(typeof(AnaylticsManager)),
                $"BasketBall '{ball.gameObject.name}' is bound to a shot-telemetry callback that is not AnaylticsManager.PlayerShoot");
            Assert.That(callback.Method.Name, Is.EqualTo(nameof(AnaylticsManager.PlayerShoot)));
        }

        // AUD-010 Phase 2b0: BasketBallAuto declares no BindShotTelemetry/telemetry field at all, so
        // CPU shots gaining no telemetry behavior is a static-type fact, not scene-dependent - proven
        // once by the EditMode test BasketBallAutoDeclaresNoShotTelemetryBindingMethod. This scene
        // load spawns no CPU ball to assert against either way (see the diagnostic log above), so a
        // reflection check here would only restate that same static fact without exercising it live.
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
