using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The progression screen's own controls. <c>confirmButton</c>/<c>cancelButton</c> live inside
/// <see cref="confirmationDialogueBox"/>, which starts inactive - serializing them directly here is
/// what makes resolving them through an active-object-only lookup unnecessary. start/stats/quit
/// footer buttons live on <see cref="MenuFooterUiObjects"/>, not here.
/// </summary>
public class ProgressionUiObjects : MonoBehaviour
{
    [SerializeField] private GameObject confirmationDialogueBox;

    [SerializeField] private Button playerSelectButton;
    [SerializeField] private Button playerSelectOptionButton;
    [SerializeField] private Button progression3AccuracyButton;
    [SerializeField] private Button progression4AccuracyButton;
    [SerializeField] private Button progression7AccuracyButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;

    public GameObject ConfirmationDialogueBox => confirmationDialogueBox;

    public Button PlayerSelectButton => playerSelectButton;
    public Button PlayerSelectOptionButton => playerSelectOptionButton;
    public Button Progression3AccuracyButton => progression3AccuracyButton;
    public Button Progression4AccuracyButton => progression4AccuracyButton;
    public Button Progression7AccuracyButton => progression7AccuracyButton;
    public Button ConfirmButton => confirmButton;
    public Button CancelButton => cancelButton;
    public Button SaveButton => saveButton;
    public Button ResetButton => resetButton;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (confirmationDialogueBox == null) missing.Add("ProgressionUiObjects.confirmationDialogueBox");
        if (playerSelectButton == null) missing.Add("ProgressionUiObjects.playerSelectButton");
        if (playerSelectOptionButton == null) missing.Add("ProgressionUiObjects.playerSelectOptionButton");
        if (progression3AccuracyButton == null) missing.Add("ProgressionUiObjects.progression3AccuracyButton");
        if (progression4AccuracyButton == null) missing.Add("ProgressionUiObjects.progression4AccuracyButton");
        if (progression7AccuracyButton == null) missing.Add("ProgressionUiObjects.progression7AccuracyButton");
        if (confirmButton == null) missing.Add("ProgressionUiObjects.confirmButton");
        if (cancelButton == null) missing.Add("ProgressionUiObjects.cancelButton");
        if (saveButton == null) missing.Add("ProgressionUiObjects.saveButton");
        if (resetButton == null) missing.Add("ProgressionUiObjects.resetButton");
        return missing.Count == before;
    }
}
