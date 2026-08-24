using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-103/AUD-104: menu managers now carry a serialized <c>*UiObjects</c>/
/// <see cref="MenuFooterUiObjects"/> view instead of resolving references with
/// <c>GameObject.Find(name)</c>. These tests cover the contract every one of those views and their
/// owning managers now share: a missing required field is reported by its own name, an inactive
/// referenced object still counts as present (the point of moving off name lookup, which cannot see
/// inactive objects), and a footer only requires the subset of buttons the caller actually uses.
/// </summary>
public class Level5MenuUiObjectsTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject gameObject in spawned)
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        spawned.Clear();
    }

    private T Spawn<T>() where T : Component
    {
        GameObject gameObject = new GameObject(typeof(T).Name);
        spawned.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, target.GetType().Name + " has no field '" + fieldName + "'.");
        field.SetValue(target, value);
    }

    private Button SpawnButton(bool active = true)
    {
        Button button = Spawn<Button>();
        button.gameObject.SetActive(active);
        return button;
    }

    private GameObject SpawnGameObject()
    {
        GameObject gameObject = new GameObject("plain");
        spawned.Add(gameObject);
        return gameObject;
    }

    // ---------- OptionsUiObjects ----------

    [Test]
    public void OptionsUiObjectsReportsEveryMissingFieldByName()
    {
        OptionsUiObjects ui = Spawn<OptionsUiObjects>();
        List<string> missing = new List<string>();

        bool valid = ui.Validate(missing);

        Assert.That(valid, Is.False);
        Assert.That(missing, Is.EquivalentTo(new[]
        {
            "OptionsUiObjects.keyboardOnlyButton",
            "OptionsUiObjects.keyboardMouseButton",
            "OptionsUiObjects.gamepadButton",
            "OptionsUiObjects.touchButton",
            "OptionsUiObjects.keyboardOnlyObject",
            "OptionsUiObjects.keyboardMouseObject",
            "OptionsUiObjects.gamepadObject",
            "OptionsUiObjects.touchObject",
        }));
    }

    [Test]
    public void OptionsUiObjectsValidatesWithAnInactiveButtonAssigned()
    {
        OptionsUiObjects ui = Spawn<OptionsUiObjects>();
        Button inactiveButton = SpawnButton(active: false);
        SetField(ui, "keyboardOnlyButton", inactiveButton);
        SetField(ui, "keyboardMouseButton", SpawnButton());
        SetField(ui, "gamepadButton", SpawnButton());
        SetField(ui, "touchButton", SpawnButton());
        SetField(ui, "keyboardOnlyObject", SpawnGameObject());
        SetField(ui, "keyboardMouseObject", SpawnGameObject());
        SetField(ui, "gamepadObject", SpawnGameObject());
        SetField(ui, "touchObject", SpawnGameObject());

        List<string> missing = new List<string>();
        bool valid = ui.Validate(missing);

        Assert.That(valid, Is.True, string.Join(", ", missing));
        Assert.That(ui.KeyboardOnlyButton, Is.SameAs(inactiveButton));
        Assert.That(ui.KeyboardOnlyButton.gameObject.activeSelf, Is.False);
    }

    // ---------- MenuFooterUiObjects ----------

    [Test]
    public void FooterOnlyRequiresTheCallerSpecifiedSubset()
    {
        MenuFooterUiObjects footer = Spawn<MenuFooterUiObjects>();
        Button accountButton = SpawnButton();
        SetField(footer, "accountButton", accountButton);

        List<string> missing = new List<string>();
        bool valid = footer.Validate(missing, (footer.AccountButton, "accountButton"));

        Assert.That(valid, Is.True, string.Join(", ", missing));

        // the same footer, asked for a button it does not have, reports exactly that one
        missing.Clear();
        valid = footer.Validate(
            missing,
            (footer.AccountButton, "accountButton"),
            (footer.QuitButton, "quitButton"));

        Assert.That(valid, Is.False);
        Assert.That(missing, Is.EquivalentTo(new[] { "MenuFooterUiObjects.quitButton" }));
    }

    // ---------- ProgressionUiObjects ----------

    [Test]
    public void ProgressionUiObjectsReportsMissingConfirmationDialogueBoxByName()
    {
        ProgressionUiObjects ui = Spawn<ProgressionUiObjects>();

        List<string> missing = new List<string>();
        ui.Validate(missing);

        Assert.That(missing, Contains.Item("ProgressionUiObjects.confirmationDialogueBox"));
    }

    // ---------- PauseUiObjects ----------

    [Test]
    public void PauseUiObjectsDoesNotRequireTheFooter()
    {
        PauseUiObjects ui = Spawn<PauseUiObjects>();
        SetField(ui, "fadeTexture", Spawn<Image>());
        SetField(ui, "loadSceneText", Spawn<Text>());
        SetField(ui, "loadStartScreenText", Spawn<Text>());
        SetField(ui, "cancelMenuText", Spawn<Text>());
        SetField(ui, "quitGameText", Spawn<Text>());
        SetField(ui, "loadSceneButton", SpawnButton());
        SetField(ui, "loadStartScreenButton", SpawnButton());
        SetField(ui, "cancelMenuButton", SpawnButton());
        SetField(ui, "quitGameButton", SpawnButton());
        SetField(ui, "toggleUiStatsText", Spawn<Text>());
        SetField(ui, "toggleMaxStatsText", Spawn<Text>());
        SetField(ui, "toggleFpsText", Spawn<Text>());
        // footer left unassigned deliberately

        List<string> missing = new List<string>();
        bool valid = ui.Validate(missing);

        Assert.That(valid, Is.True, string.Join(", ", missing));
        Assert.That(ui.Footer, Is.Null);
    }

    // ---------- Manager-level ValidateMenuUi ----------

    [Test]
    public void OptionsManagerReportsMissingUiAndFooterByName()
    {
        OptionsManager manager = Spawn<OptionsManager>();

        List<string> missing = new List<string>();
        bool valid = manager.ValidateMenuUi(missing);

        Assert.That(valid, Is.False);
        Assert.That(missing, Is.EquivalentTo(new[] { "OptionsManager.ui", "OptionsManager.footer" }));
    }

    [Test]
    public void AccountManagerRequiresExactlyOneScreenVariant()
    {
        AccountManager manager = Spawn<AccountManager>();
        SetField(manager, "footer", Spawn<MenuFooterUiObjects>());

        List<string> missing = new List<string>();
        bool valid = manager.ValidateMenuUi(missing);

        Assert.That(valid, Is.False);
        Assert.That(
            missing,
            Contains.Item("AccountManager.hubUi/createUi/loginUi (exactly one must be assigned)"));
    }
}
