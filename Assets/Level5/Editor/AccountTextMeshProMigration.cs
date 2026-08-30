using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 5A migrated the directly scene-owned ordinary legacy <see cref="Text"/> in the three
/// production account scenes that have any (<c>level_00_account</c>, <c>level_00_account_createNew</c>,
/// <c>level_00_account_loginExisting</c>) to <see cref="TextMeshProUGUI"/> on the same project-owned Neon
/// Pixel-7 SDF font asset every other menu screen uses. AUD-092 Phase 5B (this class, since) additionally
/// migrates each screen's legacy <see cref="InputField"/> components themselves, and their structural
/// <c>textComponent</c>/<c>placeholder</c> Text dependencies, to <see cref="TMP_InputField"/>/
/// <see cref="TextMeshProUGUI"/>. <c>level_00_account_loginLocal</c> has no legacy Text/InputField at all
/// and is untouched by either phase.
///
/// Unlike every earlier AUD-092 phase, these three screens are NOT prefab instances - characterization
/// (<c>AccountTextMeshProMigration.Report</c>, run once against current <c>dev</c> before this file was
/// written) confirmed each screen's entire hierarchy, including its <c>touch_joystick</c> "next" button,
/// is directly scene-authored (zero nested prefab instance Text in any of the three - the account screens
/// predate the shared <c>touch_joystick.prefab</c> extraction other menu screens use; left as-is, out of
/// scope for this text migration). Migration therefore mutates the scene file directly rather than a
/// prefab asset, via the transaction-safety wrapper in <see cref="RunSceneMigration"/>: open the target
/// scene alone, migrate in memory, validate, and save only on success, always restoring whatever scene
/// setup was open before this ran.
///
/// Reuses every low-level mechanic <see cref="MenuTextConversion"/> already proved (font asset creation,
/// single-Text conversion, named-field wiring, unsupported-consumer detection, and - Phase 5B -
/// <see cref="MenuTextConversion.ConvertInputField"/>, the same InputField mutation mechanics
/// <c>CreditsTextMeshProMigration</c>'s Phase 4B proved) - this class contributes only the
/// account-specific orchestration: the multi-InputField structural boundary (each screen has several
/// InputFields, not creditsManager.prefab's one), the password ContentType fix, the terminal
/// Create Account/Login button rewiring, the obsolete persistent-listener removal, and the
/// <c>ServerMessagesManager.serverMessagesText</c> list rewiring the hub screen's nested
/// <c>ServerMessages</c> prefab instance requires.
/// </summary>
internal static class AccountTextMeshProMigration
{
    internal const string HubScenePath = "Assets/Scenes/level_00_account.unity";
    internal const string CreateNewScenePath = "Assets/Scenes/level_00_account_createNew.unity";
    internal const string LoginExistingScenePath = "Assets/Scenes/level_00_account_loginExisting.unity";
    internal const string LoginLocalScenePath = "Assets/Scenes/level_00_account_loginLocal.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private const string ServerMessagesTextFieldName = "serverMessagesText";
    private static readonly string[] ServerMessageTextObjectNames =
    {
        "messageText", "messageText.1", "messageText.2", "messageText.3", "messageText.4",
    };

    private const int HubExpectedOrdinaryCount = 22;
    private const int CreateNewExpectedInputFieldCount = 5;
    private const int CreateNewExpectedOrdinaryCount = 12;
    private const int CreateNewExpectedTotalTmpTextCount = 22;
    private const int LoginExistingExpectedInputFieldCount = 2;
    private const int LoginExistingExpectedOrdinaryCount = 8;
    private const int LoginExistingExpectedTotalTmpTextCount = 12;

    /// <summary>
    /// Maps each legacy InputField's GameObject name (the convention <c>MenuUiObjectsWiring</c> already
    /// wires by) to the matching field name on <see cref="AccountCreateUiObjects"/>/
    /// <see cref="AccountLoginUiObjects"/>. The login screen only owns two of these five - lookups simply
    /// skip whichever names are absent.
    /// </summary>
    private static readonly (string GameObjectName, string FieldName)[] FieldRoleMap =
    {
        ("EmailInputField", "emailInputField"),
        ("UserNameInputField", "usernameInputField"),
        ("PasswordInputField", "passwordInputField"),
        ("FirstNameInputField", "firstNameInputField"),
        ("LastNameInputField", "lastNameInputField"),
    };

    /// <summary>
    /// AUD-092 Phase 5B section 12: Check Email/Check Username/Create Account/Login become entirely
    /// code-owned (<c>AccountManager.RegisterButtonCallbacks</c>). The persistent scene onClick calls
    /// that used to invoke them directly - several serialized under the stale pre-rename type name
    /// <c>LoginManager, Assembly-CSharp</c> - are removed by target+method rather than renamed, which
    /// also fully resolves the stale-type-name landmine regardless of which type name a given call still
    /// carries.
    /// </summary>
    private static readonly string[] ObsoletePersistentOnClickMethods =
    {
        nameof(AccountManager.checkEmailAddressFormat),
        nameof(AccountManager.checkUserName),
        nameof(AccountManager.createUser),
        nameof(AccountManager.LoginUser),
    };

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Report Account TMP Migration")]
    public static void Report()
    {
        foreach (string scenePath in new[] { HubScenePath, CreateNewScenePath, LoginExistingScenePath, LoginLocalScenePath })
        {
            ReportScene(scenePath);
        }
    }

    private static void ReportScene(string scenePath)
    {
        WithOpenScene(scenePath, scene =>
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("==== " + scenePath + " ====");

            List<Text> owned = new List<Text>();
            List<Text> nested = new List<Text>();
            PartitionOwnedTexts(scene, owned, nested);
            summary.AppendLine("owned Text: " + owned.Count + ", nested-prefab Text: " + nested.Count);

            List<InputField> ownedInputFields = FindOwnedInputFields(scene);
            summary.AppendLine("owned InputField: " + ownedInputFields.Count);

            List<string> boundaryErrors = new List<string>();
            HashSet<Text> protectedTexts = ResolveProtectedInputFieldTexts(ownedInputFields, boundaryErrors, ownedInputFields.Count);
            foreach (string error in boundaryErrors)
            {
                summary.AppendLine("  INPUTFIELD BOUNDARY ERROR: " + error);
            }

            summary.AppendLine("protected structural Text: " + (protectedTexts != null ? protectedTexts.Count.ToString() : "UNRESOLVED"));

            foreach (Text text in owned)
            {
                bool isProtected = protectedTexts != null && protectedTexts.Contains(text);
                summary.AppendLine(
                    "  " + MenuTextConversion.BuildHierarchyPath(text.gameObject, null)
                        + (isProtected ? " [PROTECTED]" : " [ordinary candidate]")
                        + " text=\"" + Truncate(text.text, 40) + "\"");
            }

            HashSet<Object> textSet = new HashSet<Object>(owned);
            List<string> unsupported = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MenuTextConversion.CollectUnsupportedConsumers(root, textSet, unsupported);
            }

            foreach (string finding in unsupported)
            {
                summary.AppendLine("  UNSUPPORTED CONSUMER: " + finding);
            }

            Debug.Log(summary.ToString());
            return true;
        });
    }

    // ---------------------------------------------------------------------------------------------
    // Migration entry points
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Migrate Account Hub To TMP")]
    public static void MigrateHub()
    {
        RunSceneMigration(HubScenePath, "AccountTextMeshProMigration.MigrateHub", MigrateHubInMemory);
    }

    [MenuItem("Level5/Migrate Account Create To TMP")]
    public static void MigrateCreateNew()
    {
        RunSceneMigration(CreateNewScenePath, "AccountTextMeshProMigration.MigrateCreateNew", scene =>
            MigrateFieldsScreenInMemory(
                scene, "AccountTextMeshProMigration.MigrateCreateNew", CreateNewExpectedInputFieldCount, CreateNewExpectedOrdinaryCount));
    }

    [MenuItem("Level5/Migrate Account Login To TMP")]
    public static void MigrateLoginExisting()
    {
        RunSceneMigration(LoginExistingScenePath, "AccountTextMeshProMigration.MigrateLoginExisting", scene =>
            MigrateFieldsScreenInMemory(
                scene, "AccountTextMeshProMigration.MigrateLoginExisting", LoginExistingExpectedInputFieldCount, LoginExistingExpectedOrdinaryCount));
    }

    /// <summary>Runs all three scene migrations under a single captured/restored editor scene setup.</summary>
    [MenuItem("Level5/Migrate All Account TMP")]
    public static void MigrateAll()
    {
        const string LogPrefix = "AccountTextMeshProMigration.MigrateAll";

        if (!CaptureEditorStateForMigration(LogPrefix, out SceneSetup[] priorSetup, out List<string> stateErrors))
        {
            LogAbort(LogPrefix, stateErrors);
            return;
        }

        try
        {
            RunSceneMigrationNoRestore(HubScenePath, "AccountTextMeshProMigration.MigrateHub", MigrateHubInMemory);
            RunSceneMigrationNoRestore(CreateNewScenePath, "AccountTextMeshProMigration.MigrateCreateNew", scene =>
                MigrateFieldsScreenInMemory(
                    scene, "AccountTextMeshProMigration.MigrateCreateNew", CreateNewExpectedInputFieldCount, CreateNewExpectedOrdinaryCount));
            RunSceneMigrationNoRestore(LoginExistingScenePath, "AccountTextMeshProMigration.MigrateLoginExisting", scene =>
                MigrateFieldsScreenInMemory(
                    scene, "AccountTextMeshProMigration.MigrateLoginExisting", LoginExistingExpectedInputFieldCount, LoginExistingExpectedOrdinaryCount));
        }
        finally
        {
            RestoreEditorStateAfterMigration(LogPrefix, priorSetup);
        }

        Debug.Log(LogPrefix + " complete.");
    }

    // ---------------------------------------------------------------------------------------------
    // Hub (level_00_account): no InputField at all; must rewire ServerMessagesManager.serverMessagesText
    // ---------------------------------------------------------------------------------------------

    private static List<string> MigrateHubInMemory(Scene scene)
    {
        List<string> errors = new List<string>();

        List<Text> owned = new List<Text>();
        List<Text> nested = new List<Text>();
        PartitionOwnedTexts(scene, owned, nested);

        List<InputField> ownedInputFields = FindOwnedInputFields(scene);
        if (ownedInputFields.Count != 0)
        {
            errors.Add(HubScenePath + " : expected 0 owned InputField, found " + ownedInputFields.Count + ".");
            return errors;
        }

        if (owned.Count == 0 && HasAnyTmp(scene))
        {
            Debug.Log("AccountTextMeshProMigration.MigrateHub: no directly-owned legacy Text remains; nothing to do.");
            return errors; // idempotent no-op
        }

        if (owned.Count != HubExpectedOrdinaryCount)
        {
            errors.Add(HubScenePath + " : expected " + HubExpectedOrdinaryCount + " owned Text, found " + owned.Count + ".");
            return errors;
        }

        MonoBehaviour serverMessagesManager = FindSingleComponent<ServerMessagesManager>(scene, errors, HubScenePath);
        if (serverMessagesManager == null)
        {
            return errors;
        }

        // Every owned Text lives under this one hierarchy - resolved explicitly (rather than reading it
        // off an arbitrary Text's own transform.root) so every conversion below shares exactly the same
        // Selectable-rebinding scope, matching MigrateFieldsScreenInMemory's single-shared-root approach.
        AccountHubUiObjects hubUi = FindSingleComponent<AccountHubUiObjects>(scene, errors, HubScenePath);
        if (hubUi == null)
        {
            return errors;
        }

        GameObject scopeRoot = hubUi.transform.root.gameObject;

        // Resolve the 5 targets by GameObject NAME, not by reading serverMessagesText's current
        // serialized value - ServerMessagesManager.serverMessagesText was retyped from List<Text> to
        // List<TMP_Text> in the same change that added this migration, so by the time this runs against
        // the recompiled assembly, the scene's still-unmigrated List<Text> override values type-mismatch
        // the new field and read back null/empty. Name lookup is unaffected by that recompile ordering.
        Dictionary<string, Text> ownedByName = new Dictionary<string, Text>();
        foreach (Text text in owned)
        {
            ownedByName[text.gameObject.name] = text;
        }

        List<Text> serverMessageTexts = new List<Text>();
        foreach (string name in ServerMessageTextObjectNames)
        {
            if (!ownedByName.TryGetValue(name, out Text text))
            {
                errors.Add(HubScenePath + " : could not find an owned Text GameObject named '" + name + "' for ServerMessagesManager.serverMessagesText.");
                continue;
            }

            serverMessageTexts.Add(text);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        HashSet<Object> knownConsumed = new HashSet<Object>(serverMessageTexts);
        if (HasUnsupportedConsumers(scene, owned, knownConsumed, errors))
        {
            return errors;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            errors.Add("could not create/load the Neon Pixel-7 SDF font asset.");
            return errors;
        }

        // Capture the GameObjects that must survive the Text -> TextMeshProUGUI swap for later lookup;
        // the GameObject identity is stable across the swap, the Text component reference is not.
        List<GameObject> serverMessageObjects = serverMessageTexts.ConvertAll(t => t.gameObject);

        foreach (Text text in owned)
        {
            TextMeshProUGUI tmp = MenuTextConversion.ConvertSingleText(scopeRoot, text, font);
            if (tmp == null)
            {
                errors.Add(MenuTextConversion.BuildHierarchyPath(text.gameObject, null) + " : conversion failed to add TextMeshProUGUI.");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        SerializedObject serializedManager = new SerializedObject(serverMessagesManager);
        SerializedProperty arrayProperty = serializedManager.FindProperty(ServerMessagesTextFieldName);
        if (arrayProperty == null)
        {
            errors.Add("ServerMessagesManager has no field named '" + ServerMessagesTextFieldName + "'.");
            return errors;
        }

        arrayProperty.arraySize = serverMessageObjects.Count;
        for (int i = 0; i < serverMessageObjects.Count; i++)
        {
            TextMeshProUGUI tmp = serverMessageObjects[i].GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                errors.Add(serverMessageObjects[i].name + " has no TextMeshProUGUI after conversion.");
                return errors;
            }

            arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = tmp;
        }

        serializedManager.ApplyModifiedProperties();

        errors.AddRange(CollectNullTargetGraphicErrors(scene));
        return errors;
    }

    // ---------------------------------------------------------------------------------------------
    // Create/Login screens: N InputFields protect 2N structural Text; a single named messageDisplay
    // ordinary Text is rewired into AccountCreateUiObjects/AccountLoginUiObjects. AUD-092 Phase 5B adds
    // a second sub-pass, run immediately after, that converts the InputFields themselves - see
    // MigrateFieldsScreenInputFieldsInMemory below. Splitting the two passes (rather than one long
    // method) is what lets the ordinary-text pass stay idempotent once the InputField pass has already
    // run: on a second migration run the InputFields are already gone, so there is no protected-text set
    // left to resolve, and the ordinary-text pass below must tolerate that instead of erroring.
    // ---------------------------------------------------------------------------------------------

    private static List<string> MigrateFieldsScreenInMemory(Scene scene, string logPrefix, int expectedInputFieldCount, int expectedOrdinaryCount)
    {
        List<string> errors = MigrateFieldsScreenOrdinaryTextInMemory(scene, logPrefix, expectedInputFieldCount, expectedOrdinaryCount);
        if (errors.Count > 0)
        {
            return errors;
        }

        return MigrateFieldsScreenInputFieldsInMemory(scene, logPrefix, expectedInputFieldCount);
    }

    private static List<string> MigrateFieldsScreenOrdinaryTextInMemory(Scene scene, string logPrefix, int expectedInputFieldCount, int expectedOrdinaryCount)
    {
        List<string> errors = new List<string>();

        List<Text> owned = new List<Text>();
        List<Text> nested = new List<Text>();
        PartitionOwnedTexts(scene, owned, nested);

        List<InputField> ownedInputFields = FindOwnedInputFields(scene);
        List<Text> eligible;
        if (ownedInputFields.Count == 0)
        {
            // The InputFields themselves (if any existed) have already been converted by
            // MigrateFieldsScreenInputFieldsInMemory - every remaining owned legacy Text, if any, is
            // therefore ordinary; there is no protected structural set left to resolve/exclude.
            eligible = owned;
        }
        else
        {
            if (ownedInputFields.Count != expectedInputFieldCount)
            {
                errors.Add(scene.path + " : expected " + expectedInputFieldCount + " owned InputField, found " + ownedInputFields.Count + ".");
                return errors;
            }

            HashSet<Text> protectedTexts = ResolveProtectedInputFieldTexts(ownedInputFields, errors, expectedInputFieldCount);
            if (protectedTexts == null)
            {
                return errors;
            }

            eligible = owned.FindAll(text => !protectedTexts.Contains(text));
        }

        if (eligible.Count == 0 && HasAnyTmp(scene))
        {
            Debug.Log(logPrefix + ": no directly-owned eligible legacy Text remains in " + scene.path + "; nothing to do.");
            return errors; // idempotent no-op
        }

        if (eligible.Count != expectedOrdinaryCount)
        {
            errors.Add(scene.path + " : expected " + expectedOrdinaryCount + " ordinary Text, found " + eligible.Count + ".");
            return errors;
        }

        Component messageDisplayHost = FindMessageDisplayHost(scene, errors);
        if (messageDisplayHost == null)
        {
            return errors;
        }

        bool foundMessageDisplayText = false;
        foreach (Text text in eligible)
        {
            if (text.gameObject.name == "messageDisplay")
            {
                foundMessageDisplayText = true;
                break;
            }
        }

        if (!foundMessageDisplayText)
        {
            errors.Add(scene.path + " : could not find an owned ordinary Text GameObject named 'messageDisplay'.");
            return errors;
        }

        HashSet<Object> knownConsumed = new HashSet<Object>();
        foreach (Text text in eligible)
        {
            if (text.gameObject.name == "messageDisplay")
            {
                knownConsumed.Add(text);
            }
        }

        if (HasUnsupportedConsumers(scene, eligible, knownConsumed, errors))
        {
            return errors;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            errors.Add("could not create/load the Neon Pixel-7 SDF font asset.");
            return errors;
        }

        bool wired = MenuTextConversion.ConvertOwnedTextsAndWireNamedFields(
            messageDisplayHost.transform.root.gameObject,
            eligible,
            font,
            new (string GameObjectName, string FieldName)[] { ("messageDisplay", "messageDisplay") },
            messageDisplayHost,
            checkSelectableTargetGraphics: false,
            out _,
            out _,
            errors);

        if (!wired)
        {
            return errors;
        }

        errors.AddRange(CollectNullTargetGraphicErrors(scene));
        return errors;
    }

    /// <summary>
    /// AUD-092 Phase 5B: converts every owned legacy InputField on this screen to TMP_InputField (see
    /// <see cref="MenuTextConversion.ConvertInputField"/> for the shared single-field mechanics), rewires
    /// the resulting TMP_InputFields plus the existing terminal action button (createUserButton on
    /// create, loginButton on login) into AccountCreateUiObjects/AccountLoginUiObjects's now-TMP-typed
    /// serialized fields, applies the password ContentType fix, and strips every obsolete persistent
    /// onClick listener (Check Email/Check Username/Create Account/Login - now code-owned by
    /// AccountManager).
    ///
    /// Idempotent, but not merely a count-based no-op: once no directly-owned legacy InputField remains
    /// and the expected count of TMP_InputField already does, there is nothing left to CONVERT, but the
    /// terminal-button wiring, password ContentType, and obsolete-listener removal below still run
    /// against the fields that already exist. A field-count-only no-op would leave independent drift in
    /// any of those three (a stale onClick reappearing via a manual Inspector edit or a bad merge, say)
    /// unrepaired by a second migration run - this keeps re-running the migration a genuine self-healing
    /// pass, matching what CollectFieldsScreenContractErrors actually requires.
    /// </summary>
    private static List<string> MigrateFieldsScreenInputFieldsInMemory(Scene scene, string logPrefix, int expectedInputFieldCount)
    {
        List<string> errors = new List<string>();

        List<InputField> ownedInputFields = FindOwnedInputFields(scene);
        List<TMP_InputField> ownedTmpInputFieldsBefore = FindOwnedTmpInputFields(scene);
        bool alreadyConverted = ownedInputFields.Count == 0 && ownedTmpInputFieldsBefore.Count == expectedInputFieldCount;

        if (!alreadyConverted && ownedInputFields.Count != expectedInputFieldCount)
        {
            errors.Add(scene.path + " : expected " + expectedInputFieldCount + " owned legacy InputField to migrate, found " + ownedInputFields.Count + ".");
            return errors;
        }

        Component uiHost = FindMessageDisplayHost(scene, errors);
        if (uiHost == null)
        {
            return errors;
        }

        GameObject scopeRoot = uiHost.transform.root.gameObject;
        bool isCreateScreen = uiHost is AccountCreateUiObjects;
        string terminalButtonFieldName = isCreateScreen ? "createAccountButton" : "loginButton";
        string terminalButtonGameObjectName = isCreateScreen ? "createUserButton" : "loginButton";

        Button terminalButton = FindButtonNamedInScene(scene, terminalButtonGameObjectName);
        if (terminalButton == null)
        {
            errors.Add(scene.path + " : could not find the terminal '" + terminalButtonGameObjectName + "' Button.");
            return errors;
        }

        Dictionary<string, TMP_InputField> convertedByFieldName;
        if (alreadyConverted)
        {
            Debug.Log(logPrefix + ": no directly-owned legacy InputField remains in " + scene.path + "; re-verifying terminal button/password/listener state.");
            convertedByFieldName = MapOwnedTmpInputFieldsByFieldName(ownedTmpInputFieldsBefore);
        }
        else
        {
            TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
            if (font == null)
            {
                errors.Add("could not create/load the Neon Pixel-7 SDF font asset.");
                return errors;
            }

            HashSet<Text> protectedTexts = ResolveProtectedInputFieldTexts(ownedInputFields, errors, expectedInputFieldCount);
            if (protectedTexts == null)
            {
                return errors;
            }

            Dictionary<string, InputField> byGameObjectName = new Dictionary<string, InputField>();
            foreach (InputField field in ownedInputFields)
            {
                byGameObjectName[field.gameObject.name] = field;
            }

            // Convert every field this screen owns (a subset of FieldRoleMap - the login screen only has
            // username/password), preserving role identity by GameObject name so the *UiObjects rewiring
            // and the password-specific fix below can target the right one without re-deriving it from
            // geometry or declaration order.
            Dictionary<string, TMP_InputField> converted = new Dictionary<string, TMP_InputField>();
            foreach ((string gameObjectName, string fieldName) in FieldRoleMap)
            {
                if (!byGameObjectName.TryGetValue(gameObjectName, out InputField legacyField))
                {
                    continue;
                }

                TMP_InputField tmpField = MenuTextConversion.ConvertInputField(scopeRoot, legacyField, font, errors);
                if (tmpField == null)
                {
                    return errors; // this field's legacy component is already destroyed - the scene must not be saved on this path
                }

                converted[fieldName] = tmpField;
            }

            convertedByFieldName = converted;
        }

        if (convertedByFieldName.Count != expectedInputFieldCount)
        {
            errors.Add(
                scene.path + " : resolved " + convertedByFieldName.Count + " field(s), expected " + expectedInputFieldCount
                    + " (an owned InputField's GameObject name did not match any entry in FieldRoleMap).");
            return errors;
        }

        SerializedObject serializedUi = new SerializedObject(uiHost);
        foreach (KeyValuePair<string, TMP_InputField> pair in convertedByFieldName)
        {
            SerializedProperty property = serializedUi.FindProperty(pair.Key);
            if (property == null)
            {
                errors.Add(uiHost.GetType().Name + " has no field named '" + pair.Key + "'.");
                return errors;
            }

            property.objectReferenceValue = pair.Value;
        }

        SerializedProperty terminalButtonProperty = serializedUi.FindProperty(terminalButtonFieldName);
        if (terminalButtonProperty == null)
        {
            errors.Add(uiHost.GetType().Name + " has no field named '" + terminalButtonFieldName + "'.");
            return errors;
        }

        terminalButtonProperty.objectReferenceValue = terminalButton;
        serializedUi.ApplyModifiedProperties();

        // AUD-092 Phase 5B section 8: both password fields are authored as ordinary Standard inputs
        // today - a real defect, not a preserved behavior. On first conversion this overrides the
        // unmasked Standard contentType CapturedInputFieldState.ApplyTo otherwise carries straight over
        // from the legacy field; applied unconditionally (not only when alreadyConverted is false) so a
        // later run also repairs it if something ever reset it back to Standard. Identified through the
        // typed UiObjects reference resolved above, not by re-deriving it from hierarchy names.
        if (convertedByFieldName.TryGetValue("passwordInputField", out TMP_InputField passwordField))
        {
            passwordField.contentType = TMP_InputField.ContentType.Password;
            passwordField.lineType = TMP_InputField.LineType.SingleLine;
            passwordField.richText = false;
        }

        List<string> managerErrors = new List<string>();
        AccountManager accountManager = FindSingleComponent<AccountManager>(scene, managerErrors, scene.path);
        if (accountManager == null)
        {
            errors.AddRange(managerErrors);
            return errors;
        }

        RemoveObsoletePersistentOnClickListeners(scene, accountManager);

        errors.AddRange(CollectNullTargetGraphicErrors(scene));
        return errors;
    }

    /// <summary>
    /// Maps already-converted TMP_InputFields to their <see cref="AccountCreateUiObjects"/>/
    /// <see cref="AccountLoginUiObjects"/> field name via <see cref="FieldRoleMap"/>, the same convention
    /// <see cref="MigrateFieldsScreenInputFieldsInMemory"/> uses when actually converting - used on the
    /// "nothing left to convert" repair path so it can still rewire/re-verify against the existing
    /// fields with identical role resolution.
    /// </summary>
    private static Dictionary<string, TMP_InputField> MapOwnedTmpInputFieldsByFieldName(List<TMP_InputField> ownedTmpInputFields)
    {
        Dictionary<string, TMP_InputField> byGameObjectName = new Dictionary<string, TMP_InputField>();
        foreach (TMP_InputField field in ownedTmpInputFields)
        {
            byGameObjectName[field.gameObject.name] = field;
        }

        Dictionary<string, TMP_InputField> byFieldName = new Dictionary<string, TMP_InputField>();
        foreach ((string gameObjectName, string fieldName) in FieldRoleMap)
        {
            if (byGameObjectName.TryGetValue(gameObjectName, out TMP_InputField field))
            {
                byFieldName[fieldName] = field;
            }
        }

        return byFieldName;
    }

    private static List<TMP_InputField> FindOwnedTmpInputFields(Scene scene)
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

    private static Button FindButtonNamedInScene(Scene scene, string gameObjectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == gameObjectName && PrefabUtility.GetNearestPrefabInstanceRoot(button.gameObject) == null)
                {
                    return button;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// AUD-092 Phase 5B section 9/12: removes every persistent onClick call on any owned Button in
    /// <paramref name="scene"/> that targets <paramref name="accountManager"/> with a method name in
    /// <see cref="ObsoletePersistentOnClickMethods"/> - Check Email/Check Username/Create Account/Login
    /// are now code-owned (<c>AccountManager.RegisterButtonCallbacks</c>), so the scene-authored calls
    /// that used to invoke them directly (regardless of which serialized type name a given call still
    /// carries) would otherwise fire a second time alongside the code-owned handler. Idempotent: a
    /// second run finds nothing left to remove. The obsolete per-keystroke onValueChanged
    /// (<c>read*Input</c>) listeners need no equivalent sweep here - they lived on the legacy InputField
    /// components themselves and were already destroyed along with them by
    /// <see cref="MenuTextConversion.ConvertInputField"/>.
    /// </summary>
    private static void RemoveObsoletePersistentOnClickListeners(Scene scene, AccountManager accountManager)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (button.onClick.GetPersistentTarget(i) == accountManager
                        && Array.IndexOf(ObsoletePersistentOnClickMethods, button.onClick.GetPersistentMethodName(i)) >= 0)
                    {
                        UnityEventTools.RemovePersistentListener(button.onClick, i);
                    }
                }
            }
        }
    }

    private static Component FindMessageDisplayHost(Scene scene, List<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            AccountCreateUiObjects createUi = root.GetComponentInChildren<AccountCreateUiObjects>(true);
            if (createUi != null)
            {
                return createUi;
            }

            AccountLoginUiObjects loginUi = root.GetComponentInChildren<AccountLoginUiObjects>(true);
            if (loginUi != null)
            {
                return loginUi;
            }
        }

        errors.Add(scene.path + " : no AccountCreateUiObjects/AccountLoginUiObjects found to rewire messageDisplay.");
        return null;
    }

    // ---------------------------------------------------------------------------------------------
    // InputField boundary resolution (shared by both field screens)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves the exact set of legacy Text components every owned <see cref="InputField"/> requires to
    /// remain legacy - its <c>textComponent</c> and its <c>placeholder</c> - by reading the actual object
    /// references off each InputField component, never by hierarchy-name assumption. Returns null (and
    /// logs every problem found) unless every owned InputField has a non-null <c>textComponent</c>, a
    /// non-null Text <c>placeholder</c> distinct from it, AND no Text is claimed by more than one
    /// InputField. <paramref name="expectedCount"/> is trusted as already validated against
    /// <paramref name="ownedInputFields"/> by every caller - used here only to size the final distinct-count
    /// assertion below, not re-checked against <see cref="List{T}.Count"/> again.
    /// </summary>
    private static HashSet<Text> ResolveProtectedInputFieldTexts(List<InputField> ownedInputFields, List<string> errors, int expectedCount)
    {
        HashSet<Text> protectedTexts = new HashSet<Text>();
        foreach (InputField inputField in ownedInputFields)
        {
            string where = MenuTextConversion.BuildHierarchyPath(inputField.gameObject, null);
            if (inputField.textComponent == null)
            {
                errors.Add(where + " : InputField.textComponent is null.");
                continue;
            }

            if (inputField.placeholder == null)
            {
                errors.Add(where + " : InputField.placeholder is null.");
                continue;
            }

            if (!(inputField.textComponent is Text textComponentText))
            {
                errors.Add(where + " : InputField.textComponent is a " + inputField.textComponent.GetType().Name + ", not a legacy Text.");
                continue;
            }

            if (!(inputField.placeholder is Text placeholderText))
            {
                errors.Add(where + " : InputField.placeholder is a " + inputField.placeholder.GetType().Name + ", not a legacy Text.");
                continue;
            }

            if (ReferenceEquals(textComponentText, placeholderText))
            {
                errors.Add(where + " : InputField.textComponent and InputField.placeholder must be two distinct Text components.");
                continue;
            }

            if (!protectedTexts.Add(textComponentText))
            {
                errors.Add(where + " : InputField.textComponent is already claimed as another InputField's structural Text.");
            }

            if (!protectedTexts.Add(placeholderText))
            {
                errors.Add(where + " : InputField.placeholder is already claimed as another InputField's structural Text.");
            }
        }

        if (errors.Count > 0)
        {
            return null;
        }

        if (protectedTexts.Count != expectedCount * 2)
        {
            errors.Add("expected " + (expectedCount * 2) + " distinct protected structural Text components, resolved " + protectedTexts.Count + ".");
            return null;
        }

        return protectedTexts;
    }

    // ---------------------------------------------------------------------------------------------
    // Scene-wide helpers
    // ---------------------------------------------------------------------------------------------

    private static void PartitionOwnedTexts(Scene scene, List<Text> owned, List<Text> nested)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) != null)
                {
                    nested.Add(text);
                }
                else
                {
                    owned.Add(text);
                }
            }
        }
    }

    private static List<InputField> FindOwnedInputFields(Scene scene)
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

    private static bool HasAnyTmp(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<TextMeshProUGUI>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static T FindSingleComponent<T>(Scene scene, List<string> errors, string scenePath) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            found.AddRange(root.GetComponentsInChildren<T>(true));
        }

        if (found.Count != 1)
        {
            errors.Add(scenePath + " : expected exactly 1 " + typeof(T).Name + ", found " + found.Count + ".");
            return null;
        }

        return found[0];
    }

    /// <summary>
    /// Shared unsupported-consumer gate for both migration paths: scans every root in <paramref name="scene"/>
    /// for a serialized reference into <paramref name="candidates"/> other than the <paramref name="knownConsumed"/>
    /// subset already handled explicitly elsewhere (messageDisplay / ServerMessagesManager.serverMessagesText,
    /// both rewired right after conversion) - see <see cref="MenuTextConversion.CollectUnsupportedConsumers"/>.
    /// Returns true (with <paramref name="errors"/> populated) if anything unknown still references a Text
    /// about to be destroyed; the caller must abort without converting in that case.
    /// </summary>
    private static bool HasUnsupportedConsumers(Scene scene, IEnumerable<Text> candidates, HashSet<Object> knownConsumed, List<string> errors)
    {
        HashSet<Object> scanSet = new HashSet<Object>(candidates);
        scanSet.ExceptWith(knownConsumed);

        List<string> unsupported = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MenuTextConversion.CollectUnsupportedConsumers(root, scanSet, unsupported);
        }

        if (unsupported.Count > 0)
        {
            errors.AddRange(unsupported);
            return true;
        }

        return false;
    }

    private static List<string> CollectNullTargetGraphicErrors(Scene scene)
    {
        List<string> errors = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(selectable.gameObject) != null)
                {
                    continue;
                }

                if (selectable.targetGraphic == null)
                {
                    errors.Add(MenuTextConversion.BuildHierarchyPath(selectable.gameObject, null) + " : " + selectable.GetType().Name + " has a null targetGraphic after migration.");
                }
            }
        }

        return errors;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, maxLength) + "...";
    }

    // ---------------------------------------------------------------------------------------------
    // Scene transaction safety (AUD-092 Phase 5A section 7): reject unsafe Play Mode state, reject any
    // dirty open scene, capture the prior scene setup, migrate the target scene alone, save only on
    // success, always restore the prior setup.
    // ---------------------------------------------------------------------------------------------

    private static void RunSceneMigration(string scenePath, string logPrefix, Func<Scene, List<string>> migrateInMemory)
    {
        if (!CaptureEditorStateForMigration(logPrefix, out SceneSetup[] priorSetup, out List<string> stateErrors))
        {
            LogAbort(logPrefix, stateErrors);
            return;
        }

        try
        {
            RunSceneMigrationNoRestore(scenePath, logPrefix, migrateInMemory);
        }
        finally
        {
            RestoreEditorStateAfterMigration(logPrefix, priorSetup);
        }
    }

    /// <summary>
    /// Restores whatever scene setup was open before migration ran. A <see cref="SceneSetup"/> entry
    /// with no asset path (an unsaved "Untitled" scene - notably the default scene batchmode/CI opens
    /// when launched without one) cannot be reopened by path and makes
    /// <see cref="EditorSceneManager.RestoreSceneManagerSetup"/> throw; there is nothing meaningful to
    /// restore to in that case (an interactive Editor session with real, previously-saved scenes open
    /// restores normally), so that specific failure is logged rather than left to crash the whole
    /// migration after scenes earlier in the same run already saved successfully.
    /// </summary>
    private static void RestoreEditorStateAfterMigration(string logPrefix, SceneSetup[] priorSetup)
    {
        try
        {
            EditorSceneManager.RestoreSceneManagerSetup(priorSetup);
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning(
                logPrefix + " : could not restore the prior scene setup (" + ex.Message
                    + "). Expected if no real saved scene was open before this ran (e.g. batchmode's default empty scene).");
        }
    }

    /// <summary>Same as <see cref="RunSceneMigration"/> but does not capture/restore scene setup - used by <see cref="MigrateAll"/>, which does that once around all three scenes.</summary>
    private static void RunSceneMigrationNoRestore(string scenePath, string logPrefix, Func<Scene, List<string>> migrateInMemory)
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogError(logPrefix + " aborted - scene file is missing: " + scenePath);
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        List<string> errors = migrateInMemory(scene);
        if (errors.Count > 0)
        {
            LogAbort(logPrefix, errors);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        if (!saved)
        {
            Debug.LogError(logPrefix + " : EditorSceneManager.SaveScene failed for " + scenePath + ".");
            return;
        }

        Debug.Log(logPrefix + " complete for " + scenePath + ".");
    }

    private static bool CaptureEditorStateForMigration(string logPrefix, out SceneSetup[] priorSetup, out List<string> errors)
    {
        errors = new List<string>();
        priorSetup = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("cannot migrate while the Editor is in or entering Play Mode.");
            return false;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isDirty)
            {
                errors.Add("scene '" + loaded.path + "' has unsaved changes; save or discard them before running " + logPrefix + ".");
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        priorSetup = EditorSceneManager.GetSceneManagerSetup();
        return true;
    }

    private static void LogAbort(string logPrefix, List<string> errors)
    {
        Debug.LogError(logPrefix + " aborted without saving - " + errors.Count + " error(s):\n- " + string.Join("\n- ", errors));
    }

    /// <summary>Read-only convenience for <see cref="Report"/>: opens the scene (additively if not already loaded), runs <paramref name="body"/>, then closes it again if this call opened it.</summary>
    private static void WithOpenScene(string scenePath, Func<Scene, bool> body)
    {
        Scene existing = SceneManager.GetSceneByPath(scenePath);
        bool alreadyOpen = existing.IsValid() && existing.isLoaded;
        Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            body(scene);
        }
        finally
        {
            if (!alreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectAccount*TextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    public static List<string> CollectHubContractErrors()
    {
        List<string> errors = new List<string>();
        WithOpenScene(HubScenePath, scene =>
        {
            List<Text> owned = new List<Text>();
            List<Text> nested = new List<Text>();
            PartitionOwnedTexts(scene, owned, nested);
            if (owned.Count > 0)
            {
                errors.Add(HubScenePath + " : " + owned.Count + " legacy Text component(s) remain (expected 0).");
            }

            List<InputField> ownedInputFields = FindOwnedInputFields(scene);
            if (ownedInputFields.Count > 0)
            {
                errors.Add(HubScenePath + " : " + ownedInputFields.Count + " legacy InputField component(s) remain (expected 0).");
            }

            List<string> managerErrors = new List<string>();
            MonoBehaviour manager = FindSingleComponent<ServerMessagesManager>(scene, managerErrors, HubScenePath);
            if (manager == null)
            {
                errors.AddRange(managerErrors);
            }
            else
            {
                SerializedObject serialized = new SerializedObject(manager);
                SerializedProperty arrayProperty = serialized.FindProperty(ServerMessagesTextFieldName);
                if (arrayProperty == null || arrayProperty.arraySize != ServerMessageTextObjectNames.Length)
                {
                    errors.Add(
                        HubScenePath + " : ServerMessagesManager.serverMessagesText expected " + ServerMessageTextObjectNames.Length
                            + " element(s), found " + (arrayProperty != null ? arrayProperty.arraySize.ToString() : "<missing field>") + ".");
                }
                else
                {
                    TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
                    for (int i = 0; i < arrayProperty.arraySize; i++)
                    {
                        Object element = arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (!(element is TextMeshProUGUI tmp))
                        {
                            errors.Add(HubScenePath + " : ServerMessagesManager.serverMessagesText[" + i + "] is not a TextMeshProUGUI.");
                        }
                        else if (neonPixel == null || tmp.font != neonPixel)
                        {
                            errors.Add(HubScenePath + " : ServerMessagesManager.serverMessagesText[" + i + "] does not use the shared Neon Pixel-7 SDF font asset.");
                        }
                    }
                }
            }

            errors.AddRange(CollectNullTargetGraphicErrors(scene));
            return true;
        });

        return errors;
    }

    public static List<string> CollectCreateNewContractErrors()
    {
        return CollectFieldsScreenContractErrors(CreateNewScenePath, CreateNewExpectedInputFieldCount, CreateNewExpectedTotalTmpTextCount);
    }

    public static List<string> CollectLoginExistingContractErrors()
    {
        return CollectFieldsScreenContractErrors(LoginExistingScenePath, LoginExistingExpectedInputFieldCount, LoginExistingExpectedTotalTmpTextCount);
    }

    /// <summary>
    /// AUD-092 Phase 5B permanent regression guard - the final account form-field text-rendering
    /// contract, replacing Phase 5A's temporary legacy-InputField exception. Requires: zero
    /// directly-owned legacy <see cref="Text"/>, zero directly-owned legacy <see cref="InputField"/>,
    /// exactly <paramref name="expectedInputFieldCount"/> directly-owned <see cref="TMP_InputField"/>
    /// (each with a valid masked <c>textViewport</c>, TextMeshProUGUI <c>textComponent</c>/
    /// <c>placeholder</c> on the shared Neon Pixel-7 SDF font, SingleLine, disabled rich text, and a
    /// valid <c>targetGraphic</c>), the password field using <c>ContentType.Password</c>, exactly
    /// <paramref name="expectedTotalTmpTextCount"/> directly-owned TextMeshProUGUI in total, every
    /// AccountCreateUiObjects/AccountLoginUiObjects field (including the terminal Create Account/Login
    /// button) resolved, and no obsolete persistent onClick/onValueChanged listener remaining.
    /// </summary>
    private static List<string> CollectFieldsScreenContractErrors(string scenePath, int expectedInputFieldCount, int expectedTotalTmpTextCount)
    {
        List<string> errors = new List<string>();
        WithOpenScene(scenePath, scene =>
        {
            List<Text> owned = new List<Text>();
            List<Text> nested = new List<Text>();
            PartitionOwnedTexts(scene, owned, nested);
            if (owned.Count > 0)
            {
                errors.Add(scenePath + " : " + owned.Count + " legacy Text component(s) remain (expected 0).");
            }

            List<InputField> ownedLegacyInputFields = FindOwnedInputFields(scene);
            if (ownedLegacyInputFields.Count > 0)
            {
                errors.Add(scenePath + " : " + ownedLegacyInputFields.Count + " legacy InputField component(s) remain (expected 0).");
            }

            List<TMP_InputField> ownedTmpInputFields = FindOwnedTmpInputFields(scene);
            if (ownedTmpInputFields.Count != expectedInputFieldCount)
            {
                errors.Add(scenePath + " : expected exactly " + expectedInputFieldCount + " directly-owned TMP_InputField, found " + ownedTmpInputFields.Count + ".");
            }

            TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
            foreach (TMP_InputField tmp in ownedTmpInputFields)
            {
                string where = scenePath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, null);
                MenuTextConversion.AddTmpInputFieldViewportContractErrors(where, tmp, errors);
                MenuTextConversion.AddTmpInputFieldSubTextContractErrors(where, "textComponent", tmp.textComponent, neonPixel, errors);
                MenuTextConversion.AddTmpInputFieldSubTextContractErrors(where, "placeholder", tmp.placeholder, neonPixel, errors);

                if (tmp.lineType != TMP_InputField.LineType.SingleLine)
                {
                    errors.Add(where + " : lineType is " + tmp.lineType + ", expected SingleLine.");
                }

                if (tmp.richText)
                {
                    errors.Add(where + " : richText is enabled; account input fields must stay plain-text like the legacy InputFields they replaced.");
                }

                if (tmp.targetGraphic == null)
                {
                    errors.Add(where + " : targetGraphic is null.");
                }
            }

            Component uiHost = FindMessageDisplayHost(scene, errors);
            AccountCreateUiObjects createUi = uiHost as AccountCreateUiObjects;
            AccountLoginUiObjects loginUi = uiHost as AccountLoginUiObjects;

            TMP_InputField passwordField = null;
            if (createUi != null)
            {
                List<string> missing = new List<string>();
                createUi.Validate(missing);
                foreach (string field in missing)
                {
                    errors.Add(scenePath + " : " + field + " is not resolved.");
                }

                passwordField = createUi.PasswordInputField;

                SerializedObject serialized = new SerializedObject(createUi);
                SerializedProperty property = serialized.FindProperty("messageDisplay");
                if (property == null || !(property.objectReferenceValue is TextMeshProUGUI tmp) || neonPixel == null || tmp.font != neonPixel)
                {
                    errors.Add(scenePath + " : messageDisplay is not a TextMeshProUGUI on the shared Neon Pixel-7 SDF font asset.");
                }
            }
            else if (loginUi != null)
            {
                List<string> missing = new List<string>();
                loginUi.Validate(missing);
                foreach (string field in missing)
                {
                    errors.Add(scenePath + " : " + field + " is not resolved.");
                }

                passwordField = loginUi.PasswordInputField;

                SerializedObject serialized = new SerializedObject(loginUi);
                SerializedProperty property = serialized.FindProperty("messageDisplay");
                if (property == null || !(property.objectReferenceValue is TextMeshProUGUI tmp) || neonPixel == null || tmp.font != neonPixel)
                {
                    errors.Add(scenePath + " : messageDisplay is not a TextMeshProUGUI on the shared Neon Pixel-7 SDF font asset.");
                }
            }

            if (passwordField == null)
            {
                errors.Add(scenePath + " : could not resolve the password TMP_InputField to verify its ContentType.");
            }
            else if (passwordField.contentType != TMP_InputField.ContentType.Password)
            {
                errors.Add(
                    scenePath + " -> " + MenuTextConversion.BuildHierarchyPath(passwordField.gameObject, null)
                        + " : password field contentType is " + passwordField.contentType + ", expected Password.");
            }

            List<TextMeshProUGUI> ownedTmpTexts = new List<TextMeshProUGUI>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TextMeshProUGUI candidate in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(candidate.gameObject) == null)
                    {
                        ownedTmpTexts.Add(candidate);
                    }
                }
            }

            if (ownedTmpTexts.Count != expectedTotalTmpTextCount)
            {
                errors.Add(
                    scenePath + " : expected exactly " + expectedTotalTmpTextCount + " directly-owned TextMeshProUGUI component(s), found "
                        + ownedTmpTexts.Count + ".");
            }

            List<string> managerErrors = new List<string>();
            AccountManager accountManager = FindSingleComponent<AccountManager>(scene, managerErrors, scenePath);
            if (accountManager == null)
            {
                errors.AddRange(managerErrors);
            }
            else
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Button button in root.GetComponentsInChildren<Button>(true))
                    {
                        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        {
                            if (button.onClick.GetPersistentTarget(i) == accountManager
                                && Array.IndexOf(ObsoletePersistentOnClickMethods, button.onClick.GetPersistentMethodName(i)) >= 0)
                            {
                                errors.Add(
                                    scenePath + " -> " + MenuTextConversion.BuildHierarchyPath(button.gameObject, null)
                                        + " : stale persistent onClick -> AccountManager." + button.onClick.GetPersistentMethodName(i)
                                        + " remains (must be code-owned).");
                            }
                        }
                    }

                    foreach (TMP_InputField field in root.GetComponentsInChildren<TMP_InputField>(true))
                    {
                        if (field.onValueChanged.GetPersistentEventCount() > 0)
                        {
                            errors.Add(
                                scenePath + " -> " + MenuTextConversion.BuildHierarchyPath(field.gameObject, null)
                                    + " : stale persistent onValueChanged listener remains (obsolete read*Input wiring must be gone).");
                        }
                    }
                }
            }

            errors.AddRange(CollectNullTargetGraphicErrors(scene));
            return true;
        });

        return errors;
    }

    public static List<string> CollectLoginLocalContractErrors()
    {
        List<string> errors = new List<string>();
        WithOpenScene(LoginLocalScenePath, scene =>
        {
            List<Text> owned = new List<Text>();
            List<Text> nested = new List<Text>();
            PartitionOwnedTexts(scene, owned, nested);
            if (owned.Count > 0)
            {
                errors.Add(LoginLocalScenePath + " : expected 0 owned legacy Text, found " + owned.Count + " - AUD-092 Phase 5A must not touch this scene.");
            }

            List<InputField> ownedInputFields = FindOwnedInputFields(scene);
            if (ownedInputFields.Count > 0)
            {
                errors.Add(LoginLocalScenePath + " : expected 0 owned legacy InputField, found " + ownedInputFields.Count + ".");
            }

            return true;
        });

        return errors;
    }
}
