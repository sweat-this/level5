using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The credits screen's own controls: the bug-report input field and its submit button, plus an
/// "options" button distinct from the shared footer's "options_menu" (both call
/// <c>LoadOptionsMenu</c> today - preserved as-is, not collapsed into one). Footer buttons live on
/// <see cref="MenuFooterUiObjects"/>, not here.
/// </summary>
public class CreditsUiObjects : MonoBehaviour
{
    [SerializeField] private InputField reportInputField;
    [SerializeField] private Button submitReportButton;
    [SerializeField] private Button optionsButton;

    public InputField ReportInputField => reportInputField;
    public Button SubmitReportButton => submitReportButton;
    public Button OptionsButton => optionsButton;
    public GameObject SubmitReportButtonObject => submitReportButton != null ? submitReportButton.gameObject : null;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (reportInputField == null) missing.Add("CreditsUiObjects.reportInputField");
        if (submitReportButton == null) missing.Add("CreditsUiObjects.submitReportButton");
        if (optionsButton == null) missing.Add("CreditsUiObjects.optionsButton");
        return missing.Count == before;
    }
}
