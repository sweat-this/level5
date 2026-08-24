
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    [SerializeField]
    public string currentHighlightedButton;

    [SerializeField] private OptionsUiObjects ui;
    [SerializeField] private MenuFooterUiObjects footer;

    //footer object names - kept for TouchInputOptionsScreenController's name-selected dispatch
    //(out of scope: legacy touch controller deletion is gated on device verification)
    private const string mainMenuButtonName = "press_start";
    private const string statsMenuButtonName = "stats_menu";
    private const string optionsButtonName = "options";
    private const string quitButtonName = "quit_game";
    private const string optionsMenuButtonName = "options_menu";
    private const string creditsMenuButtonName = "credits_menu";
    private const string progressionMenuButtonName = "update_menu";
    private const string accountMenuButtonName = "account_menu";

    private const string keyboardOnlyMenuButtonName = "controls_keyboard";
    private const string keyboardMouseMenuButtonName = "controls_keyboardMouse";
    private const string gamepadMenuButtonName = "controls_gamepad";
    private const string touchMenuButtonName = "controls_touch";

    // RequiredSceneObjectNames retired: Level5ProjectValidator now asserts this screen's contract
    // through ValidateMenuUi/CollectMenuUiObjectContractErrors instead of a name list (AUD-103).

    GameObject keyboardOnlyObject;
    GameObject keyboardMouseObject;
    GameObject gamepadObject;
    GameObject touchObject;

    Button mainMenuButton;
    Button statsMenuButton;
    Button quitButton;
    Button optionsMenuButton;
    Button creditsMenuButton;
    Button progressionMenuButton;
    Button accountMenuButton;
    Button keyboardOnlyButton;
    Button keyboardMouseButton;
    Button gamepadButton;
    Button touchButton;

    private bool initialized;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        // AUD-102: OnDisable unregisters every onClick but this used to not register them again,
        // so disabling and re-enabling this component left every button on the screen inert.
        if (initialized)
        {
            RegisterButtonCallbacks();
        }
    }
    private void OnDisable()
    {
        UnregisterButtonCallbacks();
        PlayerControlsProvider.DisableMenuMaps();
    }

    void Awake()
    {
    }

    // Start is called before the first frame update
    void Start()
    {
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            Debug.LogError(
                "OptionsManager is missing required serialized UI references and will be disabled: "
                    + string.Join(", ", missing.ToArray()),
                this);
            enabled = false;
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        ResolveButtonReferences();
        RegisterButtonCallbacks();
        DisplayKeyboardOnlyControls();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        initialized = true;
    }

    /// <summary>
    /// True once <see cref="ui"/>/<see cref="footer"/> carry every reference this screen needs.
    /// Callable from editor tooling as a pure check - it only reads already-serialized references,
    /// so it does not require the component to have run <see cref="Start"/>.
    /// </summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        if (ui == null)
        {
            missing.Add("OptionsManager.ui");
        }
        else
        {
            ui.Validate(missing);
        }

        if (footer == null)
        {
            missing.Add("OptionsManager.footer");
        }
        else
        {
            footer.Validate(
                missing,
                (footer.StartOrPlayButton, "startOrPlayButton"),
                (footer.StatsButton, "statsButton"),
                (footer.OptionsButton, "optionsButton"),
                (footer.CreditsButton, "creditsButton"),
                (footer.ProgressionButton, "progressionButton"),
                (footer.AccountButton, "accountButton"),
                (footer.QuitButton, "quitButton"));
        }

        return missing.Count == 0;
    }

    /// <summary>
    /// Shows the control scheme for whichever control is selected.
    ///
    /// The four checks below used to run their Display* method on every frame the selection rested
    /// on a controls button, so <see cref="DisplayControls"/> - four SetActive calls and four
    /// string comparisons - ran every frame for as long as the player sat there (AUD-109). It now
    /// only runs when the selection actually changes.
    /// </summary>
    private void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject == null)
        {
            return;
        }

        string selectedName = selectedObject.name;
        if (selectedName == currentHighlightedButton)
        {
            return;
        }

        currentHighlightedButton = selectedName;
        if (currentHighlightedButton.Equals(keyboardOnlyMenuButtonName))
        {
            DisplayKeyboardOnlyControls();
        }
        if (currentHighlightedButton.Equals(keyboardMouseMenuButtonName))
        {
            DisplayKeyboardMouseControls();
        }
        if (currentHighlightedButton.Equals(gamepadMenuButtonName))
        {
            DisplayGamepadControls();
        }
        if (currentHighlightedButton.Equals(touchMenuButtonName))
        {
            DisplayTouchControls();
        }
    }

    /// <summary>
    /// Copies references out of the serialized <see cref="ui"/>/<see cref="footer"/> views, which
    /// <see cref="ValidateMenuUi"/> has already confirmed are complete. This replaced a chain of
    /// <c>GameObject.Find(name)</c> fallbacks (AUD-103): the Inspector/prefab is the only authority
    /// for these references now.
    /// </summary>
    private void ResolveButtonReferences()
    {
        mainMenuButton = footer.StartOrPlayButton;
        statsMenuButton = footer.StatsButton;
        quitButton = footer.QuitButton;
        optionsMenuButton = footer.OptionsButton;
        creditsMenuButton = footer.CreditsButton;
        progressionMenuButton = footer.ProgressionButton;
        accountMenuButton = footer.AccountButton;
        keyboardOnlyButton = ui.KeyboardOnlyButton;
        keyboardMouseButton = ui.KeyboardMouseButton;
        gamepadButton = ui.GamepadButton;
        touchButton = ui.TouchButton;
        keyboardOnlyObject = ui.KeyboardOnlyObject;
        keyboardMouseObject = ui.KeyboardMouseObject;
        gamepadObject = ui.GamepadObject;
        touchObject = ui.TouchObject;
    }

    private void RegisterButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.RegisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.RegisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.RegisterButton(optionsMenuButton, LoadOptionsMenu);
        UiSelectionAdapter.RegisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.RegisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.RegisterButton(quitButton, QuitGame);
        UiSelectionAdapter.RegisterButton(keyboardOnlyButton, DisplayKeyboardOnlyControls);
        UiSelectionAdapter.RegisterButton(keyboardMouseButton, DisplayKeyboardMouseControls);
        UiSelectionAdapter.RegisterButton(gamepadButton, DisplayGamepadControls);
        UiSelectionAdapter.RegisterButton(touchButton, DisplayTouchControls);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.UnregisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.UnregisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.UnregisterButton(optionsMenuButton, LoadOptionsMenu);
        UiSelectionAdapter.UnregisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.UnregisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.UnregisterButton(quitButton, QuitGame);
        UiSelectionAdapter.UnregisterButton(keyboardOnlyButton, DisplayKeyboardOnlyControls);
        UiSelectionAdapter.UnregisterButton(keyboardMouseButton, DisplayKeyboardMouseControls);
        UiSelectionAdapter.UnregisterButton(gamepadButton, DisplayGamepadControls);
        UiSelectionAdapter.UnregisterButton(touchButton, DisplayTouchControls);
    }

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        return keyboardOnlyButton != null ? keyboardOnlyButton.gameObject : null;
    }

    private void DisplayKeyboardOnlyControls()
    {
        DisplayControls("keyboardOnly");
    }

    private void DisplayKeyboardMouseControls()
    {
        DisplayControls("keyboardMouse");
    }

    private void DisplayGamepadControls()
    {
        DisplayControls("gamepad");
    }

    private void DisplayTouchControls()
    {
        DisplayControls("touch");
    }

    private void DisplayControls(string controls)
    {
        SetActiveIfNotNull(keyboardOnlyObject, controls.Contains("keyboardOnly"));
        SetActiveIfNotNull(keyboardMouseObject, controls.Contains("keyboardMouse"));
        SetActiveIfNotNull(gamepadObject, controls.Contains("gamepad"));
        SetActiveIfNotNull(touchObject, controls.Contains("touch"));
    }

    private void SetActiveIfNotNull(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void LoadStartMenu()
    {
        loadMenu(Constants.SCENE_NAME_level_00_start);
    }

    private void LoadStatsMenu()
    {
        loadMenu(Constants.SCENE_NAME_level_00_stats);
    }

    private void LoadProgressionMenu()
    {
        loadMenu(Constants.SCENE_NAME_level_00_progression);
    }

    private void LoadOptionsMenu()
    {
        loadMenu(Constants.SCENE_NAME_level_00_options);
    }

    private void LoadCreditsMenu()
    {
        loadMenu(Constants.SCENE_NAME_level_00_credits);
    }

    private void LoadAccountMenu()
    {
        loadMenu(Constants.SCENE_NAME_level_00_account);
    }

    private void QuitGame()
    {
        Application.Quit();
    }

    public void loadMenu(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    public static string MainMenuButtonName => mainMenuButtonName;
    public static string StatsMenuButtonName => statsMenuButtonName;
    public static string OptionsButtonName => optionsButtonName;
    public static string QuitButtonName => quitButtonName;
    public static string OptionsMenuButtonName => optionsMenuButtonName;
    public static string CreditsMenuButtonName => creditsMenuButtonName;
    public static string ProgressionMenuButtonName => progressionMenuButtonName;
    public static string AccountMenuButtonName => accountMenuButtonName;
}
