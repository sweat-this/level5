
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TouchPhase = UnityEngine.TouchPhase;

public class TouchInputLinksScreenController : MonoBehaviour
{

    private Vector2 startTouchPosition, endTouchPosition;

    float swipeUpTolerance;
    float swipeDownTolerance;
    float swipeDistance;

    [SerializeField]
    GraphicRaycaster m_Raycaster;
    PointerEventData m_PointerEventData;
    [SerializeField]
    EventSystem m_EventSystem;
    [SerializeField]
    GameObject CreditsManagerObject;

    Touch touch;
    bool buttonPressed;
    [SerializeField]
    GameObject joystickGameObject;

    //public static TouchInputController instance;
    private GameObject prevSelectedGameObject;

    void Awake()
    {
        initializeOptionScreenTouchControls();
    }
    private void Start()
    {
        if (UiSelectionAdapter.EnsureInputSystemUiModule())
        {
            enabled = false;
            return;
        }

        // set distance required for swipe up to be regeistered by device
        swipeUpTolerance = Screen.height / 7;
        swipeDownTolerance = Screen.height / 5;
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        prevSelectedGameObject = EventSystem.current.firstSelectedGameObject;
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

        if (EventSystem.current.currentSelectedGameObject == null && prevSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        }

        if (EventSystem.current.currentSelectedGameObject == null)
            return;

        // save previous button until a touch is made
        if (!buttonPressed && Input.touchCount == 0)
        {
            prevSelectedGameObject = EventSystem.current.currentSelectedGameObject;
        }

        if (Input.touchCount > 0 && !buttonPressed)
        {
            Touch touch = Input.touches[0];
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Began)
            {
                startTouchPosition = touch.position;
            }
            endTouchPosition = touch.position;
            swipeDistance = endTouchPosition.y - startTouchPosition.y;

            // on double tap, perform actions
            if (touch.tapCount == 2 && touch.phase == TouchPhase.Began 
                && !buttonPressed)
            {
                buttonPressed = true;
                activateDoubleTappedButton();
            }
        }
    }

    private void initializeOptionScreenTouchControls()
    {
        // find onscreen stick and disable
        if (GameObject.Find("floating_joystick") != null)
        {
            joystickGameObject = GameObject.Find("floating_joystick");
            joystickGameObject.SetActive(false);
        }

        //check if startmanager is empty and find correct GraphicRaycaster and EventSystem
        CreditsManager creditsManager = UnityEngine.Object.FindAnyObjectByType<CreditsManager>();
        if (creditsManager != null)
        {
            CreditsManagerObject = creditsManager.gameObject;
            //Fetch the Raycaster from the GameObject (the Canvas)
            m_Raycaster = creditsManager.gameObject.GetComponentInChildren<GraphicRaycaster>();
            //Fetch the Event System from the Scene
            m_EventSystem = creditsManager.gameObject.GetComponentInChildren<EventSystem>();
        }
        // else, this is not the startscreen and disable object
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void activateDoubleTappedButton()
    {
        //buttonPressed = true;
        GameObject selectedObject = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        if (selectedObject == null)
        {
            buttonPressed = false;
            return;
        }

        //if (EventSystem.current.currentSelectedGameObject.name.Equals(StatsManager.ModeSelectButtonName))
        //{
        //    StatsManager.instance.changeSelectedMode("right");
        //    //StatsManager.instance.changeHighScoreModeNameDisplay();
        //    StatsManager.instance.changeHighScoreDataDisplay();
        //}

        // footer
        // main menu
        if (selectedObject.name.Equals(CreditsManager.MainMenuButtonName))
        {
            SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_start);
        }
        //stats
        if (selectedObject.name.Equals(CreditsManager.StatsMenuButtonName))
        {
            SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_stats);
        }
        //progression
        if (selectedObject.name.Equals(CreditsManager.ProgressionMenuButtonName))
        {
            SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_progression);
        }
        // account
        if (selectedObject.name.Equals(CreditsManager.AccountMenuButtonName))
        {
            SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account);
        }
        //credits
        if (selectedObject.name.Equals(CreditsManager.CreditsMenuButtonName))
        {
            SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_credits);
        }

        // account
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

}
