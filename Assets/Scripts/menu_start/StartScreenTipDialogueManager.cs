using Assets.Scripts.Utility;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class StartScreenTipDialogueManager : MonoBehaviour
{
    [HideInInspector]
    public int NONE = 0, YES = 1, CANCEL = 2, NEXT = 3;
    [HideInInspector]
    public int result = 0;

    [SerializeField]
    private TipDialogueUiObjects ui;

    [SerializeField]
    List<PlayerTips> tipsList;

    int randomTipIndex = 0;
    PlayerControls controls;

    bool buttonPressed = false;
    bool menuMapsEnabled;

    private void OnEnable()
    {
        if (!GameOptions.tipDialogueLoadedOnStart)
        {
            controls = PlayerControlsProvider.Controls;
            PlayerControlsProvider.EnableMenuMaps();
            menuMapsEnabled = true;
        }

        ui.NextButton.onClick.AddListener(NextButtonOnClick);
        ui.CloseButton.onClick.AddListener(CancelButtonOnClick);
    }
    private void OnDisable()
    {
        ui.NextButton.onClick.RemoveListener(NextButtonOnClick);
        ui.CloseButton.onClick.RemoveListener(CancelButtonOnClick);

        if (menuMapsEnabled)
        {
            PlayerControlsProvider.DisableMenuMaps();
            menuMapsEnabled = false;
        }
    }

    // Start is called before the first frame update
    void Awake()
    {
        if (!GameOptions.tipDialogueLoadedOnStart)
        {
            controls = PlayerControlsProvider.Controls;

            int i = 0;
            foreach (PlayerTips tip in tipsList)
            {
                tip.tipId = i;
                i++;
            }
            randomTipIndex = UtilityFunctions.GetRandomInteger(0, tipsList.Count);
            ui.Tip.text = tipsList[randomTipIndex].tip;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(ui.NextButton.gameObject);
        GameOptions.tipDialogueLoadedOnStart = true;
        ui.Header.text = "Tip" + "    " + (randomTipIndex + 1) + " / " + (tipsList.Count);
    }

    private void CloseTipDialogue()
    {
        EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        Destroy(this.gameObject);
    }
    public void NextTipButton()
    {
        buttonPressed = true;

        if (randomTipIndex < (tipsList.Count - 1))
        {
            randomTipIndex++;
            ui.Tip.text = tipsList[randomTipIndex].tip;
        }
        else
        {
            randomTipIndex = 0;
            ui.Tip.text = tipsList[0].tip;
        }
        EventSystem.current.SetSelectedGameObject(ui.NextButton.gameObject);
        ui.Header.text = "Tip" + "    " + (randomTipIndex + 1) + " / " + (tipsList.Count);

        buttonPressed = false;
    }

    public void CancelButtonOnClick()
    {
        result = CANCEL;
        CloseTipDialogue();
    }

    public void NextButtonOnClick()
    {
        result = NEXT;
        if (!buttonPressed)
        {
            NextTipButton();
        }
    }
}
