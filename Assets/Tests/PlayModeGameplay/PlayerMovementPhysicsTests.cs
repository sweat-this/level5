#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Regression coverage for the two movement bugs that shared a root cause: the player is a
/// non-kinematic Rigidbody with zero linear damping, and PlayerController drove it with
/// <c>MovePosition</c> while <c>PlayerJump</c> wrote <c>linearVelocity</c>.
///
/// MovePosition on a dynamic body derives an implicit velocity of delta/fixedDeltaTime, so walking
/// into another character produced a depenetration impulse nothing damped (the player shot away),
/// and holding a direction while jumping had position-driven and velocity-driven motion compound
/// (the player flew). Horizontal movement is velocity-driven now, which composes with gravity, with
/// the jump, and with contacts.
/// </summary>
public class PlayerMovementPhysicsTests
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

        Scene blank = SceneManager.CreateScene("player-movement-test-cleanup");
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

    /// <summary>
    /// A contact impulse must not survive into the next physics steps. Before the fix the player
    /// kept - and compounded - whatever velocity a collision imparted, because nothing overwrote it
    /// and linear damping is zero.
    /// </summary>
    [UnityTest]
    public IEnumerator AContactImpulseDoesNotSendThePlayerFlying()
    {
        Rigidbody player = null;
        yield return EnterGameplayLevel(result => player = result);
        Assert.That(player, Is.Not.Null, "No player rigidbody in the gameplay level.");

        // roughly what depenetration between two mass-100 bodies used to impart
        player.linearVelocity = new Vector3(120f, 0f, 90f);

        // fail fast rather than hang: WaitForFixedUpdate never returns while timeScale is 0
        Assert.That(Time.timeScale, Is.GreaterThan(0f), "physics is frozen, the level never resumed");

        float settle = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < settle)
        {
            yield return new WaitForFixedUpdate();
        }

        Vector3 v = player.linearVelocity;
        float horizontal = new Vector2(v.x, v.z).magnitude;
        Debug.Log("DIAG horizontal speed after impulse: " + horizontal);

        Assert.That(
            horizontal,
            Is.LessThan(30f),
            "the player kept a contact impulse instead of movement reasserting control - "
                + "horizontal speed was " + horizontal);
    }

    /// <summary>
    /// Jumping while holding a direction must not multiply horizontal travel. With no input the
    /// horizontal component should be whatever movement asks for - not a compounding sum of a
    /// position-driven step and a velocity-driven jump.
    /// </summary>
    [UnityTest]
    public IEnumerator JumpingDoesNotCompoundHorizontalVelocity()
    {
        Rigidbody player = null;
        yield return EnterGameplayLevel(result => player = result);
        Assert.That(player, Is.Not.Null, "No player rigidbody in the gameplay level.");

        PlayerController controller = player.GetComponent<PlayerController>();
        Assert.That(controller, Is.Not.Null, "No PlayerController on the player.");

        float jumpForce = Field<CharacterProfile>(controller, "characterProfile") != null
            ? Field<CharacterProfile>(controller, "characterProfile").JumpForce
            : 0f;

        Vector3 start = player.position;
        controller.PlayerJump();

        // fail fast rather than hang: WaitForFixedUpdate never returns while timeScale is 0
        Assert.That(Time.timeScale, Is.GreaterThan(0f), "physics is frozen, the level never resumed");

        float settle = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < settle)
        {
            yield return new WaitForFixedUpdate();
        }

        Vector3 travelled = player.position - start;
        float horizontalTravel = new Vector2(travelled.x, travelled.z).magnitude;
        Debug.Log("DIAG jumpForce=" + jumpForce + " horizontal travel during jump: " + horizontalTravel);

        Assert.That(
            horizontalTravel,
            Is.LessThan(20f),
            "a jump with no directional input moved the player " + horizontalTravel
                + " horizontally, so something is still driving horizontal motion during the jump");
    }

    /// <summary>
    /// Locomotion must not erase a velocity another system imposed. PlayerDunk.Launch sets a
    /// ballistic velocity and then clears Locked immediately, so FixedUpdate resumes mid-flight; an
    /// unconditional horizontal write zeroed x/z and the player never reached the rim.
    /// </summary>
    [UnityTest]
    public IEnumerator AnImposedArcSurvivesWhileAirborneWithNoInput()
    {
        Rigidbody player = null;
        yield return EnterGameplayLevel(result => player = result);
        Assert.That(player, Is.Not.Null, "No player rigidbody in the gameplay level.");

        Assert.That(Time.timeScale, Is.GreaterThan(0f), "physics is frozen, the level never resumed");

        // lift clear of the ground so Grounded is false, then impose an arc the way a dunk does
        player.position += Vector3.up * 3f;
        yield return new WaitForFixedUpdate();
        player.linearVelocity = new Vector3(4f, 6f, 3f);

        // one physics step is all it took for the old code to flatten it
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Vector3 v = player.linearVelocity;
        float horizontal = new Vector2(v.x, v.z).magnitude;
        Debug.Log("DIAG horizontal retained mid-air: " + horizontal);

        Assert.That(
            horizontal,
            Is.GreaterThan(1f),
            "an imposed arc was flattened by locomotion - dunks and knockbacks would not travel");
    }

    /// <summary>Start menu -> gameplay level, resumed, physics running.</summary>
    private IEnumerator EnterGameplayLevel(System.Action<Rigidbody> onReady)
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

        GameObject pressStart = GameObject.Find("press_start");
        ExecuteEvents.Execute(pressStart, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start)
        {
            yield return null;
        }

        yield return null;
        yield return null;

        // Gameplay levels open on the start-on-pause screen with timeScale 0. Pause.Update also
        // re-pauses on the next frame if `paused` is still set, so simply writing timeScale is not
        // enough - with timeScale 0 there are no fixed updates and every WaitForFixedUpdate below
        // would wait forever. Take Pause out of the loop entirely for the duration of the test.
        Pause pause = Object.FindAnyObjectByType<Pause>(FindObjectsInactive.Include);
        if (pause != null)
        {
            pause.enabled = false;
        }

        Time.timeScale = 1f;
        yield return null;

        PlayerController controller = Object.FindAnyObjectByType<PlayerController>();
        onReady(controller == null ? null : controller.GetComponent<Rigidbody>());
    }

    private static T Field<T>(object target, string name) where T : class
    {
        FieldInfo f = target.GetType().GetField(name, Flags);
        return f == null ? null : f.GetValue(target) as T;
    }

    private static T Invoke<T>(object target, string name)
    {
        MethodInfo mi = target.GetType().GetMethod(name, Flags);
        object v = mi == null ? null : mi.Invoke(target, null);
        return v is T typed ? typed : default;
    }
}
#endif
