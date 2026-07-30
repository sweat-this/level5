using Assets.Scripts.Utility;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StartScreenCpuSelectManager : MonoBehaviour
{
    [HideInInspector]
    public int NONE = 0, YES = 1, CANCEL = 2, NEXT = 3;
    [HideInInspector]
    public int result = 0;

    [SerializeField]
    private Button cancelButton;
    [SerializeField]
    private Button nextButton;

    [SerializeField]
    Text tipText;

    [SerializeField]
    Text headerText;

    [SerializeField]
    List<PlayerTips> tipsList;

    PlayerControls controls;

    bool menuMapsEnabled;

    public Button CancelButton { get => cancelButton; set => cancelButton = value; }
    public Button NextButton { get => nextButton; set => nextButton = value; }

    private void OnEnable()
    {
        if (!GameOptions.tipDialogueLoadedOnStart)
        {
            controls = PlayerControlsProvider.Controls;
            PlayerControlsProvider.EnableMenuMaps();
            menuMapsEnabled = true;
        }
    }
    private void OnDisable()
    {
        if (menuMapsEnabled)
        {
            PlayerControlsProvider.DisableMenuMaps();
            menuMapsEnabled = false;
        }
    }

    // Start is called before the first frame update
    void Awake()
    {
        //if (!GameOptions.tipDialogueLoadedOnStart)
        //{
            //instance = this;
            controls = PlayerControlsProvider.Controls;
            if (GameObject.Find("cancel_button") != null)
            {
                cancelButton = GameObject.Find("cancel_button").GetComponent<Button>();
                cancelButton.onClick.AddListener(CancelButtonOnClick);
            }
            //if (GameObject.Find("next_button") != null)
            //{
            //    nextButton = GameObject.Find("next_button").GetComponent<Button>();
            //    nextButton.onClick.AddListener(NextButtonOnClick);
            //}
        //}
        //else
        //{
        //    gameObject.SetActive(false);
        //}
    }

    private void Start()
    {
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
        GameOptions.tipDialogueLoadedOnStart = true;
        //headerText.text = "Tip" + "    " + (randomTipIndex + 1) + " / " + (tipsList.Count);
    }

    private void CloseTipDialogue()
    {
        EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        //Destroy(this.gameObject);
        gameObject.SetActive(false);
    }
    //public void NextTipButton()
    //{
    //    buttonPressed = true;

    //    if (randomTipIndex < (tipsList.Count - 1))
    //    {
    //        randomTipIndex++;
    //        tipText.text = tipsList[randomTipIndex].tip;
    //    }
    //    else
    //    {
    //        randomTipIndex = 0;
    //        tipText.text = tipsList[0].tip;
    //    }
    //    EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
    //    headerText.text = "Tip" + "    " + (randomTipIndex + 1) + " / " + (tipsList.Count);

    //    buttonPressed = false;
    //}

    public void CancelButtonOnClick()
    {
        result = CANCEL;
        CloseTipDialogue();
    }

    //public void NextButtonOnClick()
    //{
    //    result = NEXT;
    //    if (!buttonPressed)
    //    {
    //        NextTipButton();
    //    }
    //}
}
