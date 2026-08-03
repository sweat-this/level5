using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchInputStartScreenController : MonoBehaviour
{
    //#if UNITY_ANDROID || UNITY_IOS && !UNITY_EDITOR
    private Vector2 startTouchPosition, endTouchPosition;

    float swipeUpTolerance;
    float swipeDownTolerance;
    float swipeDistance;

    [SerializeField]
    GameObject prevSelectedGameObject;

    GraphicRaycaster m_Raycaster;
    PointerEventData m_PointerEventData;
    EventSystem m_EventSystem;

    Touch touch;
    bool buttonPressed;

    GameObject joystickGameObject;

    [SerializeField]
    public string currentHighlightedButton;

    public static TouchInputStartScreenController instance;


    void Awake()
    {
        initializeStartScreenTouchControls();
    }

    private void Start()
    {
        if (UiSelectionAdapter.EnsureInputSystemUiModule())
        {
            enabled = false;
            return;
        }

        //StartManager.instance.disableButtonsNotUsedForTouchInput();
        // set distance required for swipe up to be regeistered by device
        swipeUpTolerance = Screen.height / 7;
        swipeDownTolerance = Screen.height / 5;
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        prevSelectedGameObject = EventSystem.current.firstSelectedGameObject;
        //Debug.Log("screen width : " + Screen.width);
    }

    void Update()
    {
        if (UiSelectionAdapter.InputSystemUiActive)
        {
            enabled = false;
            return;
        }

        if (EventSystem.current == null)
            return;

        //// if no button selected, return to previous
        if (EventSystem.current.currentSelectedGameObject == null && prevSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        }

        if (EventSystem.current.currentSelectedGameObject == null)
            return;

        currentHighlightedButton = EventSystem.current.currentSelectedGameObject.name;

        // save previous button until a touch is made
        if (!buttonPressed && Input.touchCount == 0)
        {
            prevSelectedGameObject = EventSystem.current.currentSelectedGameObject;
        }
        // if touch
        if (Input.touchCount > 0 && !buttonPressed)
        {
            Touch touch = Input.touches[0];
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Began)
            {
                startTouchPosition = touch.position;
            }
            endTouchPosition = touch.position;
            swipeDistance = endTouchPosition.y - startTouchPosition.y;

            // swipe down on changeable options
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Ended // finger stoppped moving | *tapcount = 1 keeps pause from being called twice
                && Mathf.Abs(swipeDistance) > swipeDownTolerance // swipe is long enough
                && swipeDistance < 0 // swipe down
                && (startTouchPosition.x > (Screen.width / 2))) // if swipe on right 1/2 of screen)) 
            {
                //change option
                swipeDownOnOption();
                // reset previous button to active button
                if (EventSystem.current.currentSelectedGameObject != prevSelectedGameObject)
                {
                    EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
                }
            }
            //swipe up on changeable options
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Ended // finger stoppped moving | *tapcount = 1 keeps pause from being called twice
                && Mathf.Abs(swipeDistance) > swipeDownTolerance // swipe is long enough
                && swipeDistance > 0 // swipe down
                && (startTouchPosition.x > (Screen.width / 2))) // if swipe on right 1/2 of screen)) 
            {
                //change option
                swipeUpOnOption();
                // reset previous button to active button
                if (EventSystem.current.currentSelectedGameObject != prevSelectedGameObject)
                {
                    EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
                }
            }
            // on double tap, perform actions
            if (touch.tapCount == 2 && touch.phase == TouchPhase.Began && !buttonPressed)
            {
                activateDoubleTappedButton();
            }
        }
    }

    private void initializeStartScreenTouchControls()
    {
        // find onscreen stick and disable
        if (GameObject.Find("floating_joystick") != null)
        {
            joystickGameObject = GameObject.Find("floating_joystick");
            joystickGameObject.SetActive(false);
        }

        //check if StartManager is empty and find correct GraphicRaycaster and EventSystem
        if (UnityEngine.Object.FindAnyObjectByType<StartManager>() != null)
        {
            //Fetch the Raycaster from the GameObject (the Canvas)
            //m_Raycaster = StartManager.instance.gameObject.GetComponentInChildren<GraphicRaycaster>();
            m_Raycaster = GameObject.Find("startScreen").GetComponentInChildren<GraphicRaycaster>();
            //Fetch the Event System from the Scene
            m_EventSystem = GameObject.Find("startScreen").GetComponentInChildren<EventSystem>();
        }
        // else, this is not the startscreen and disable object
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void activateDoubleTappedButton()
    {
        buttonPressed = true;
        if (EventSystem.current == null
            || prevSelectedGameObject == null
            || StartManager.instance == null
            || string.IsNullOrEmpty(currentHighlightedButton))
        {
            buttonPressed = false;
            return;
        }

        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);

        //level select
        if (currentHighlightedButton.Equals(StartManager.levelSelectButtonName))
        {
            StartManager.instance.changeSelectedLevelDown();
            StartManager.instance.initializeLevelDisplay();
            buttonPressed = true;
        }
        // player select
        if (currentHighlightedButton.Equals(StartManager.playerSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedPlayerDown();
            StartManager.instance.initializePlayerDisplay();
            buttonPressed = true;
        }
        // friend select
        if (currentHighlightedButton.Equals(StartManager.friendSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedfriendDown();
            StartManager.instance.initializefriendDisplay();
            buttonPressed = true;
        }
        // mode select
        if (currentHighlightedButton.Equals(StartManager.modeSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedModeDown();
            StartManager.instance.initializeModeDisplay();
            buttonPressed = true;
        }
        //stats
        if (currentHighlightedButton.Equals(StartManager.statsMenuButtonName))
        {
            StartManager.instance.LoadStatsMenu();
            buttonPressed = true;
        }
        // start
        if (currentHighlightedButton.Equals(StartManager.startButtonName))
        {
            StartManager.instance.StartGame();
            buttonPressed = true;
        }
        // update /progression
        if (currentHighlightedButton.Equals(StartManager.updateMenuButtonName))
        {
            //Debug.Log("load prgression screen");
            StartManager.instance.LoadProgressionMenu();
            buttonPressed = true;
        }
        // options
        if (currentHighlightedButton.Equals(StartManager.optionsMenuButtonName))
        {
            //Debug.Log("load prgression screen");
            StartManager.instance.LoadOptionsMenu();
            buttonPressed = true;
        }
        // credits
        if (currentHighlightedButton.Equals(StartManager.creditsMenuButtonName))
        {
            //Debug.Log("load prgression screen");
            StartManager.instance.LoadCreditsMenu();
            buttonPressed = true;
        }
        // quit
        if (currentHighlightedButton.Equals(StartManager.quitButtonName))
        {
            StartManager.instance.QuitGame();
            buttonPressed = true;
        }
        //account
        if (currentHighlightedButton.Equals(StartManager.accountMenuButtonName))
        {
            StartManager.instance.LoadAccountMenu();
            buttonPressed = true;
        }
        //cpu select option
        if (currentHighlightedButton.Equals(StartManager.cpuSelectButtonName))
        {
            StartManager.instance.initializeCpuDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.Cpu1SelectOptionName)) { StartManager.instance.setCpuPlayer1(); buttonPressed = true; }
        if (currentHighlightedButton.Equals(StartManager.Cpu2SelectOptionName)) { StartManager.instance.setCpuPlayer2(); buttonPressed = true; }
        if (currentHighlightedButton.Equals(StartManager.Cpu3SelectOptionName)) { StartManager.instance.setCpuPlayer3(); buttonPressed = true; }
        if (currentHighlightedButton.Equals(StartManager.levelSelectButtonName) || currentHighlightedButton.Equals(StartManager.levelSelectOptionButtonName))
        {
            StartManager.instance.initializeLevelDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.modeSelectButtonName) || currentHighlightedButton.Equals(StartManager.modeSelectOptionButtonName))
        {
            StartManager.instance.initializeModeDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.optionsSelectButtonName) || currentHighlightedButton.Equals(StartManager.optionsSelectOptionName))
        {
            StartManager.instance.initializeOptionsDisplay();
            buttonPressed = true;
            //options
            //if (currentHighlightedButton.Equals(StartManager.OptionsMenuButtonName))
            //{
            //    StartManager.instance.loadMenu(Constants.SCENE_NAME_level_00_options);
            //    buttonPressed = true;
            //}
        }
        if (currentHighlightedButton.Equals(StartManager.trafficSelectOptionName))
        {
            // disabled for now. default : OFF
            StartManager.instance.changeSelectedTrafficOption();
            StartManager.instance.initializeTrafficOptionDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.hardcoreSelectOptionName))
        {
            StartManager.instance.changeSelectedHardcoreOption();
            StartManager.instance.initializeHardcoreOptionDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.enemySelectOptionName))
        {
            StartManager.instance.changeSelectedEnemiesOption();
            StartManager.instance.initializeEnemyOptionDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.SniperSelectOptionName))
        {
            StartManager.instance.changeSelectedSniperOption();
            StartManager.instance.initializeSniperOptionDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.difficultySelectOptionName))
        {
            StartManager.instance.changeSelectedDifficultyOption(StartManager.instance.difficultySelected);
            StartManager.instance.initializeDifficultyOptionDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.ObstacleSelectOptionName))
        {
            Debug.Log("obstacle selected : double tap");
            StartManager.instance.changeSelectedObstacleOption();
            StartManager.instance.initializeObstacleOptionDisplay();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(StartManager.Cpu1SelectOptionName)
            || currentHighlightedButton.Equals(StartManager.Cpu2SelectOptionName)
            || currentHighlightedButton.Equals(StartManager.Cpu3SelectOptionName))
        {
            StartManager.instance.changeSelectedCpuOptionUp(currentHighlightedButton);
            buttonPressed = true;
        }
        buttonPressed = false;
    }

    private void selectPressedButton()
    {
        //Set up the new Pointer Event
        m_PointerEventData = new PointerEventData(m_EventSystem);
        //Set the Pointer Event Position to that of the mouse position
        m_PointerEventData.position = Input.mousePosition;

        //Create a list of Raycast Results
        List<RaycastResult> results = new List<RaycastResult>();

        //Raycast using the Graphics Raycaster and mouse click position
        m_Raycaster.Raycast(m_PointerEventData, results);
    }

    private void swipeUpOnOption()
    {
        if (EventSystem.current == null || prevSelectedGameObject == null || StartManager.instance == null)
        {
            buttonPressed = false;
            return;
        }

        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        //level select
        if (prevSelectedGameObject.name.Equals(StartManager.levelSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedLevelUp();
            StartManager.instance.initializeLevelDisplay();
            buttonPressed = true;
        }
        // traffic select
        if (prevSelectedGameObject.name.Equals(StartManager.trafficSelectOptionName))
        {
            StartManager.instance.changeSelectedTrafficOption();
            StartManager.instance.initializeTrafficOptionDisplay();
            buttonPressed = true;
        }
        if (prevSelectedGameObject.name.Equals(StartManager.hardcoreSelectOptionName))
        {
            StartManager.instance.changeSelectedHardcoreOption();
            StartManager.instance.initializeHardcoreOptionDisplay();
            buttonPressed = true;
        }
        if (prevSelectedGameObject.name.Equals(StartManager.SniperSelectOptionName))
        {
            StartManager.instance.changeSelectedSniperOption();
            StartManager.instance.initializeSniperOptionDisplay();
            buttonPressed = true;
        }
        if (prevSelectedGameObject.name.Equals(StartManager.enemySelectOptionName))
        {
            StartManager.instance.changeSelectedEnemiesOption();
            StartManager.instance.initializeEnemyOptionDisplay();
        }
        if (prevSelectedGameObject.name.Equals(StartManager.difficultySelectOptionName))
        {
            StartManager.instance.changeSelectedDifficultyOption(GameOptions.difficultySelected);
            StartManager.instance.initializeDifficultyOptionDisplay();
        }
        if (prevSelectedGameObject.name.Equals(StartManager.ObstacleSelectOptionName))
        {
            StartManager.instance.changeSelectedObstacleOption();
            StartManager.instance.initializeObstacleOptionDisplay();
        }
        //// num players  select
        //if (prevSelectedGameObject.name.Equals(StartManager.num))
        //{
        //    StartManager.instance.changeSelectedPlayerUp();
        //    StartManager.instance.initializePlayerDisplay();
        //    buttonPressed = true;
        //}
        // player select
        if (prevSelectedGameObject.name.Equals(StartManager.playerSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedPlayerUp();
            StartManager.instance.initializePlayerDisplay();
            buttonPressed = true;
        }
        // friend select
        if (prevSelectedGameObject.name.Equals(StartManager.friendSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedfriendUp();
            StartManager.instance.initializefriendDisplay();
            buttonPressed = true;
        }
        // mode select
        if (prevSelectedGameObject.name.Equals(StartManager.modeSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedModeUp();
            StartManager.instance.initializeModeDisplay();
            buttonPressed = true;
        }
        buttonPressed = false;
    }
    private void swipeDownOnOption()
    {
        if (EventSystem.current == null || prevSelectedGameObject == null || StartManager.instance == null)
        {
            buttonPressed = false;
            return;
        }

        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        //level select
        if (prevSelectedGameObject.name.Equals(StartManager.levelSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedLevelDown();
            StartManager.instance.initializeLevelDisplay();
            buttonPressed = true;
        }
        // traffic select
        if (prevSelectedGameObject.name.Equals(StartManager.trafficSelectOptionName))
        {
            StartManager.instance.changeSelectedTrafficOption();
            StartManager.instance.initializeTrafficOptionDisplay();
            buttonPressed = true;
        }
        if (prevSelectedGameObject.name.Equals(StartManager.hardcoreSelectOptionName))
        {
            StartManager.instance.changeSelectedHardcoreOption();
            StartManager.instance.initializeHardcoreOptionDisplay();
            buttonPressed = true;
        }
        // sniper select
        if (prevSelectedGameObject.name.Equals(StartManager.SniperSelectOptionName))
        {
            StartManager.instance.changeSelectedSniperOption();
            StartManager.instance.initializeSniperOptionDisplay();
            buttonPressed = true;
        }
        if (prevSelectedGameObject.name.Equals(StartManager.enemySelectOptionName))
        {
            StartManager.instance.changeSelectedEnemiesOption();
            StartManager.instance.initializeEnemyOptionDisplay();
        }
        if (prevSelectedGameObject.name.Equals(StartManager.difficultySelectOptionName))
        {
            StartManager.instance.changeSelectedDifficultyOption(GameOptions.difficultySelected);
            StartManager.instance.initializeDifficultyOptionDisplay();
        }
        if (prevSelectedGameObject.name.Equals(StartManager.ObstacleSelectOptionName))
        {
            StartManager.instance.changeSelectedObstacleOption();
            StartManager.instance.initializeObstacleOptionDisplay();
        }
        // player select
        if (prevSelectedGameObject.name.Equals(StartManager.playerSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedPlayerDown();
            StartManager.instance.initializePlayerDisplay();
            buttonPressed = true;
        }
        // friend select
        if (prevSelectedGameObject.name.Equals(StartManager.friendSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedfriendDown();
            StartManager.instance.initializefriendDisplay();
            buttonPressed = true;
        }
        // mode select
        if (prevSelectedGameObject.name.Equals(StartManager.modeSelectOptionButtonName))
        {
            StartManager.instance.changeSelectedModeDown();
            StartManager.instance.initializeModeDisplay();
            buttonPressed = true;
        }
        buttonPressed = false;
    }
    //#endif
}
