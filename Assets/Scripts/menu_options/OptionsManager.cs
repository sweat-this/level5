
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    [SerializeField]
    public string currentHighlightedButton;

    //footer object names
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

    [SerializeField]
    GameObject keyboardOnlyObject;
    [SerializeField]
    GameObject keyboardMouseObject;
    [SerializeField]
    GameObject gamepadObject;
    [SerializeField]
    GameObject touchObject;

    [SerializeField] Button mainMenuButton;
    [SerializeField] Button statsMenuButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button optionsMenuButton;
    [SerializeField] Button creditsMenuButton;
    [SerializeField] Button progressionMenuButton;
    [SerializeField] Button accountMenuButton;
    [SerializeField] Button keyboardOnlyButton;
    [SerializeField] Button keyboardMouseButton;
    [SerializeField] Button gamepadButton;
    [SerializeField] Button touchButton;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
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

        UiSelectionAdapter.EnsureInputSystemUiModule();
        ResolveButtonReferences();
        RegisterButtonCallbacks();
        DisplayKeyboardOnlyControls();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    private void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject == null)
        {
            return;
        }

        currentHighlightedButton = selectedObject.name;
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

    private void ResolveButtonReferences()
    {
        mainMenuButton = ResolveButton(mainMenuButton, mainMenuButtonName);
        statsMenuButton = ResolveButton(statsMenuButton, statsMenuButtonName);
        quitButton = ResolveButton(quitButton, quitButtonName);
        optionsMenuButton = ResolveButton(optionsMenuButton, optionsMenuButtonName);
        creditsMenuButton = ResolveButton(creditsMenuButton, creditsMenuButtonName);
        progressionMenuButton = ResolveButton(progressionMenuButton, progressionMenuButtonName);
        accountMenuButton = ResolveButton(accountMenuButton, accountMenuButtonName);
        keyboardOnlyButton = ResolveButton(keyboardOnlyButton, keyboardOnlyMenuButtonName);
        keyboardMouseButton = ResolveButton(keyboardMouseButton, keyboardMouseMenuButtonName);
        gamepadButton = ResolveButton(gamepadButton, gamepadMenuButtonName);
        touchButton = ResolveButton(touchButton, touchMenuButtonName);
    }

    private Button ResolveButton(Button button, string buttonName)
    {
        if (button != null)
        {
            return button;
        }

        GameObject buttonObject = GameObject.Find(buttonName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
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
