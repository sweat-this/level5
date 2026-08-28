using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The credits screen's own controls: the bug-report input field and its submit button, plus an
/// "options" button distinct from the shared footer's "options_menu" (both call
/// <c>LoadOptionsMenu</c> today - preserved as-is, not collapsed into one). Footer buttons live on
/// <see cref="MenuFooterUiObjects"/>, not here.
///
/// AUD-092 Phase 4B: <see cref="reportInputField"/> is <see cref="TMP_InputField"/> - the legacy
/// <c>UnityEngine.UI.InputField</c> it used to be was migrated along with its two structural Text
/// dependencies (<c>textComponent</c>/<c>placeholder</c>), completing the Credits screen's TMP
/// migration that AUD-092 Phase 4A deliberately left this one field out of.
/// </summary>
public class CreditsUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_InputField reportInputField;
    [SerializeField] private Button submitReportButton;
    [SerializeField] private Button optionsButton;

    public TMP_InputField ReportInputField => reportInputField;
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
