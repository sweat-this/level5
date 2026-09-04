#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 1c: closes a coverage gap a code review flagged on the money-ball-state migration.
/// Every EditMode test for <c>GameRules.BindMoneyBallStateToBasketballs</c>
/// (<see cref="Level5BasketballMoneyBallStateTests"/>) invokes it directly via reflection against a
/// hand-built <see cref="PlayerRegistry"/> - proving the binding logic is correct in isolation, but
/// never proving the real <c>GameRules.Awake()</c> -&gt; <c>GameLevelManager.instance.Registry</c> chain
/// actually wires a live scene's real spawned balls. That chain depends on Unity's script execution
/// order (<c>GameLevelManager</c> at -8000, <c>GameRules</c> at default 0) actually holding in a real
/// scene load, which no EditMode test can observe - EditMode tests build components directly and
/// never let Unity itself decide Awake() order across objects.
///
/// This drives the real production flow - the same technique <see cref="BasketballVisibilityTests"/>
/// already uses - through the start menu into a real gameplay scene, so the balls under test are
/// spawned by the actual <c>SpawnCoordinator</c> and bound by the actual <c>GameRules.Awake()</c>,
/// not a test double standing in for either.
/// </summary>
public class Level5MoneyBallStateCompositionPlayModeTests
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
        Scene blank = SceneManager.CreateScene("moneyball-composition-cleanup");
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
    public IEnumerator EveryRealSpawnedBasketballIsBoundToTheRealGameRulesInstance()
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

        Debug.Log("DIAG scene: " + SceneManager.GetActiveScene().name);

        Assert.That(GameRules.instance, Is.Not.Null, "the real gameplay scene must have produced a live GameRules instance");
        Assert.That(GameLevelManager.instance, Is.Not.Null, "the real gameplay scene must have produced a live GameLevelManager instance");

        BasketBall[] balls = Object.FindObjectsByType<BasketBall>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        BasketBallAuto[] autoBalls = Object.FindObjectsByType<BasketBallAuto>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Debug.Log("DIAG human balls (BasketBall): " + balls.Length + ", cpu balls (BasketBallAuto): " + autoBalls.Length);

        Assert.That(balls.Length, Is.GreaterThan(0), "no BasketBall was spawned - nothing for this test to verify");

        foreach (BasketBall ball in balls)
        {
            object bound = FieldObject(ball, "moneyBallState");
            Assert.That(bound, Is.Not.Null, $"BasketBall '{ball.gameObject.name}' reached play with no bound IMoneyBallState - GameRules.Awake()'s composition step did not reach it");
            Assert.That(
                ReferenceEquals(bound, GameRules.instance),
                Is.True,
                $"BasketBall '{ball.gameObject.name}' is bound to a money-ball provider that is not the real live GameRules instance");

            // AUD-010 Phase 2b0: proves the real SpawnCoordinator.GiveBall -> BasketBall.BindMatchRules
            // wiring holds in an actual scene load - the same gap this file's own header comment
            // already explains no EditMode test can observe. A missing bind here would have
            // deactivated the whole GameObject in Start() (see BasketBall.Start()'s rules guard).
            Assert.That(
                ball.gameObject.activeSelf,
                Is.True,
                $"BasketBall '{ball.gameObject.name}' deactivated itself - it reached Start() with no bound match rules");
            Assert.That(
                FieldObject(ball, "matchRules"),
                Is.Not.Null,
                $"BasketBall '{ball.gameObject.name}' reached play with no bound ResolvedMatchRules - SpawnCoordinator.GiveBall's composition step did not reach it");
        }

        foreach (BasketBallAuto ball in autoBalls)
        {
            object bound = FieldObject(ball, "moneyBallState");
            Assert.That(bound, Is.Not.Null, $"BasketBallAuto '{ball.gameObject.name}' reached play with no bound IMoneyBallState - GameRules.Awake()'s composition step did not reach it");
            Assert.That(
                ReferenceEquals(bound, GameRules.instance),
                Is.True,
                $"BasketBallAuto '{ball.gameObject.name}' is bound to a money-ball provider that is not the real live GameRules instance");

            // AUD-010 Phase 2b0 code review: proves the real SpawnCoordinator.GiveBall -> BasketBallAuto.BindMatchRules
            // wiring holds in an actual scene load, the same gap this file's own header comment
            // already explains no EditMode test can observe. A missing bind here would have
            // deactivated the whole GameObject in Start() (see BasketBallAuto.Start()'s rules guard).
            Assert.That(
                ball.gameObject.activeSelf,
                Is.True,
                $"BasketBallAuto '{ball.gameObject.name}' deactivated itself - it reached Start() with no bound match rules");
            Assert.That(
                FieldObject(ball, "matchRules"),
                Is.Not.Null,
                $"BasketBallAuto '{ball.gameObject.name}' reached play with no bound ResolvedMatchRules - SpawnCoordinator.GiveBall's composition step did not reach it");
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
