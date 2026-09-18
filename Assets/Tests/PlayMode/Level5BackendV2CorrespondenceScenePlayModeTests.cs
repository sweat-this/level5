using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Smoke test for the #159 correspondence scene: it loads, its <c>CorrespondenceScreenController</c>
/// builds its runtime UI, and the unauthenticated (no Backend V2 session in a fresh test run) path
/// shows the login panel - all without throwing or logging an error. Unity Test Framework fails a
/// <see cref="UnityTest"/> on any unhandled <c>Debug.LogError</c>/exception during its yielded
/// frames, which is the actual assertion here.
///
/// Cannot reference <c>CorrespondenceScreenController</c> by type: it lives in the default assembly
/// (next to <c>StartManager</c>/<c>GameRules</c>), which no custom assembly definition - including
/// this project's own <c>Level5.PlayModeTests.asmdef</c> - can ever reference (a Unity engine
/// restriction, not a project choice). <c>Level5GameplayPlayModeTests.cs</c> establishes the same
/// pattern already: verify default-assembly MonoBehaviour behavior indirectly, through effects
/// visible to this assembly (here, ordinary <c>UnityEngine</c> types - GameObject, Canvas).
/// </summary>
public class Level5BackendV2CorrespondenceScenePlayModeTests
{
    [UnityTest]
    public IEnumerator TheCorrespondenceSceneLoadsAndBuildsItsUiWithoutErrors()
    {
        yield return SceneManager.LoadSceneAsync("level_00_multiplayer");

        // A few frames for Awake/Start/the login-or-refresh resume coroutine to run.
        yield return null;
        yield return null;
        yield return null;

        GameObject screen = GameObject.Find("CorrespondenceScreen");
        Assert.That(screen, Is.Not.Null, "the scene must contain the CorrespondenceScreen GameObject");
        Assert.That(
            screen.GetComponentInChildren<Canvas>(), Is.Not.Null,
            "CorrespondenceScreenController must have built its runtime Canvas");
        Assert.That(
            GameObject.Find("LoginPanel"), Is.Not.Null,
            "no Backend V2 session exists in a fresh test run, so the login panel must be present");
    }
}
