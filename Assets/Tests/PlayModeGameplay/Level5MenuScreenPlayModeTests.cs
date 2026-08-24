#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// AUD-112/AUD-103: no test previously loaded a menu scene at all. These smoke-test the screens whose
/// migration to serialized <c>*UiObjects</c>/<see cref="MenuFooterUiObjects"/> references this issue
/// completed and that have no heavy runtime dependency (loaded player-select data, an active match
/// session, a live database wait) that would make an isolated scene load flaky: the scene loads, its
/// manager stays enabled (proving <c>ValidateMenuUi</c> did not disable it for a missing reference),
/// and the EventSystem has a non-null selection.
///
/// Progression, Start and Pause are not covered here - they pull in LoadedData/DBHelper/
/// GameLevelManager/MatchSession, which are not initialized in an isolated scene load and would make
/// the test flaky or slow rather than a meaningful smoke check. Lives in a folder with no asmdef, the
/// same reason <see cref="Level5GameplayPlayModeTests"/> does: everything in Assets/Scripts compiles
/// into Assembly-CSharp, which an asmdef'd test assembly cannot reference.
/// </summary>
public class Level5MenuScreenPlayModeTests
{
    private string loadedSceneName;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!string.IsNullOrEmpty(loadedSceneName))
        {
            Scene scene = SceneManager.GetSceneByName(loadedSceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            loadedSceneName = null;
        }
    }

    private IEnumerator LoadMenuScene(string sceneName)
    {
        loadedSceneName = sceneName;
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        // let Start()/EnsureSelected run before asserting on their results
        yield return null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator OptionsScreenLoadsAndKeepsItsManagerEnabled()
    {
        yield return LoadMenuScene(Constants.SCENE_NAME_level_00_options);

        OptionsManager manager = Object.FindFirstObjectByType<OptionsManager>();
        Assert.That(manager, Is.Not.Null, "OptionsManager was not found in the loaded scene.");
        Assert.That(manager.enabled, Is.True, "OptionsManager disabled itself - a required UI reference is missing.");
        Assert.That(EventSystem.current, Is.Not.Null);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator CreditsScreenLoadsAndKeepsItsManagerEnabled()
    {
        yield return LoadMenuScene(Constants.SCENE_NAME_level_00_credits);

        CreditsManager manager = Object.FindFirstObjectByType<CreditsManager>();
        Assert.That(manager, Is.Not.Null, "CreditsManager was not found in the loaded scene.");
        Assert.That(manager.enabled, Is.True, "CreditsManager disabled itself - a required UI reference is missing.");
        Assert.That(EventSystem.current, Is.Not.Null);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator StatsScreenLoadsAndKeepsItsManagerEnabled()
    {
        yield return LoadMenuScene(Constants.SCENE_NAME_level_00_stats);

        StatsManager manager = Object.FindFirstObjectByType<StatsManager>();
        Assert.That(manager, Is.Not.Null, "StatsManager was not found in the loaded scene.");
        Assert.That(manager.enabled, Is.True, "StatsManager disabled itself - a required UI reference is missing.");
        Assert.That(EventSystem.current, Is.Not.Null);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator AccountHubScreenLoadsAndKeepsItsManagerEnabled()
    {
        yield return LoadMenuScene(Constants.SCENE_NAME_level_00_account);

        AccountManager manager = Object.FindFirstObjectByType<AccountManager>();
        Assert.That(manager, Is.Not.Null, "AccountManager was not found in the loaded scene.");
        Assert.That(manager.enabled, Is.True, "AccountManager disabled itself - a required UI reference is missing.");
        Assert.That(EventSystem.current, Is.Not.Null);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
    }
}
#endif
