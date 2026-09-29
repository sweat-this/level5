#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the real local-profile screens (Account Hub, Create Local Profile, local profile list) the way
/// a player does, for the PlayMode fixtures that certify them
/// (<c>Level5LocalProfileCertificationPlayModeTests</c>, and the Backend V2 live online-account
/// certification's local-profile independence check). Every step goes through a real scene object -
/// buttons are activated with an EventSystem selection plus the UI submit event, never by calling the
/// production method the button is wired to.
///
/// The production managers live in Unity's implicit default assembly, which a named assembly
/// definition (this test assembly) can never reference, so they are reached through
/// <see cref="RealScenePlayModeTestSupport"/>'s reflection helpers.
/// </summary>
internal static class LocalProfileUiFlow
{
    internal const string LoginButtonName = "userAccountLoginButton";

    internal static IEnumerator LoadScene(string sceneName)
    {
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return SettleFrames();
    }

    internal static IEnumerator SettleFrames()
    {
        yield return null;
        yield return null;
        yield return null;
    }

    internal static MonoBehaviour FindInActiveScene(string typeName)
    {
        MonoBehaviour found = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(SceneManager.GetActiveScene(), typeName);
        Assert.That(found, Is.Not.Null, typeName + " was not found in " + SceneManager.GetActiveScene().name);
        return found;
    }

    /// <summary>Reads a public property of a default-assembly manager, which this assembly cannot reference.</summary>
    internal static object ReadManagerProperty(string typeName, string propertyName)
    {
        MonoBehaviour manager = FindInActiveScene(typeName);
        return manager.GetType().GetProperty(propertyName).GetValue(manager);
    }

    internal static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string timeoutMessage)
    {
        yield return WaitUntil(condition, timeoutSeconds, () => timeoutMessage);
    }

    internal static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, Func<string> timeoutMessage)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail(timeoutMessage());
            }

            yield return null;
        }
    }

    internal static void PressButton(Button button)
    {
        Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name + " is not active");
        Assert.That(button.interactable, Is.True, button.name + " is not interactable");
        Assert.That(EventSystem.current, Is.Not.Null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }

    /// <summary>
    /// Account Hub -> the real Create Local Profile button -> level_00_account_createNew -> the real
    /// profile-name field -> the real Create Profile button -> level_00_loading. Reports the created
    /// profile's local id, read back from SQLite rather than from the identity under test.
    /// </summary>
    internal static IEnumerator CreateProfileThroughHub(
        LocalProfileTestDatabase database, string profileName, Action<int> createdUserId)
    {
        yield return LoadScene(Constants.SCENE_NAME_level_00_account);
        MonoBehaviour hubManager = FindInActiveScene("AccountManager");
        Button createNewButton = RealScenePlayModeTestSupport.GetField<Button>(hubManager, "createNewButton");
        Assert.That(createNewButton, Is.Not.Null, "the Account Hub has no Create Local Profile button");

        PressButton(createNewButton);
        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_account_createNew, 30f,
            "the Create Local Profile button did not open level_00_account_createNew");
        yield return SettleFrames();

        MonoBehaviour createManager = FindInActiveScene("AccountManager");
        object nameField = RealScenePlayModeTestSupport.GetField(createManager, "usernameInputField");
        Button createButton = RealScenePlayModeTestSupport.GetField<Button>(createManager, "createAccountButton");
        Assert.That(nameField, Is.Not.Null, "the create screen has no profile-name field");
        Assert.That(createButton, Is.Not.Null, "the create screen has no Create Profile button");

        nameField.GetType().GetProperty("text").SetValue(nameField, profileName);
        PressButton(createButton);

        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_loading, 30f,
            "Create Profile did not proceed to level_00_loading");

        KeyValuePair<int, string> row = database.ReadUsers().SingleOrDefault(u => u.Value == profileName);
        Assert.That(row.Value, Is.EqualTo(profileName), "the created profile was not committed to SQLite");
        createdUserId(row.Key);
    }

    /// <summary>
    /// Waits for exactly <paramref name="expected"/> rows to be rendered by the real
    /// <c>UserAccountManager</c> in the active (loginLocal) scene and returns each row's own login button.
    /// </summary>
    internal static IEnumerator WaitForProfileRows(int expected, Action<List<Button>> rows)
    {
        MonoBehaviour manager = FindInActiveScene("UserAccountManager");
        GameObject spawn = RealScenePlayModeTestSupport.GetField<GameObject>(manager, "localAccountPrefabSpawnLocation");
        Assert.That(spawn, Is.Not.Null, "UserAccountManager has no row container");

        List<Button> buttons = new List<Button>();
        yield return WaitUntil(
            () =>
            {
                buttons = LoginButtons(spawn);
                return buttons.Count >= expected;
            },
            30f,
            () => "the profile rows were not rendered - the list shows " + buttons.Count + " of " + expected);

        // Rows are instantiated in one synchronous pass; settle a frame so a surplus row would show up.
        yield return null;
        buttons = LoginButtons(spawn);
        Assert.That(buttons, Has.Count.EqualTo(expected));
        rows(buttons);
    }

    private static List<Button> LoginButtons(GameObject spawn)
    {
        return spawn.GetComponentsInChildren<Button>(false).Where(b => b.name == LoginButtonName).ToList();
    }

    /// <summary>The name a row displays - the same first child of the button's parent that LocalAccount reads.</summary>
    internal static string RowName(Button loginButton)
    {
        return loginButton.transform.parent.GetChild(0).GetComponent<Text>().text;
    }

    /// <summary>
    /// Selects the row exactly as a player does - EventSystem selection on the row's own button, then the
    /// button's submit - and waits for the loading scene. <c>LocalAccount</c> reads the selected row's name
    /// from the EventSystem selection in its own Update, so the selection must exist for a frame before
    /// the press.
    /// </summary>
    internal static IEnumerator SelectProfileRow(Button loginButton)
    {
        EventSystem.current.SetSelectedGameObject(loginButton.gameObject);
        yield return null;
        yield return null;

        PressButton(loginButton);
        yield return WaitUntil(
            () => SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_loading, 30f,
            "the profile row's button did not proceed to level_00_loading");
    }
}
#endif
