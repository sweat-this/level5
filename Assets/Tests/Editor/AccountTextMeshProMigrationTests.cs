using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 5A: the account hub/create/login screens' ordinary directly scene-owned legacy Text was
/// migrated to TextMeshProUGUI on the shared Neon Pixel-7 SDF font asset, while each screen's legacy
/// InputField components and their structural textComponent/placeholder Text dependencies deliberately
/// remain legacy until Phase 5B migrates the InputFields themselves. Mirrors
/// <c>CreditsTextMeshProMigrationTests</c>'s shape: permanent contract tests delegate to
/// <see cref="Level5ProjectValidator"/>, the rest open the real scenes directly.
/// </summary>
public class AccountTextMeshProMigrationTests
{
    private const string HubScenePath = "Assets/Scenes/level_00_account.unity";
    private const string CreateNewScenePath = "Assets/Scenes/level_00_account_createNew.unity";
    private const string LoginExistingScenePath = "Assets/Scenes/level_00_account_loginExisting.unity";
    private const string LoginLocalScenePath = "Assets/Scenes/level_00_account_loginLocal.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private readonly List<Scene> _openedByThisTest = new List<Scene>();

    [TearDown]
    public void TearDown()
    {
        foreach (Scene scene in _openedByThisTest)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        _openedByThisTest.Clear();
    }

    private Scene OpenScene(string scenePath)
    {
        Scene existing = SceneManager.GetSceneByPath(scenePath);
        if (existing.IsValid() && existing.isLoaded)
        {
            return existing;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        _openedByThisTest.Add(scene);
        return scene;
    }

    private static List<Text> OwnedTexts(Scene scene)
    {
        List<Text> owned = new List<Text>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) == null)
                {
                    owned.Add(text);
                }
            }
        }

        return owned;
    }

    private static List<InputField> OwnedInputFields(Scene scene)
    {
        List<InputField> owned = new List<InputField>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (InputField field in root.GetComponentsInChildren<InputField>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(field.gameObject) == null)
                {
                    owned.Add(field);
                }
            }
        }

        return owned;
    }

    // ---------------------------------------------------------------------------------------------
    // 1-4: production scene set is correctly identified; ordinary vs InputField-structural Text
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void HubHasZeroOwnedLegacyTextAndZeroInputField()
    {
        Scene scene = OpenScene(HubScenePath);
        Assert.That(OwnedTexts(scene), Is.Empty, "AUD-092 Phase 5A: account hub must have zero ordinary legacy Text remaining.");
        Assert.That(OwnedInputFields(scene), Is.Empty, "the account hub screen has no InputField at all.");
    }

    [Test]
    public void CreateNewHasExactlyFiveInputFieldsAndTenProtectedText()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        List<InputField> inputFields = OwnedInputFields(scene);
        Assert.That(inputFields, Has.Count.EqualTo(5));

        List<Text> owned = OwnedTexts(scene);
        Assert.That(owned, Has.Count.EqualTo(10), "expected zero ordinary Text - only the 5 InputFields' 10 structural Text dependencies remain (Phase 5B boundary).");
    }

    [Test]
    public void LoginExistingHasExactlyTwoInputFieldsAndFourProtectedText()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        List<InputField> inputFields = OwnedInputFields(scene);
        Assert.That(inputFields, Has.Count.EqualTo(2));

        List<Text> owned = OwnedTexts(scene);
        Assert.That(owned, Has.Count.EqualTo(4), "expected zero ordinary Text - only the 2 InputFields' 4 structural Text dependencies remain (Phase 5B boundary).");
    }

    [Test]
    public void LoginLocalHasNoOwnedTextOrInputFieldToMigrate()
    {
        Scene scene = OpenScene(LoginLocalScenePath);
        Assert.That(OwnedTexts(scene), Is.Empty, "AUD-092 Phase 5A must not introduce a migration target into level_00_account_loginLocal.");
        Assert.That(OwnedInputFields(scene), Is.Empty);
    }

    // ---------------------------------------------------------------------------------------------
    // 5, 9-10: every protected Text is derived from an actual InputField reference, not a name guess
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreateNewProtectedTextComponentsAreExactlyInputFieldDependencies()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AssertOwnedTextIsExactlyInputFieldDependencies(scene, OwnedInputFields(scene), 5);
    }

    [Test]
    public void LoginExistingProtectedTextComponentsAreExactlyInputFieldDependencies()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        AssertOwnedTextIsExactlyInputFieldDependencies(scene, OwnedInputFields(scene), 2);
    }

    private static void AssertOwnedTextIsExactlyInputFieldDependencies(Scene scene, List<InputField> inputFields, int expectedFieldCount)
    {
        Assume.That(inputFields, Has.Count.EqualTo(expectedFieldCount));

        HashSet<Text> derivedFromInputFields = new HashSet<Text>();
        foreach (InputField field in inputFields)
        {
            Assert.That(field.textComponent, Is.Not.Null, field.gameObject.name + ".textComponent must not be null.");
            Assert.That(field.placeholder, Is.Not.Null, field.gameObject.name + ".placeholder must not be null.");

            Text textComponentText = field.textComponent as Text;
            Text placeholderText = field.placeholder as Text;
            Assert.That(textComponentText, Is.Not.Null, field.gameObject.name + ".textComponent must still be a legacy Text (Phase 5B migrates this).");
            Assert.That(placeholderText, Is.Not.Null, field.gameObject.name + ".placeholder must still be a legacy Text (Phase 5B migrates this).");
            Assert.That(textComponentText, Is.Not.SameAs(placeholderText), field.gameObject.name + " : textComponent and placeholder must be distinct.");

            Assert.That(derivedFromInputFields.Add(textComponentText), Is.True, field.gameObject.name + ".textComponent is already claimed by another InputField.");
            Assert.That(derivedFromInputFields.Add(placeholderText), Is.True, field.gameObject.name + ".placeholder is already claimed by another InputField.");
        }

        List<Text> owned = OwnedTexts(scene);
        Assert.That(owned, Has.Count.EqualTo(derivedFromInputFields.Count));
        foreach (Text text in owned)
        {
            Assert.That(
                derivedFromInputFields,
                Has.Member(text),
                "AUD-092 Phase 5A: every remaining legacy Text must be derived from an actual InputField.textComponent/placeholder reference, not a hierarchy-name guess: "
                    + text.gameObject.name);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 11-12: messageDisplay runtime/view references use TMP on the Neon Pixel-7 SDF font
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreateNewMessageDisplayIsTmpOnNeonPixelFont()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AccountCreateUiObjects ui = FindInScene<AccountCreateUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        Assert.That(ui.MessageDisplay, Is.Not.Null);
        TextMeshProUGUI tmp = ui.MessageDisplay as TextMeshProUGUI;
        Assert.That(tmp, Is.Not.Null, "AccountCreateUiObjects.MessageDisplay must be a TextMeshProUGUI.");
        Assert.That(tmp.font, Is.EqualTo(neonPixel));
    }

    [Test]
    public void LoginExistingMessageDisplayIsTmpOnNeonPixelFont()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        AccountLoginUiObjects ui = FindInScene<AccountLoginUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        Assert.That(ui.MessageDisplay, Is.Not.Null);
        TextMeshProUGUI tmp = ui.MessageDisplay as TextMeshProUGUI;
        Assert.That(tmp, Is.Not.Null, "AccountLoginUiObjects.MessageDisplay must be a TextMeshProUGUI.");
        Assert.That(tmp.font, Is.EqualTo(neonPixel));
    }

    [Test]
    public void HubServerMessagesManagerReferencesFiveTmpTextOnNeonPixelFont()
    {
        Scene scene = OpenScene(HubScenePath);
        ServerMessagesManager manager = FindInScene<ServerMessagesManager>(scene);
        Assume.That(manager, Is.Not.Null);

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty array = serialized.FindProperty("serverMessagesText");
        Assume.That(array, Is.Not.Null);
        Assert.That(array.arraySize, Is.EqualTo(5));

        for (int i = 0; i < array.arraySize; i++)
        {
            Object element = array.GetArrayElementAtIndex(i).objectReferenceValue;
            TextMeshProUGUI tmp = element as TextMeshProUGUI;
            Assert.That(tmp, Is.Not.Null, "serverMessagesText[" + i + "] must be a TextMeshProUGUI.");
            Assert.That(tmp.font, Is.EqualTo(neonPixel));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 13: Selectable targetGraphics remain valid and non-legacy
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void HubSelectablesHaveValidNonLegacyTargetGraphics()
    {
        AssertSelectablesHaveValidNonLegacyTargetGraphics(OpenScene(HubScenePath));
    }

    [Test]
    public void CreateNewSelectablesHaveValidNonLegacyTargetGraphics()
    {
        AssertSelectablesHaveValidNonLegacyTargetGraphics(OpenScene(CreateNewScenePath));
    }

    [Test]
    public void LoginExistingSelectablesHaveValidNonLegacyTargetGraphics()
    {
        AssertSelectablesHaveValidNonLegacyTargetGraphics(OpenScene(LoginExistingScenePath));
    }

    private static void AssertSelectablesHaveValidNonLegacyTargetGraphics(Scene scene)
    {
        int checkedCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(selectable.gameObject) != null)
                {
                    continue; // nested prefab instance content (e.g. touch_joystick) - out of scope
                }

                if (selectable is InputField)
                {
                    continue; // Phase 5B boundary - InputField.targetGraphic is not migrated this phase
                }

                checkedCount++;
                Assert.That(selectable.targetGraphic, Is.Not.Null, selectable.gameObject.name + " (" + selectable.GetType().Name + ") has a null targetGraphic.");
                Assert.That(selectable.targetGraphic, Is.Not.InstanceOf<Text>(), selectable.gameObject.name + " still targets a legacy Text component.");
            }
        }

        Assert.That(checkedCount, Is.GreaterThan(0), "expected at least one non-InputField Selectable to check.");
    }

    // ---------------------------------------------------------------------------------------------
    // 14: unknown serialized consumers would block migration (regression guard against the exact
    // ServerMessagesManager.serverMessagesText class of bug this phase's migration had to detect)
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void NoSerializedConsumerReferencesADestroyedLegacyTextComponent()
    {
        foreach (string scenePath in new[] { HubScenePath, CreateNewScenePath, LoginExistingScenePath })
        {
            Scene scene = OpenScene(scenePath);
            HashSet<Object> textSet = new HashSet<Object>(OwnedTexts(scene));
            List<string> findings = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MenuTextConversion.CollectUnsupportedConsumers(root, textSet, findings);
            }

            Assert.That(findings, Is.Empty, scenePath + ":\n- " + string.Join("\n- ", findings));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 15: nested prefab ownership remains untouched (touch_joystick is scene-owned here, not nested -
    // see AccountTextMeshProMigration's class doc comment - so the only nested content across these
    // scenes is level_00_account_loginLocal's touch_joystick instance, which this phase never opens)
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void LoginLocalNestedTouchJoystickInstanceIsUntouched()
    {
        Scene scene = OpenScene(LoginLocalScenePath);
        int nestedTextCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) != null)
                {
                    nestedTextCount++;
                }
            }
        }

        Assert.That(nestedTextCount, Is.GreaterThan(0), "expected level_00_account_loginLocal's nested prefab instance(s) to still carry their own legacy Text - untouched by this phase.");
    }

    // ---------------------------------------------------------------------------------------------
    // 17-19: permanent contract / idempotency (mirrors CreditsTextMeshProMigrationTests: the contract
    // itself is the idempotency guard - Migrate* only ever moves a scene TOWARD this state, so an empty
    // error list here is exactly what a second migration run requires too)
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void AccountTextRenderingContractHasNoErrors()
    {
        List<string> errors = Level5ProjectValidator.CollectAccountTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    // ---------------------------------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------------------------------

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
