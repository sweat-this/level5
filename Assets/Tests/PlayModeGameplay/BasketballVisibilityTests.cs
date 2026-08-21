#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// "When the player has the ball, the one above his head needs to be hidden."
///
/// BasketBall.Update moves the ball to the owner's basketBall_position (local y 1, above the head)
/// and hides it with spriteRenderer.color alpha 0 when hasBasketball is true, so the position is by
/// design and the hide is what fails. Static reading could not tell which ball is actually visible:
/// SpawnCoordinator gives every participant a ball and spawns them all at the same
/// ball_spawn_location, so a second player's ball is also a candidate.
///
/// This reports, per ball in the scene: its owner, whether that owner has it, where it is relative
/// to the owner's hold point, and whether it is actually being drawn.
///
/// Kept as regression coverage: alpha alone silently stopped hiding it once the sprite moved to
/// a particle shader, and nothing would have caught that.
/// </summary>
public class BasketballVisibilityTests
{
    private const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    [SetUp]
    public void IgnoreSceneLogNoise()
    {
        // These drive real scenes end to end, and those scenes log errors of their own that have
        // nothing to do with what is under test. Without this the runner turns any stray Debug.LogError
        // into a failure for whichever test happened to be running.
        LogAssert.ignoreFailingMessages = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        Scene blank = SceneManager.CreateScene("basketball-diag-cleanup");
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
    public IEnumerator TheHeldBallIsNotDrawnAbovethePlayersHead()
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

        // The player never picks the ball up on his own here, so force the held state - that is the
        // state the bug is reported in ("when player has the ball, the one above his head needs to
        // be hidden").
        PlayerController human = Object.FindAnyObjectByType<PlayerController>();
        if (human != null)
        {
            FieldInfo hasBall = human.GetType().GetField("hasBasketball", Flags);
            if (hasBall != null)
            {
                hasBall.SetValue(human, true);
                Debug.Log("DIAG forced hasBasketball=true on " + human.gameObject.name);
            }
            else
            {
                Debug.Log("DIAG could not find hasBasketball field");
            }
        }

        // let BasketBall.Update run against the held state
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }


        BasketBall[] balls = Object.FindObjectsByType<BasketBall>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log("DIAG human balls (BasketBall): " + balls.Length);

        for (int i = 0; i < balls.Length; i++)
        {
            Report("BasketBall[" + i + "]", balls[i], balls[i].gameObject);
        }

        BasketBallAuto[] autoBalls = Object.FindObjectsByType<BasketBallAuto>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log("DIAG cpu balls (BasketBallAuto): " + autoBalls.Length);
        for (int i = 0; i < autoBalls.Length; i++)
        {
            Report("BasketBallAuto[" + i + "]", autoBalls[i], autoBalls[i].gameObject);
        }

        // the reported bug: the ball sits on the hold point above the head and is still drawn
        BasketBall held = null;
        for (int i = 0; i < balls.Length; i++)
        {
            object controller = FieldObject(balls[i], "actor");
            FieldInfo hb = controller?.GetType().GetField("hasBasketball", Flags);
            if (hb != null && hb.GetValue(controller) is bool b && b)
            {
                held = balls[i];
                break;
            }
        }

        Assert.That(held, Is.Not.Null, "no ball reported its owner as holding it");

        SpriteRenderer renderer = FieldObject(held, "spriteRenderer") as SpriteRenderer;
        Assert.That(renderer, Is.Not.Null, "the held ball has no sprite renderer");
        Assert.That(
            renderer.enabled,
            Is.False,
            "the ball is still being drawn while the player holds it - tinting alpha is not enough, "
                + "the sprite uses a particle shader that ignores the renderer colour");
    }

    private static void Report(string label, object ballComponent, GameObject ballObject)
    {
        object controller = FieldObject(ballComponent, "actor");
        object owner = FieldObject(ballComponent, "player");
        GameObject ownerObject = owner as GameObject;
        object hold = FieldObject(ballComponent, "basketBallPosition");
        GameObject holdObject = hold as GameObject;

        bool hasBall = false;
        if (controller != null)
        {
            FieldInfo hb = controller.GetType().GetField("hasBasketball", Flags);
            object v = hb?.GetValue(controller);
            hasBall = v is bool b && b;
        }

        SpriteRenderer sr = FieldObject(ballComponent, "spriteRenderer") as SpriteRenderer;

        Debug.Log(
            "DIAG " + label
            + " owner=" + (ownerObject == null ? "NULL" : ownerObject.name)
            + " hasBasketball=" + hasBall
            + " ballPos=" + ballObject.transform.position
            + " holdPos=" + (holdObject == null ? "NULL" : holdObject.transform.position.ToString())
            + " atHoldPoint=" + (holdObject != null
                && Vector3.Distance(ballObject.transform.position, holdObject.transform.position) < 0.2f)
            + " | renderer=" + (sr == null ? "NULL" : sr.gameObject.name)
            + " enabled=" + (sr == null ? "-" : sr.enabled.ToString())
            + " alpha=" + (sr == null ? "-" : sr.color.a.ToString("F2"))
            + " isVisible=" + (sr == null ? "-" : sr.isVisible.ToString()));
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
