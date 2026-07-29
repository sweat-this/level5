
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

    PlayerControls controls;

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.UINavigation.Enable();
        controls.Other.Enable();
    }
    private void OnDisable()
    {
        controls.Player.Disable();
        controls.UINavigation.Disable();
        controls.Other.Disable();
    }

    void Awake()
    {
        controls = new PlayerControls();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }
        displayControls(keyboardOnlyMenuButtonName);
    }

    private void Update()
    {
        // check for some button not selected
        if (EventSystem.current != null)
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject); // + "_description";
            }
            currentHighlightedButton = EventSystem.current.currentSelectedGameObject.name; // + "_description";
        }
        // ================================== footer buttons ===========================================
        // start button | start game
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(mainMenuButtonName))
        {
            loadMenu(Constants.SCENE_NAME_level_00_start);
        }
        // quit button | quit game
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(quitButtonName))
        {
            Application.Quit();
        }
        // stats menu button | load stats menu
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(statsMenuButtonName))
        {
            loadMenu(Constants.SCENE_NAME_level_00_stats);
        }

        // update menu button | load update menu
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(progressionMenuButtonName))
        {
            loadMenu(Constants.SCENE_NAME_level_00_progression);
        }
        // options menu button | load options menu
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(optionsMenuButtonName))
        {
            loadMenu(Constants.SCENE_NAME_level_00_options);
        }
        // credits menu button | load credits menu
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(creditsMenuButtonName))
        {
            loadMenu(Constants.SCENE_NAME_level_00_credits);
        }
        // account menu button | load account menu
        if (controls.UINavigation.Submit.triggered
            && currentHighlightedButton.Equals(accountMenuButtonName))
        {
            loadMenu(Constants.SCENE_NAME_level_00_account);
        }
        // ================================== Display controls ===========================================

        // keyboard only
        if (currentHighlightedButton.Equals(keyboardOnlyMenuButtonName))
        {
            displayControls("keyboardOnly");
        }
        // keyboard + mouse
        if (currentHighlightedButton.Equals(keyboardMouseMenuButtonName))
        {
            displayControls("keyboardMouse");
        }
        // gamepad
        if (currentHighlightedButton.Equals(gamepadMenuButtonName))
        {
            displayControls("gamepad");
        }
        // touch
        if (currentHighlightedButton.Equals(touchMenuButtonName))
        {
            displayControls("touch");
        }
    }

    private void displayControls(string controls)
    {
        if (controls.Contains("keyboardOnly"))
        {
            keyboardOnlyObject.SetActive(true);
            keyboardMouseObject.SetActive(false);
            gamepadObject.SetActive(false);
            touchObject.SetActive(false);
        }
        if (controls.Contains("keyboardMouse"))
        {
            keyboardOnlyObject.SetActive(false);
            keyboardMouseObject.SetActive(true);
            gamepadObject.SetActive(false);
            touchObject.SetActive(false);
        }
        if (controls.Contains("gamepad"))
        {
            keyboardOnlyObject.SetActive(false);
            keyboardMouseObject.SetActive(false);
            gamepadObject.SetActive(true);
            touchObject.SetActive(false);
        }
        if (controls.Contains("touch"))
        {
            keyboardOnlyObject.SetActive(false);
            keyboardMouseObject.SetActive(false);
            gamepadObject.SetActive(false);
            touchObject.SetActive(true);
        }
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
