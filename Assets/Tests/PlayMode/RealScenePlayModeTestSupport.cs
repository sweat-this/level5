#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Shared scaffolding for PlayMode fixtures that drive real scenes end to end: silencing scene log
/// noise unrelated to what is under test, unloading every scene after a test, reflecting into private
/// production fields, and resolving the live runtime <c>GameRules</c> singleton by type name for
/// fixtures that need it. Extracted because <c>BasketballVisibilityTests</c> and
/// <c>PlayerMovementPhysicsTests</c> each hand-rolled an identical copy of the first three, and
/// <c>Level5ShotMarkerSessionCompositionPlayModeTests</c>/<c>Level5MoneyBallStateCompositionPlayModeTests</c>
/// each hand-rolled an identical copy of the <c>GameRules</c> resolver.
/// </summary>
internal static class RealScenePlayModeTestSupport
{
    internal const BindingFlags PrivateInstanceFlags =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    /// <summary>
    /// These fixtures drive real scenes end to end, and those scenes log errors of their own that
    /// have nothing to do with what is under test. Without this the runner turns any stray
    /// Debug.LogError into a failure for whichever test happened to be running.
    /// </summary>
    internal static void IgnoreSceneLogNoise()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    /// <summary>
    /// Restores <see cref="Time.timeScale"/> and unloads every scene the test loaded, so the next
    /// test starts clean regardless of which scene(s) this one left active. Creates and activates a
    /// blank scene first since Unity does not allow unloading the last loaded scene.
    /// </summary>
    internal static IEnumerator UnloadAllLoadedScenes(string blankSceneName)
    {
        Time.timeScale = 1f;
        Scene blank = SceneManager.CreateScene(blankSceneName);
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
    /// Resolves a live component by exact runtime type name, scoped to <paramref name="scene"/>, so the
    /// lookup cannot accidentally match a same-named component left over in another loaded scene.
    /// Walking each root's <see cref="GameObject.GetComponentsInChildren{T}(bool)"/> with
    /// <c>includeInactive: false</c> inspects only GameObjects active in the hierarchy while still
    /// returning a disabled <see cref="MonoBehaviour"/> on an otherwise active GameObject, so a caller's
    /// own <c>enabled</c> assertion can fail correctly instead of this helper silently reporting "not
    /// found". Extracted from <c>Level5MenuScreenPlayModeTests</c> (AUD-012 Phase 2c Slice 46) once
    /// <c>GameplayLevelUnpauseTests</c> (Slice 47) became a second, genuine consumer.
    /// </summary>
    internal static MonoBehaviour FindActiveBehaviourInScene(Scene scene, string runtimeTypeName)
    {
        Assert.That(
            scene.IsValid() && scene.isLoaded,
            Is.True,
            $"scene must be loaded before resolving '{runtimeTypeName}'.");

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(false))
            {
                if (behaviour != null && behaviour.gameObject.scene == scene && behaviour.GetType().Name == runtimeTypeName)
                {
                    return behaviour;
                }
            }
        }

        return null;
    }

    /// <summary>Looks up a private/internal instance or static field by name via reflection.</summary>
    internal static FieldInfo GetFieldInfo(object target, string name)
    {
        return target?.GetType().GetField(name, PrivateInstanceFlags);
    }

    /// <summary>Reads a private/internal instance or static field by name via reflection.</summary>
    internal static object GetField(object target, string name)
    {
        return GetFieldInfo(target, name)?.GetValue(target);
    }

    /// <summary>Reads a private/internal instance or static field by name via reflection, typed.</summary>
    internal static T GetField<T>(object target, string name) where T : class
    {
        return GetField(target, name) as T;
    }

    /// <summary>Writes a private/internal instance or static field by name via reflection.</summary>
    internal static void SetField(object target, string name, object value)
    {
        FieldInfo field = GetFieldInfo(target, name);
        Assert.That(field, Is.Not.Null, $"{target?.GetType().Name} must declare a field named '{name}'");
        field.SetValue(target, value);
    }

    /// <summary>Invokes a private/internal instance or static method by name via reflection.</summary>
    internal static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target?.GetType().GetMethod(name, PrivateInstanceFlags);
        Assert.That(method, Is.Not.Null, $"{target?.GetType().Name} must declare a method named '{name}'");
        return method.Invoke(target, args);
    }

    /// <summary>
    /// Resolves the surviving runtime <c>GameRules</c> instance independently of any binding target's
    /// own field, so a fixture asserting that its bound field is the real live <c>GameRules</c> is not
    /// partially circular (deriving the expected value from the same field being verified). Finds every
    /// live <see cref="MonoBehaviour"/> whose runtime type name is exactly <c>"GameRules"</c> - including
    /// inactive ones, since <c>GameRules</c> itself is never deactivated but this keeps every scan that
    /// uses this resolver consistent - then reads that type's own <c>public static GameRules instance</c>
    /// field through reflection as the authoritative surviving instance. Fails explicitly rather than
    /// guessing if zero components are found, or if the resolved static instance does not correspond to
    /// any discovered live component. <c>GameRules</c> lives in <c>Assembly-CSharp</c>, so this is
    /// reflection-only rather than a compile-time reference - shared by
    /// <c>Level5ShotMarkerSessionCompositionPlayModeTests</c> and
    /// <c>Level5MoneyBallStateCompositionPlayModeTests</c>.
    /// </summary>
    internal static object ResolveRuntimeGameRulesInstance()
    {
        MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        System.Type gameRulesType = null;
        List<MonoBehaviour> candidates = new List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in all)
        {
            if (behaviour != null && behaviour.GetType().Name == "GameRules")
            {
                gameRulesType = behaviour.GetType();
                candidates.Add(behaviour);
            }
        }

        Assert.That(candidates.Count, Is.GreaterThan(0), "no live GameRules component was found in the real gameplay scene");

        FieldInfo instanceField = gameRulesType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
        Assert.That(instanceField, Is.Not.Null, "GameRules must declare a public static 'instance' field");

        object instance = instanceField.GetValue(null);
        Assert.That(instance, Is.Not.Null, "the real gameplay scene must have produced a live GameRules instance");
        Assert.That(instance.GetType().Name, Is.EqualTo("GameRules"));
        Assert.That(candidates.Contains(instance as MonoBehaviour), Is.True,
            "GameRules.instance must be one of the live GameRules components found in the scene");

        return instance;
    }
}
#endif
