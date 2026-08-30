using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 5A migrated the account hub/create/login screens' ordinary directly scene-owned legacy
/// Text to TextMeshProUGUI. AUD-092 Phase 5B (this file, since) migrates each screen's legacy InputField
/// components themselves, and their structural textComponent/placeholder Text dependencies, to
/// TMP_InputField/TextMeshProUGUI, fixes password masking, replaces the legacy EventTrigger Submit
/// architecture with native TMP_InputField.onSubmit, and consolidates account-form button ownership.
/// Mirrors <c>CreditsTextMeshProMigrationTests</c>'s shape: permanent contract tests delegate to
/// <see cref="Level5ProjectValidator"/>, the rest open the real scenes directly.
/// </summary>
public class AccountTextMeshProMigrationTests
{
    private const string HubScenePath = "Assets/Scenes/level_00_account.unity";
    private const string CreateNewScenePath = "Assets/Scenes/level_00_account_createNew.unity";
    private const string LoginExistingScenePath = "Assets/Scenes/level_00_account_loginExisting.unity";
    private const string LoginLocalScenePath = "Assets/Scenes/level_00_account_loginLocal.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private const int CreateNewExpectedTotalTmpTextCount = 22;
    private const int LoginExistingExpectedTotalTmpTextCount = 12;

    private static readonly string[] CreateNewFieldGameObjectNames =
    {
        "EmailInputField", "UserNameInputField", "PasswordInputField", "FirstNameInputField", "LastNameInputField",
    };

    private static readonly string[] LoginExistingFieldGameObjectNames =
    {
        "UserNameInputField", "PasswordInputField",
    };

    private readonly List<Scene> _openedByThisTest = new List<Scene>();
    private readonly List<Object> _instantiated = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object instance in _instantiated)
        {
            if (instance == null)
            {
                continue;
            }

            // A manually reflection-invoked OnEnable (see InstallScratchEventSystem) never flows through
            // Unity's normal enable bookkeeping, so DestroyImmediate below does not reliably dispatch a
            // matching OnDisable - EventSystem.OnDisable is what removes an instance from its internal
            // static registry, so skipping it leaks a stale entry that can make a LATER test's (including
            // ones in other files, e.g. CreditsTextMeshProMigrationTests) EventSystem.current resolve to
            // this already-destroyed instance instead of its own. Invoking OnDisable explicitly first
            // balances the manual OnEnable and keeps this test file's scratch EventSystems from leaking
            // across the whole suite.
            if (instance is GameObject gameObject && gameObject.TryGetComponent(out EventSystem eventSystem))
            {
                typeof(EventSystem)
                    .GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(eventSystem, null);
            }

            Object.DestroyImmediate(instance);
        }

        _instantiated.Clear();

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

    private static List<InputField> OwnedLegacyInputFields(Scene scene)
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

    private static List<TMP_InputField> OwnedTmpInputFields(Scene scene)
    {
        List<TMP_InputField> owned = new List<TMP_InputField>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_InputField field in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(field.gameObject) == null)
                {
                    owned.Add(field);
                }
            }
        }

        return owned;
    }

    private static Dictionary<string, TMP_InputField> OwnedTmpInputFieldsByName(Scene scene)
    {
        Dictionary<string, TMP_InputField> byName = new Dictionary<string, TMP_InputField>();
        foreach (TMP_InputField field in OwnedTmpInputFields(scene))
        {
            byName[field.gameObject.name] = field;
        }

        return byName;
    }

    // ---------------------------------------------------------------------------------------------
    // 1-6: production scene set is correctly identified; zero legacy, expected TMP counts
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void HubHasZeroOwnedLegacyTextAndZeroInputField()
    {
        Scene scene = OpenScene(HubScenePath);
        Assert.That(OwnedTexts(scene), Is.Empty, "account hub must have zero ordinary legacy Text remaining.");
        Assert.That(OwnedLegacyInputFields(scene), Is.Empty, "the account hub screen has no InputField at all.");
    }

    [Test]
    public void LoginLocalHasNoOwnedTextOrInputFieldToMigrate()
    {
        Scene scene = OpenScene(LoginLocalScenePath);
        Assert.That(OwnedTexts(scene), Is.Empty, "AUD-092 must not introduce a migration target into level_00_account_loginLocal.");
        Assert.That(OwnedLegacyInputFields(scene), Is.Empty);
    }

    [Test]
    public void CreateNewHasExactlyFiveTmpInputFieldsAndZeroLegacy()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        Assert.That(OwnedLegacyInputFields(scene), Is.Empty, "AUD-092 Phase 5B: zero legacy InputField must remain.");
        Assert.That(OwnedTexts(scene), Is.Empty, "AUD-092 Phase 5B: zero legacy Text must remain.");
        Assert.That(OwnedTmpInputFields(scene), Has.Count.EqualTo(5));
    }

    [Test]
    public void LoginExistingHasExactlyTwoTmpInputFieldsAndZeroLegacy()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        Assert.That(OwnedLegacyInputFields(scene), Is.Empty, "AUD-092 Phase 5B: zero legacy InputField must remain.");
        Assert.That(OwnedTexts(scene), Is.Empty, "AUD-092 Phase 5B: zero legacy Text must remain.");
        Assert.That(OwnedTmpInputFields(scene), Has.Count.EqualTo(2));
    }

    [Test]
    public void CreateNewHasExpectedTotalTextMeshProCount()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(tmp.gameObject) == null)
                {
                    count++;
                }
            }
        }

        Assert.That(count, Is.EqualTo(CreateNewExpectedTotalTmpTextCount), "expected the 12 Phase 5A ordinary labels plus 2 per migrated InputField (5 fields x 2 = 10).");
    }

    [Test]
    public void LoginExistingHasExpectedTotalTextMeshProCount()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(tmp.gameObject) == null)
                {
                    count++;
                }
            }
        }

        Assert.That(count, Is.EqualTo(LoginExistingExpectedTotalTmpTextCount), "expected the 8 Phase 5A ordinary labels plus 2 per migrated InputField (2 fields x 2 = 4).");
    }

    // ---------------------------------------------------------------------------------------------
    // 6-9: viewport/mask, content/placeholder font
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreateNewFieldsHaveValidViewportsWithRectMask2D()
    {
        AssertFieldsHaveValidViewports(OpenScene(CreateNewScenePath), CreateNewFieldGameObjectNames);
    }

    [Test]
    public void LoginExistingFieldsHaveValidViewportsWithRectMask2D()
    {
        AssertFieldsHaveValidViewports(OpenScene(LoginExistingScenePath), LoginExistingFieldGameObjectNames);
    }

    private static void AssertFieldsHaveValidViewports(Scene scene, string[] expectedNames)
    {
        Dictionary<string, TMP_InputField> byName = OwnedTmpInputFieldsByName(scene);
        foreach (string name in expectedNames)
        {
            Assert.That(byName.ContainsKey(name), Is.True, "expected a TMP_InputField named '" + name + "'.");
            TMP_InputField field = byName[name];

            Assert.That(field.textViewport, Is.Not.Null, name + ".textViewport is null.");
            Assert.That(field.textViewport.gameObject.name, Is.EqualTo("Text Area"));
            Assert.That(field.textViewport.parent, Is.EqualTo(field.transform));
            Assert.That(field.textViewport.GetComponent<RectMask2D>(), Is.Not.Null, name + ".textViewport must carry a RectMask2D to clip text/caret/selection.");
        }
    }

    [Test]
    public void CreateNewFieldsContentAndPlaceholderAreTmpOnNeonPixelFont()
    {
        AssertFieldsContentAndPlaceholderAreTmp(OpenScene(CreateNewScenePath), CreateNewFieldGameObjectNames);
    }

    [Test]
    public void LoginExistingFieldsContentAndPlaceholderAreTmpOnNeonPixelFont()
    {
        AssertFieldsContentAndPlaceholderAreTmp(OpenScene(LoginExistingScenePath), LoginExistingFieldGameObjectNames);
    }

    private static void AssertFieldsContentAndPlaceholderAreTmp(Scene scene, string[] expectedNames)
    {
        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        Dictionary<string, TMP_InputField> byName = OwnedTmpInputFieldsByName(scene);
        foreach (string name in expectedNames)
        {
            TMP_InputField field = byName[name];

            TextMeshProUGUI content = field.textComponent as TextMeshProUGUI;
            Assert.That(content, Is.Not.Null, name + ".textComponent must be a TextMeshProUGUI.");
            Assert.That(content.font, Is.EqualTo(neonPixel));
            Assert.That(content.transform.parent, Is.EqualTo(field.textViewport));

            TextMeshProUGUI placeholder = field.placeholder as TextMeshProUGUI;
            Assert.That(placeholder, Is.Not.Null, name + ".placeholder must be a TextMeshProUGUI.");
            Assert.That(placeholder.font, Is.EqualTo(neonPixel));
            Assert.That(placeholder.transform.parent, Is.EqualTo(field.textViewport));

            Assert.That(content, Is.Not.SameAs(placeholder));
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 10: richText disabled on all 7 fields
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void AllSevenAccountInputFieldsDisableRichText()
    {
        foreach (TMP_InputField field in OwnedTmpInputFields(OpenScene(CreateNewScenePath)))
        {
            Assert.That(field.richText, Is.False, field.gameObject.name + " : richText must stay disabled.");
        }

        foreach (TMP_InputField field in OwnedTmpInputFields(OpenScene(LoginExistingScenePath)))
        {
            Assert.That(field.richText, Is.False, field.gameObject.name + " : richText must stay disabled.");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 11: password ContentType.Password; non-password semantics preserved (Standard, SingleLine)
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void BothPasswordFieldsUseContentTypePassword()
    {
        TMP_InputField createPassword = OwnedTmpInputFieldsByName(OpenScene(CreateNewScenePath))["PasswordInputField"];
        Assert.That(createPassword.contentType, Is.EqualTo(TMP_InputField.ContentType.Password));

        TMP_InputField loginPassword = OwnedTmpInputFieldsByName(OpenScene(LoginExistingScenePath))["PasswordInputField"];
        Assert.That(loginPassword.contentType, Is.EqualTo(TMP_InputField.ContentType.Password));
    }

    [Test]
    public void NonPasswordFieldsPreserveStandardContentTypeAndSingleLine()
    {
        Dictionary<string, TMP_InputField> createFields = OwnedTmpInputFieldsByName(OpenScene(CreateNewScenePath));
        foreach (string name in new[] { "EmailInputField", "UserNameInputField", "FirstNameInputField", "LastNameInputField" })
        {
            TMP_InputField field = createFields[name];
            Assert.That(field.contentType, Is.EqualTo(TMP_InputField.ContentType.Standard), name + " must not be affected by the password-only ContentType fix.");
            Assert.That(field.lineType, Is.EqualTo(TMP_InputField.LineType.SingleLine));
        }

        TMP_InputField loginUsername = OwnedTmpInputFieldsByName(OpenScene(LoginExistingScenePath))["UserNameInputField"];
        Assert.That(loginUsername.contentType, Is.EqualTo(TMP_InputField.ContentType.Standard));
        Assert.That(loginUsername.lineType, Is.EqualTo(TMP_InputField.LineType.SingleLine));
    }

    // ---------------------------------------------------------------------------------------------
    // 12: external Navigation references (the sibling label Buttons' selectOnRight) are repaired to
    // point at the new TMP_InputField, not a dangling reference to the destroyed legacy InputField
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreateNewSiblingButtonsNavigationRepairedToTmpInputFields()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AssertSiblingNavigationRepaired(scene, "emailText", "EmailInputField");
        AssertSiblingNavigationRepaired(scene, "passwordText", "PasswordInputField");
        AssertSiblingNavigationRepaired(scene, "firstNameText", "FirstNameInputField");
        AssertSiblingNavigationRepaired(scene, "lastNameText", "LastNameInputField");
        AssertSiblingNavigationRepaired(scene, "userNameText", "UserNameInputField");
    }

    [Test]
    public void LoginExistingSiblingButtonsNavigationRepairedToTmpInputFields()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        AssertSiblingNavigationRepaired(scene, "passwordText", "PasswordInputField");
        AssertSiblingNavigationRepaired(scene, "userNameText", "UserNameInputField");
    }

    private static void AssertSiblingNavigationRepaired(Scene scene, string labelButtonName, string fieldGameObjectName)
    {
        Button labelButton = FindButtonNamed(scene, labelButtonName);
        Assume.That(labelButton, Is.Not.Null, "expected a Button named '" + labelButtonName + "'.");

        TMP_InputField field = OwnedTmpInputFieldsByName(scene)[fieldGameObjectName];
        Assert.That(
            labelButton.navigation.selectOnRight,
            Is.SameAs(field),
            labelButtonName + ".navigation.selectOnRight must reference the new TMP_InputField, not a dangling reference to the destroyed legacy InputField.");
    }

    private static Button FindButtonNamed(Scene scene, string gameObjectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == gameObjectName)
                {
                    return button;
                }
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------------------------------
    // 13-14: AccountCreateUiObjects/AccountLoginUiObjects resolve every field plus the terminal button
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void AccountCreateUiObjectsResolvesAllFieldsAndCreateAccountButton()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AccountCreateUiObjects ui = FindInScene<AccountCreateUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        List<string> missing = new List<string>();
        Assert.That(ui.Validate(missing), Is.True, string.Join(", ", missing));

        Assert.That(ui.EmailInputField, Is.Not.Null);
        Assert.That(ui.UsernameInputField, Is.Not.Null);
        Assert.That(ui.PasswordInputField, Is.Not.Null);
        Assert.That(ui.FirstNameInputField, Is.Not.Null);
        Assert.That(ui.LastNameInputField, Is.Not.Null);
        Assert.That(ui.CreateAccountButton, Is.Not.Null);
        Assert.That(ui.CreateAccountButton.gameObject.name, Is.EqualTo("createUserButton"));
    }

    [Test]
    public void AccountLoginUiObjectsResolvesAllFieldsAndLoginButton()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        AccountLoginUiObjects ui = FindInScene<AccountLoginUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        List<string> missing = new List<string>();
        Assert.That(ui.Validate(missing), Is.True, string.Join(", ", missing));

        Assert.That(ui.UsernameInputField, Is.Not.Null);
        Assert.That(ui.PasswordInputField, Is.Not.Null);
        Assert.That(ui.LoginButton, Is.Not.Null);
        Assert.That(ui.LoginButton.gameObject.name, Is.EqualTo("loginButton"));
    }

    // ---------------------------------------------------------------------------------------------
    // 15: no obsolete onValueChanged persistent listeners remain (read*Input wiring is gone because the
    // legacy InputField carrying it was destroyed outright, never migrated)
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void NoObsoletePersistentOnValueChangedListenersRemain()
    {
        foreach (string scenePath in new[] { CreateNewScenePath, LoginExistingScenePath })
        {
            Scene scene = OpenScene(scenePath);
            foreach (TMP_InputField field in OwnedTmpInputFields(scene))
            {
                Assert.That(
                    field.onValueChanged.GetPersistentEventCount(),
                    Is.EqualTo(0),
                    scenePath + " -> " + field.gameObject.name + " must carry no persistent onValueChanged listener.");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 16: no legacy EventTrigger submit architecture remains on any migrated field
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void NoEventTriggerSubmitArchitectureRemainsOnAnyField()
    {
        foreach (string scenePath in new[] { CreateNewScenePath, LoginExistingScenePath })
        {
            Scene scene = OpenScene(scenePath);
            foreach (TMP_InputField field in OwnedTmpInputFields(scene))
            {
                Assert.That(
                    field.GetComponent<EventTrigger>(),
                    Is.Null,
                    scenePath + " -> " + field.gameObject.name + " must carry no EventTrigger - Submit is native TMP_InputField.onSubmit now.");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 17-18: account form actions have exactly one behavioral owner (code); no stale onClick listener
    // (including the pre-rename "LoginManager" type name landmine) targets AccountManager any more
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void NoStalePersistentOnClickListenersTargetAccountManager()
    {
        foreach (string scenePath in new[] { CreateNewScenePath, LoginExistingScenePath })
        {
            Scene scene = OpenScene(scenePath);
            AccountManager manager = FindInScene<AccountManager>(scene);
            Assume.That(manager, Is.Not.Null);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        Assert.That(
                            button.onClick.GetPersistentTarget(i),
                            Is.Not.SameAs(manager),
                            scenePath + " -> " + button.gameObject.name + " : must carry no persistent onClick listener targeting AccountManager"
                                + " (method '" + button.onClick.GetPersistentMethodName(i) + "') - Check Email/Check Username/Create Account/Login are code-owned now.");
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 19-20: Selectable targetGraphics remain valid and non-legacy (InputFields now included - they are
    // TMP-based Selectables, not exempt any more)
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

                checkedCount++;
                Assert.That(selectable.targetGraphic, Is.Not.Null, selectable.gameObject.name + " (" + selectable.GetType().Name + ") has a null targetGraphic.");
                Assert.That(selectable.targetGraphic, Is.Not.InstanceOf<Text>(), selectable.gameObject.name + " still targets a legacy Text component.");
            }
        }

        Assert.That(checkedCount, Is.GreaterThan(0), "expected at least one Selectable to check.");
    }

    // ---------------------------------------------------------------------------------------------
    // 21: unknown serialized consumers would block migration
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
    // 22: nested prefab ownership remains untouched
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
    // 23-24: submit progression matches the defined form flow, driven through the real scene instance
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreateNewSubmitDestinationsMatchDefinedProgression()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AccountManager manager = FindInScene<AccountManager>(scene);
        Assume.That(manager, Is.Not.Null);

        EventSystem eventSystem = InstallScratchEventSystem();
        InvokePrivate(manager, "ResolveUiReferences");
        InvokePrivate(manager, "RegisterInputSubmitCallbacks");

        Dictionary<string, TMP_InputField> fields = OwnedTmpInputFieldsByName(scene);
        AccountCreateUiObjects ui = FindInScene<AccountCreateUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        AssertSubmitSelects(fields["EmailInputField"], eventSystem, ui.CheckEmailButton.gameObject, "Email");
        AssertSubmitSelects(fields["UserNameInputField"], eventSystem, ui.CheckUserNameButton.gameObject, "Username");
        AssertSubmitSelects(fields["PasswordInputField"], eventSystem, fields["FirstNameInputField"].gameObject, "Password");
        AssertSubmitSelects(fields["FirstNameInputField"], eventSystem, fields["LastNameInputField"].gameObject, "First Name");
        AssertSubmitSelects(fields["LastNameInputField"], eventSystem, ui.CreateAccountButton.gameObject, "Last Name");
    }

    [Test]
    public void LoginExistingSubmitDestinationsMatchDefinedProgression()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        AccountManager manager = FindInScene<AccountManager>(scene);
        Assume.That(manager, Is.Not.Null);

        EventSystem eventSystem = InstallScratchEventSystem();
        InvokePrivate(manager, "ResolveUiReferences");
        InvokePrivate(manager, "RegisterInputSubmitCallbacks");

        Dictionary<string, TMP_InputField> fields = OwnedTmpInputFieldsByName(scene);
        AccountLoginUiObjects ui = FindInScene<AccountLoginUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        AssertSubmitSelects(fields["UserNameInputField"], eventSystem, ui.CheckUserNameButton.gameObject, "Username");
        AssertSubmitSelects(fields["PasswordInputField"], eventSystem, ui.LoginButton.gameObject, "Password");
    }

    private static void AssertSubmitSelects(TMP_InputField field, EventSystem eventSystem, GameObject expectedTarget, string fieldLabel)
    {
        eventSystem.SetSelectedGameObject(null);
        field.onSubmit.Invoke(field.text);
        Assert.That(
            eventSystem.currentSelectedGameObject,
            Is.SameAs(expectedTarget),
            fieldLabel + " Submit must select '" + expectedTarget.name + "', not a self-selection/dead-end.");
    }

    /// <summary>
    /// Proves the native onSubmit registration is idempotent the same way
    /// <c>CreditsTextMeshProMigrationTests.RepeatedRegisterCallsDoNotAccumulateSubmitListeners</c> proves
    /// it for Credits: registering 3 times still leaves exactly one live listener per field, so a single
    /// Unregister call fully silences it. If Register had instead accumulated 3 separate listener
    /// entries, the second Invoke below would still fire after only one Unregister call.
    /// </summary>
    [Test]
    public void RepeatedRegisterCallsDoNotAccumulateSubmitListeners()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AccountManager manager = FindInScene<AccountManager>(scene);
        Assume.That(manager, Is.Not.Null);

        EventSystem eventSystem = InstallScratchEventSystem();
        InvokePrivate(manager, "ResolveUiReferences");

        for (int i = 0; i < 3; i++)
        {
            InvokePrivate(manager, "RegisterInputSubmitCallbacks");
        }

        TMP_InputField emailField = OwnedTmpInputFieldsByName(scene)["EmailInputField"];
        AccountCreateUiObjects ui = FindInScene<AccountCreateUiObjects>(scene);
        Assume.That(ui, Is.Not.Null);

        emailField.onSubmit.Invoke(string.Empty);
        Assert.That(
            eventSystem.currentSelectedGameObject,
            Is.SameAs(ui.CheckEmailButton.gameObject),
            "expected the submit listener to select the Check Email button after 3 Register calls.");

        InvokePrivate(manager, "UnregisterInputSubmitCallbacks");
        eventSystem.SetSelectedGameObject(null);

        emailField.onSubmit.Invoke(string.Empty);
        Assert.That(
            eventSystem.currentSelectedGameObject,
            Is.Null,
            "a single Unregister call must fully clear the listener - if Register had accumulated duplicate listeners, this would still fire.");
    }

    // ---------------------------------------------------------------------------------------------
    // 25: duplicate Create/Login activation is guarded. Network-free: proves the early-return short
    // circuit (the guard field is left exactly as it was found rather than reset by a fresh, unwanted
    // coroutine start), not the full end-to-end coroutine behavior, which would require mocking
    // APIHelper's network calls - infrastructure this codebase does not have and this phase does not add.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void DuplicateCreateAccountActivationIsGuarded()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AccountManager manager = FindInScene<AccountManager>(scene);
        Assume.That(manager, Is.Not.Null);

        SetPrivateField(manager, "isCreatingAccount", true);
        InvokePublic(manager, "createUser");
        Assert.That(GetPrivateField<bool>(manager, "isCreatingAccount"), Is.True, "a second createUser() activation while one is already in flight must not disturb the guard.");
    }

    [Test]
    public void DuplicateLoginActivationIsGuarded()
    {
        Scene scene = OpenScene(LoginExistingScenePath);
        AccountManager manager = FindInScene<AccountManager>(scene);
        Assume.That(manager, Is.Not.Null);

        SetPrivateField(manager, "isLoggingIn", true);
        InvokePublic(manager, "LoginUser");
        Assert.That(GetPrivateField<bool>(manager, "isLoggingIn"), Is.True, "a second LoginUser() activation while one is already in flight must not disturb the guard.");
    }

    // ---------------------------------------------------------------------------------------------
    // Idempotency is more than a field-count no-op: a second run must still repair peripheral drift
    // (password ContentType, stale persistent onClick listeners) against fields that are already
    // TMP_InputField, not just skip straight past them. Both drift scenarios are simulated in memory
    // only (never saved to disk - TearDown/CloseScene discards them) and repaired via the same private
    // method the real Migrate* entry points call.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void SecondMigrationRunRepairsDriftedPasswordContentTypeWithoutReconvertingFields()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        TMP_InputField passwordFieldBefore = OwnedTmpInputFieldsByName(scene)["PasswordInputField"];
        Assume.That(passwordFieldBefore.contentType, Is.EqualTo(TMP_InputField.ContentType.Password));

        // Simulate drift after the original migration - e.g. a manual Inspector edit or a bad merge
        // resets it back to Standard.
        passwordFieldBefore.contentType = TMP_InputField.ContentType.Standard;

        List<string> errors = InvokeMigrateFieldsScreenInputFieldsInMemory(scene, 5);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));

        TMP_InputField passwordFieldAfter = OwnedTmpInputFieldsByName(scene)["PasswordInputField"];
        Assert.That(
            passwordFieldAfter,
            Is.SameAs(passwordFieldBefore),
            "the repair-only path must not re-convert/replace a field that is already TMP_InputField.");
        Assert.That(
            passwordFieldAfter.contentType,
            Is.EqualTo(TMP_InputField.ContentType.Password),
            "a second migration run must repair drifted password ContentType, not just no-op on field counts.");
    }

    [Test]
    public void SecondMigrationRunRemovesAReintroducedStalePersistentOnClickListener()
    {
        Scene scene = OpenScene(CreateNewScenePath);
        AccountManager manager = FindInScene<AccountManager>(scene);
        Assume.That(manager, Is.Not.Null);

        // Simulate drift: a stale persistent onClick reappears (a bad merge, or a manual Inspector
        // re-add) pointed at AccountManager.createUser - the exact class of listener the original
        // migration already removed once.
        Button createAccountButton = FindButtonNamed(scene, "createUserButton");
        Assume.That(createAccountButton, Is.Not.Null);
        UnityEventTools.AddPersistentListener(createAccountButton.onClick, manager.createUser);
        Assume.That(createAccountButton.onClick.GetPersistentEventCount(), Is.GreaterThan(0));

        List<string> errors = InvokeMigrateFieldsScreenInputFieldsInMemory(scene, 5);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));

        Assert.That(
            createAccountButton.onClick.GetPersistentEventCount(),
            Is.EqualTo(0),
            "a second migration run must strip a re-introduced stale persistent onClick listener, not just no-op on field counts.");
    }

    private static List<string> InvokeMigrateFieldsScreenInputFieldsInMemory(Scene scene, int expectedInputFieldCount)
    {
        MethodInfo method = typeof(AccountTextMeshProMigration).GetMethod(
            "MigrateFieldsScreenInputFieldsInMemory", BindingFlags.NonPublic | BindingFlags.Static);
        Assume.That(method, Is.Not.Null, "expected a private static method named 'MigrateFieldsScreenInputFieldsInMemory'.");
        return (List<string>)method.Invoke(null, new object[] { scene, "Test", expectedInputFieldCount });
    }

    // ---------------------------------------------------------------------------------------------
    // 26: permanent contract / idempotency (mirrors CreditsTextMeshProMigrationTests: the contract
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

    /// <summary>
    /// Mirrors <c>CreditsTextMeshProMigrationTests.RepeatedRegisterCallsDoNotAccumulateSubmitListeners</c>'
    /// scratch EventSystem setup: <see cref="EventSystem.current"/> is only settable by
    /// <see cref="EventSystem.OnEnable"/> registering into its internal list, which does not fire on a
    /// freshly-constructed GameObject in this project's batchmode EditMode test harness, so it must be
    /// invoked directly via reflection.
    /// </summary>
    private EventSystem InstallScratchEventSystem()
    {
        GameObject eventSystemObject = new GameObject("TestEventSystem", typeof(EventSystem));
        _instantiated.Add(eventSystemObject);
        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();

        typeof(EventSystem)
            .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(eventSystem, null);

        // The scene just opened by this test carries its own (never-enabled, since scene loading
        // outside Play Mode does not run MonoBehaviour lifecycle) EventSystem GameObject, and an
        // earlier test in this same run may also have left a stale entry in EventSystem's internal
        // static registry. Force selection onto this scratch instance explicitly rather than assuming
        // it is already current - the setter only reorders an EventSystem already present in that
        // registry (which the OnEnable call above just added it to), so this is safe.
        EventSystem.current = eventSystem;
        Assume.That(EventSystem.current, Is.SameAs(eventSystem));
        return eventSystem;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(method, Is.Not.Null, "expected a private method named '" + methodName + "'.");
        method.Invoke(target, null);
    }

    private static void InvokePublic(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
        Assume.That(method, Is.Not.Null, "expected a public parameterless method named '" + methodName + "'.");
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(field, Is.Not.Null, "expected a private field named '" + fieldName + "'.");
        field.SetValue(target, value);
    }

    private static TValue GetPrivateField<TValue>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(field, Is.Not.Null, "expected a private field named '" + fieldName + "'.");
        return (TValue)field.GetValue(target);
    }
}
