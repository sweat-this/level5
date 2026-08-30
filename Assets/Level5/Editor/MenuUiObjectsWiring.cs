using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-off migration utility for the serialized-menu-UI-references issue (AUD-103 rest, AUD-104):
/// adds the new <c>*UiObjects</c>/<see cref="MenuFooterUiObjects"/> view components to the menu
/// prefabs/scenes that need them, and wires each field to the object the corresponding manager
/// currently resolves by name (or, where the manager's own field is already assigned in the asset,
/// copies that existing reference instead of re-deriving it by name).
///
/// Idempotent: re-running it finds the already-added component via <see cref="AddOrGet{T}"/> and
/// simply re-wires it, so a partial or repeated run is safe. Kept in the repository afterward as a
/// record of how the wiring was produced, matching <see cref="MenuSceneCleanup"/>.
/// </summary>
public static class MenuUiObjectsWiring
{
    private const string OptionManagerPrefabPath = "Assets/Resources/Prefabs/critical/OptionManager.prefab";
    private const string CreditsManagerPrefabPath = "Assets/Resources/Prefabs/menu_credits/creditsManager.prefab";
    private const string StatsManagerPrefabPath = "Assets/Resources/Prefabs/menu_stats/StatsManager.prefab";
    private const string GameManagerPrefabPath = "Assets/Resources/Prefabs/critical/GameManager.prefab";

    private const string AccountHubScenePath = "Assets/Scenes/level_00_account.unity";
    private const string AccountCreateScenePath = "Assets/Scenes/level_00_account_createNew.unity";
    private const string AccountLoginScenePath = "Assets/Scenes/level_00_account_loginExisting.unity";
    private const string ProgressionScenePath = "Assets/Scenes/level_00_progression.unity";
    private const string StartScenePath = "Assets/Scenes/level_00_start.unity";

    /// <summary>
    /// Every scene with the Pause script GUID appearing directly in its YAML (not just through a
    /// GameManager.prefab instance) authors its own "pause" Pause setup inline - found by
    /// CollectMenuUiObjectContractErrors still reporting them missing after the prefab was wired.
    /// Confirmed complete via `grep -rl` for the GUID across Assets/Scenes.
    /// </summary>
    private static readonly string[] ScenesWithTheirOwnPause =
    {
        "Assets/Scenes/level_17_rumble_pit.unity",
        "Assets/Scenes/level_18_aveb2.unity",
        "Assets/Scenes/minigame_racing.unity",
    };

    [MenuItem("Level5/Wire Menu UiObjects")]
    public static void RunAll()
    {
        WireOptions();
        WireCredits();
        WireStats();
        WireProgression();
        WirePause();
        WireAccountHub();
        WireAccountCreate();
        WireAccountLogin();
        WireStartFooter();
        foreach (string scenePath in ScenesWithTheirOwnPause)
        {
            WirePauseInScene(scenePath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("MenuUiObjectsWiring: run complete.");
    }

    // ---------------------------------------------------------------- prefab targets

    private static void WireOptions()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(OptionManagerPrefabPath);
        OptionsManager manager = root.GetComponentInChildren<OptionsManager>(true);
        if (manager == null)
        {
            Debug.LogError("WireOptions: no OptionsManager in " + OptionManagerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        GameObject host = manager.gameObject;
        OptionsUiObjects ui = AddOrGet<OptionsUiObjects>(host);
        // These four panels were resolved by reading OptionsManager's own already-serialized field
        // (it had no name constant for them at all) until OptionsManager migrated and the fields
        // stopped being serialized. Their actual names, recovered from dev HEAD before that edit:
        // keyboardOnly_keys/keyboardMouse_keys/gamepad_keys/touch_keys.
        SetField(ui, "keyboardOnlyObject", FindNamed(root, "keyboardOnly_keys")?.gameObject);
        SetField(ui, "keyboardMouseObject", FindNamed(root, "keyboardMouse_keys")?.gameObject);
        SetField(ui, "gamepadObject", FindNamed(root, "gamepad_keys")?.gameObject);
        SetField(ui, "touchObject", FindNamed(root, "touch_keys")?.gameObject);
        SetField(ui, "keyboardOnlyButton", FindComponentNamed<Button>(root, "controls_keyboard"));
        SetField(ui, "keyboardMouseButton", FindComponentNamed<Button>(root, "controls_keyboardMouse"));
        SetField(ui, "gamepadButton", FindComponentNamed<Button>(root, "controls_gamepad"));
        SetField(ui, "touchButton", FindComponentNamed<Button>(root, "controls_touch"));

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(host);
        WireFooterButtons(footer, root);
        SetField(manager, "ui", ui);
        SetField(manager, "footer", footer);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        footer.Validate(missing,
            (footer.StartOrPlayButton, "startOrPlayButton"),
            (footer.StatsButton, "statsButton"),
            (footer.OptionsButton, "optionsButton"),
            (footer.CreditsButton, "creditsButton"),
            (footer.ProgressionButton, "progressionButton"),
            (footer.AccountButton, "accountButton"),
            (footer.QuitButton, "quitButton"));
        LogMissing(OptionManagerPrefabPath, missing);

        PrefabUtility.SaveAsPrefabAsset(root, OptionManagerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void WireCredits()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CreditsManagerPrefabPath);
        CreditsManager manager = root.GetComponentInChildren<CreditsManager>(true);
        if (manager == null)
        {
            Debug.LogError("WireCredits: no CreditsManager in " + CreditsManagerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        GameObject host = manager.gameObject;
        CreditsUiObjects ui = AddOrGet<CreditsUiObjects>(host);
        SetField(ui, "reportInputField", FindComponentNamed<TMP_InputField>(root, "ReportInputField"));
        SetField(ui, "submitReportButton", FindComponentNamed<Button>(root, "submit_report"));
        SetField(ui, "optionsButton", FindComponentNamed<Button>(root, "options"));

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(host);
        WireFooterButtons(footer, root);
        SetField(manager, "ui", ui);
        SetField(manager, "footer", footer);

        // creditsManager.prefab only has press_start/stats_menu/quit_game on the shared footer -
        // credits_menu/update_menu/account_menu do not exist here (CreditsManager's own "options"
        // and "options_menu" - the latter also absent - are handled by CreditsUiObjects.optionsButton
        // instead), so those are pre-existing always-null references and not required here.
        List<string> missing = new List<string>();
        ui.Validate(missing);
        footer.Validate(missing,
            (footer.StartOrPlayButton, "startOrPlayButton"),
            (footer.StatsButton, "statsButton"),
            (footer.QuitButton, "quitButton"));
        LogMissing(CreditsManagerPrefabPath, missing);

        PrefabUtility.SaveAsPrefabAsset(root, CreditsManagerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void WireStats()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(StatsManagerPrefabPath);
        StatsManager manager = root.GetComponentInChildren<StatsManager>(true);
        if (manager == null)
        {
            Debug.LogError("WireStats: no StatsManager in " + StatsManagerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        GameObject host = manager.gameObject;
        StatsUiObjects ui = AddOrGet<StatsUiObjects>(host);
        SetField(ui, "highScoreTableObject", FindNamed(root, "high_scores_table")?.gameObject);
        SetField(ui, "allTimeTableObject", FindNamed(root, "all_time_table")?.gameObject);
        SetField(ui, "highScoresRowsObject", FindNamed(root, "high_scores_rows")?.gameObject);
        SetField(ui, "mainMenuButton", FindComponentNamed<Button>(root, "main_menu"));
        SetField(ui, "modeSelectButton", FindComponentNamed<Button>(root, "mode_select_name"));
        SetField(ui, "modeSelectOnlineButton", FindComponentNamed<Button>(root, "mode_select_name_online"));
        SetField(ui, "allTimeSelectButton", FindComponentNamed<Button>(root, "all_time_select"));
        SetField(ui, "pageNumberLocalButton", FindComponentNamed<Button>(root, "page_number_local"));
        SetField(ui, "pageNumberOnlineButton", FindComponentNamed<Button>(root, "page_number_online"));
        SetField(ui, "trafficOptionButton", FindComponentNamed<Button>(root, "traffic_value_button"));
        SetField(ui, "hardcoreOptionButton", FindComponentNamed<Button>(root, "hardcore_value_button"));
        SetField(ui, "enemiesOptionButton", FindComponentNamed<Button>(root, "enemies_value_button"));
        SetField(ui, "sniperOptionButton", FindComponentNamed<Button>(root, "sniper_value_button"));
        SetField(manager, "ui", ui);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        LogMissing(StatsManagerPrefabPath, missing);

        PrefabUtility.SaveAsPrefabAsset(root, StatsManagerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// <summary>
    /// Unlike Options/Credits/Stats, ProgressionManager (in <c>progression_manager.prefab</c>) and
    /// its buttons (in the sibling <c>progressionScreen.prefab</c>) are two separate prefab assets
    /// that only share a parent once both are instanced into <see cref="ProgressionScenePath"/> - so
    /// this must wire the scene, not either prefab in isolation.
    /// </summary>
    private static void WireProgression()
    {
        Scene scene = EditorSceneManager.OpenScene(ProgressionScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        ProgressionManager manager = null;
        foreach (GameObject root in roots)
        {
            manager = root.GetComponentInChildren<ProgressionManager>(true);
            if (manager != null)
            {
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogError("WireProgression: no ProgressionManager in " + ProgressionScenePath);
            return;
        }

        GameObject host = manager.gameObject;
        ProgressionUiObjects ui = AddOrGet<ProgressionUiObjects>(host);
        SetField(ui, "confirmationDialogueBox", FindNamedInRoots(roots, "confirm_update")?.gameObject);
        SetField(ui, "playerSelectButton", FindComponentNamedInRoots<Button>(roots, "player_select_button"));
        SetField(ui, "playerSelectOptionButton", FindComponentNamedInRoots<Button>(roots, "player_selected_name"));
        SetField(ui, "progression3AccuracyButton", FindComponentNamedInRoots<Button>(roots, "3accuracyButton"));
        SetField(ui, "progression4AccuracyButton", FindComponentNamedInRoots<Button>(roots, "4accuracyButton"));
        SetField(ui, "progression7AccuracyButton", FindComponentNamedInRoots<Button>(roots, "7accuracyButton"));
        SetField(ui, "confirmButton", FindComponentNamedInRoots<Button>(roots, "confirm_button"));
        SetField(ui, "cancelButton", FindComponentNamedInRoots<Button>(roots, "cancel_button"));
        SetField(ui, "saveButton", FindComponentNamedInRoots<Button>(roots, "save_button"));
        SetField(ui, "resetButton", FindComponentNamedInRoots<Button>(roots, "reset_button"));

        // AUD-092 Phase 3: the 17 display-text references and the player portrait image, wired here
        // rather than by ProgressionTextMeshProMigration (which converts the Text -> TMP components
        // this depends on) because ProgressionUiObjects is scene-owned, not prefab-owned - see that
        // migration's class doc comment.
        SetField(ui, "playerSelectOptionImage", FindComponentNamedInRoots<Image>(roots, "player_selected_image"));
        SetField(ui, "playerSelectOptionText", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "player_selected_name"));
        SetField(ui, "playerProgressionStatsText", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "player_progression_stats"));
        SetField(ui, "playerProgressionUpdatePointsText", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "player_points_available"));
        SetField(ui, "progression3Accuracy", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "3accuracy"));
        SetField(ui, "progression4Accuracy", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "4accuracy"));
        SetField(ui, "progression7Accuracy", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "7accuracy"));
        SetField(ui, "progressionRange", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "range"));
        SetField(ui, "progressionRelease", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "release"));
        SetField(ui, "progressionSpeed", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "speed"));
        SetField(ui, "progressionJump", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "jump"));
        SetField(ui, "progressionLuck", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "luck"));
        SetField(ui, "bonusReleaseText", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "release_bonus"));
        SetField(ui, "bonusRangeText", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "range_bonus"));
        SetField(ui, "bonusLuckText", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "luck_bonus"));
        SetField(ui, "addTo3Text", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "3accuracyButton"));
        SetField(ui, "addTo4Text", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "4accuracyButton"));
        SetField(ui, "addTo7Text", FindComponentNamedInRoots<TextMeshProUGUI>(roots, "7accuracyButton"));

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(host);
        WireFooterButtonsInRoots(footer, roots);
        SetField(manager, "ui", ui);
        SetField(manager, "footer", footer);

        // "quit_game" does not exist anywhere reachable from this scene (confirmed against both
        // progressionScreen.prefab and progression_manager.prefab) - ProgressionManager.quitButton
        // is a pre-existing always-null reference, not a wiring gap, so it is not required here.
        List<string> missing = new List<string>();
        ui.Validate(missing);
        footer.Validate(missing,
            (footer.StartOrPlayButton, "startOrPlayButton"),
            (footer.StatsButton, "statsButton"));
        LogMissing(ProgressionScenePath, missing);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WirePause()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameManagerPrefabPath);
        Pause manager = root.GetComponentInChildren<Pause>(true);
        if (manager == null)
        {
            Debug.LogError("WirePause: no Pause in " + GameManagerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        GameObject host = manager.gameObject;
        PauseUiObjects ui = AddOrGet<PauseUiObjects>(host);
        SetField(ui, "footer", FindNamed(root, "footer")?.gameObject);
        SetField(ui, "fadeTexture", FindComponentNamed<Image>(root, "fade_texture"));
        SetField(ui, "loadSceneText", FindComponentNamed<Text>(root, "load_scene"));
        SetField(ui, "loadStartScreenText", FindComponentNamed<Text>(root, "load_start"));
        SetField(ui, "cancelMenuText", FindComponentNamed<Text>(root, "cancel_menu"));
        SetField(ui, "quitGameText", FindComponentNamed<Text>(root, "quit_game"));
        SetField(ui, "loadSceneButton", FindComponentNamed<Button>(root, "load_scene"));
        SetField(ui, "loadStartScreenButton", FindComponentNamed<Button>(root, "load_start"));
        SetField(ui, "cancelMenuButton", FindComponentNamed<Button>(root, "cancel_menu"));
        SetField(ui, "quitGameButton", FindComponentNamed<Button>(root, "quit_game"));
        SetField(ui, "toggleUiStatsText", FindComponentNamed<Text>(root, "toggle_stats"));
        SetField(ui, "toggleMaxStatsText", FindComponentNamed<Text>(root, "toggle_max_stats"));
        SetField(ui, "toggleFpsText", FindComponentNamed<Text>(root, "toggle_fps"));
        SetField(manager, "ui", ui);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        LogMissing(GameManagerPrefabPath, missing);

        PrefabUtility.SaveAsPrefabAsset(root, GameManagerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void WirePauseInScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        Pause manager = null;
        foreach (GameObject root in roots)
        {
            manager = root.GetComponentInChildren<Pause>(true);
            if (manager != null)
            {
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogError("WirePauseInScene: no Pause in " + scenePath);
            return;
        }

        GameObject host = manager.gameObject;
        PauseUiObjects ui = AddOrGet<PauseUiObjects>(host);
        SetField(ui, "footer", FindNamedInRoots(roots, "footer")?.gameObject);
        SetField(ui, "fadeTexture", FindComponentNamedInRoots<Image>(roots, "fade_texture"));
        SetField(ui, "loadSceneText", FindComponentNamedInRoots<Text>(roots, "load_scene"));
        SetField(ui, "loadStartScreenText", FindComponentNamedInRoots<Text>(roots, "load_start"));
        SetField(ui, "cancelMenuText", FindComponentNamedInRoots<Text>(roots, "cancel_menu"));
        SetField(ui, "quitGameText", FindComponentNamedInRoots<Text>(roots, "quit_game"));
        SetField(ui, "loadSceneButton", FindComponentNamedInRoots<Button>(roots, "load_scene"));
        SetField(ui, "loadStartScreenButton", FindComponentNamedInRoots<Button>(roots, "load_start"));
        SetField(ui, "cancelMenuButton", FindComponentNamedInRoots<Button>(roots, "cancel_menu"));
        SetField(ui, "quitGameButton", FindComponentNamedInRoots<Button>(roots, "quit_game"));
        SetField(ui, "toggleUiStatsText", FindComponentNamedInRoots<Text>(roots, "toggle_stats"));
        SetField(ui, "toggleMaxStatsText", FindComponentNamedInRoots<Text>(roots, "toggle_max_stats"));
        SetField(ui, "toggleFpsText", FindComponentNamedInRoots<Text>(roots, "toggle_fps"));
        SetField(manager, "ui", ui);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        LogMissing(scenePath, missing);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---------------------------------------------------------------- scene targets

    private static void WireAccountHub()
    {
        Scene scene = EditorSceneManager.OpenScene(AccountHubScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        AccountManager manager = FindManagerHost(roots);
        if (manager == null)
        {
            Debug.LogError("WireAccountHub: no AccountManager in " + AccountHubScenePath);
            return;
        }

        GameObject host = manager.gameObject;
        AccountHubUiObjects ui = AddOrGet<AccountHubUiObjects>(host);
        SetField(ui, "createNewButton", FindComponentNamedInRoots<Button>(roots, "createNew"));
        SetField(ui, "loginExistingButton", FindComponentNamedInRoots<Button>(roots, "loginExisting"));
        SetField(ui, "loginLocalButton", FindComponentNamedInRoots<Button>(roots, "loginLocal"));

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(host);
        WireFooterButtonsInRoots(footer, roots);
        SetField(manager, "hubUi", ui);
        SetField(manager, "footer", footer);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        footer.Validate(missing,
            (footer.StartOrPlayButton, "startOrPlayButton"),
            (footer.StatsButton, "statsButton"),
            (footer.OptionsButton, "optionsButton"),
            (footer.CreditsButton, "creditsButton"),
            (footer.ProgressionButton, "progressionButton"),
            (footer.AccountButton, "accountButton"),
            (footer.QuitButton, "quitButton"));
        LogMissing(AccountHubScenePath, missing);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireAccountCreate()
    {
        Scene scene = EditorSceneManager.OpenScene(AccountCreateScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        AccountManager manager = FindManagerHost(roots);
        if (manager == null)
        {
            Debug.LogError("WireAccountCreate: no AccountManager in " + AccountCreateScenePath);
            return;
        }

        GameObject host = manager.gameObject;
        AccountCreateUiObjects ui = AddOrGet<AccountCreateUiObjects>(host);
        SetField(ui, "emailInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "EmailInputField"));
        SetField(ui, "usernameInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "UserNameInputField"));
        SetField(ui, "passwordInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "PasswordInputField"));
        SetField(ui, "firstNameInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "FirstNameInputField"));
        SetField(ui, "lastNameInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "LastNameInputField"));
        SetField(ui, "messageDisplay", FindComponentNamedInRoots<Text>(roots, "messageDisplay"));
        SetField(ui, "checkEmailButton", FindComponentNamedInRoots<Button>(roots, "checkEmail"));
        SetField(ui, "checkUserNameButton", FindComponentNamedInRoots<Button>(roots, "checkUserName"));
        // AUD-092 Phase 5B: createUserButton used to be wired only via its own authored onClick; it is
        // now also code-owned (AccountManager.RegisterButtonCallbacks), so it needs a resolved reference.
        SetField(ui, "createAccountButton", FindComponentNamedInRoots<Button>(roots, "createUserButton"));

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(host);
        WireFooterButtonsInRoots(footer, roots);
        SetField(manager, "createUi", ui);
        SetField(manager, "footer", footer);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        footer.Validate(missing, (footer.AccountButton, "accountButton"));
        LogMissing(AccountCreateScenePath, missing);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireAccountLogin()
    {
        Scene scene = EditorSceneManager.OpenScene(AccountLoginScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        AccountManager manager = FindManagerHost(roots);
        if (manager == null)
        {
            Debug.LogError("WireAccountLogin: no AccountManager in " + AccountLoginScenePath);
            return;
        }

        GameObject host = manager.gameObject;
        AccountLoginUiObjects ui = AddOrGet<AccountLoginUiObjects>(host);
        SetField(ui, "usernameInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "UserNameInputField"));
        SetField(ui, "passwordInputField", FindComponentNamedInRoots<TMP_InputField>(roots, "PasswordInputField"));
        SetField(ui, "messageDisplay", FindComponentNamedInRoots<Text>(roots, "messageDisplay"));
        SetField(ui, "checkUserNameButton", FindComponentNamedInRoots<Button>(roots, "checkUserName"));
        // AUD-092 Phase 5B: loginButton used to be wired only via its own authored onClick; it is now
        // also code-owned (AccountManager.RegisterButtonCallbacks), so it needs a resolved reference.
        SetField(ui, "loginButton", FindComponentNamedInRoots<Button>(roots, "loginButton"));

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(host);
        WireFooterButtonsInRoots(footer, roots);
        SetField(manager, "loginUi", ui);
        SetField(manager, "footer", footer);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        footer.Validate(missing, (footer.AccountButton, "accountButton"));
        LogMissing(AccountLoginScenePath, missing);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireStartFooter()
    {
        Scene scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        StartManager manager = null;
        foreach (GameObject root in roots)
        {
            manager = root.GetComponentInChildren<StartManager>(true);
            if (manager != null)
            {
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogError("WireStartFooter: no StartManager in " + StartScenePath);
            return;
        }

        MenuFooterUiObjects footer = AddOrGet<MenuFooterUiObjects>(manager.gameObject);
        WireFooterButtonsInRoots(footer, roots);
        SetField(manager, "footer", footer);

        List<string> missing = new List<string>();
        footer.Validate(missing,
            (footer.StartOrPlayButton, "startOrPlayButton"),
            (footer.StatsButton, "statsButton"),
            (footer.OptionsButton, "optionsButton"),
            (footer.CreditsButton, "creditsButton"),
            (footer.ProgressionButton, "progressionButton"),
            (footer.AccountButton, "accountButton"),
            (footer.QuitButton, "quitButton"));
        LogMissing(StartScenePath, missing);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---------------------------------------------------------------- shared helpers

    private static void WireFooterButtons(MenuFooterUiObjects footer, GameObject root)
    {
        SetField(footer, "startOrPlayButton", FindComponentNamed<Button>(root, "press_start"));
        SetField(footer, "statsButton", FindComponentNamed<Button>(root, "stats_menu"));
        SetField(footer, "optionsButton", FindComponentNamed<Button>(root, "options_menu"));
        SetField(footer, "creditsButton", FindComponentNamed<Button>(root, "credits_menu"));
        SetField(footer, "progressionButton", FindComponentNamed<Button>(root, "update_menu"));
        SetField(footer, "accountButton", FindComponentNamed<Button>(root, "account_menu"));
        SetField(footer, "quitButton", FindComponentNamed<Button>(root, "quit_game"));
    }

    private static void WireFooterButtonsInRoots(MenuFooterUiObjects footer, GameObject[] roots)
    {
        SetField(footer, "startOrPlayButton", FindComponentNamedInRoots<Button>(roots, "press_start"));
        SetField(footer, "statsButton", FindComponentNamedInRoots<Button>(roots, "stats_menu"));
        SetField(footer, "optionsButton", FindComponentNamedInRoots<Button>(roots, "options_menu"));
        SetField(footer, "creditsButton", FindComponentNamedInRoots<Button>(roots, "credits_menu"));
        SetField(footer, "progressionButton", FindComponentNamedInRoots<Button>(roots, "update_menu"));
        SetField(footer, "accountButton", FindComponentNamedInRoots<Button>(roots, "account_menu"));
        SetField(footer, "quitButton", FindComponentNamedInRoots<Button>(roots, "quit_game"));
    }

    private static AccountManager FindManagerHost(GameObject[] roots)
    {
        foreach (GameObject root in roots)
        {
            AccountManager manager = root.GetComponentInChildren<AccountManager>(true);
            if (manager != null)
            {
                return manager;
            }
        }

        return null;
    }

    private static Transform FindNamed(GameObject root, string name)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == name)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindNamedInRoots(GameObject[] roots, string name)
    {
        foreach (GameObject root in roots)
        {
            Transform found = FindNamed(root, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static T FindComponentNamed<T>(GameObject root, string name) where T : Component
    {
        Transform transform = FindNamed(root, name);
        return transform != null ? transform.GetComponent<T>() : null;
    }

    private static T FindComponentNamedInRoots<T>(GameObject[] roots, string name) where T : Component
    {
        Transform transform = FindNamedInRoots(roots, name);
        return transform != null ? transform.GetComponent<T>() : null;
    }

    private static T AddOrGet<T>(GameObject host) where T : Component
    {
        T existing = host.GetComponent<T>();
        return existing != null ? existing : host.AddComponent<T>();
    }

    private static void SetField(Component target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError(
                "MenuUiObjectsWiring: " + target.GetType().Name + " has no serialized field '" + fieldName + "'.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    private static void LogMissing(string assetPath, List<string> missing)
    {
        if (missing.Count == 0)
        {
            Debug.Log("MenuUiObjectsWiring: " + assetPath + " wired with no missing references.");
            return;
        }

        Debug.LogError(
            "MenuUiObjectsWiring: " + assetPath + " is missing references after wiring:\n- "
                + string.Join("\n- ", missing.ToArray()));
    }
}
