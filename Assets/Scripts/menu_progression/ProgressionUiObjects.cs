using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The progression screen's own controls and display text. <c>confirmButton</c>/<c>cancelButton</c>
/// live inside <see cref="confirmationDialogueBox"/>, which starts inactive - serializing them
/// directly here is what makes resolving them through an active-object-only lookup unnecessary.
/// start/stats/quit footer buttons live on <see cref="MenuFooterUiObjects"/>, not here.
///
/// AUD-092 Phase 3: the 17 display-text references and the player portrait image used to be resolved
/// at runtime by <see cref="ProgressionManager"/> via <c>SceneObjects.Find&lt;Text/Image&gt;</c>. They
/// are now TMP/Image references owned here, matching every other reference on this component and the
/// <see cref="StatsUiObjects"/> precedent (AUD-103).
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

    [SerializeField] private Image playerSelectOptionImage;

    [SerializeField] private TextMeshProUGUI playerSelectOptionText;
    [SerializeField] private TextMeshProUGUI playerProgressionStatsText;
    [SerializeField] private TextMeshProUGUI playerProgressionUpdatePointsText;
    [SerializeField] private TextMeshProUGUI progression3Accuracy;
    [SerializeField] private TextMeshProUGUI progression4Accuracy;
    [SerializeField] private TextMeshProUGUI progression7Accuracy;
    [SerializeField] private TextMeshProUGUI progressionRange;
    [SerializeField] private TextMeshProUGUI progressionRelease;
    [SerializeField] private TextMeshProUGUI progressionSpeed;
    [SerializeField] private TextMeshProUGUI progressionJump;
    [SerializeField] private TextMeshProUGUI progressionLuck;
    [SerializeField] private TextMeshProUGUI bonusReleaseText;
    [SerializeField] private TextMeshProUGUI bonusRangeText;
    [SerializeField] private TextMeshProUGUI bonusLuckText;
    [SerializeField] private TextMeshProUGUI addTo3Text;
    [SerializeField] private TextMeshProUGUI addTo4Text;
    [SerializeField] private TextMeshProUGUI addTo7Text;

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

    public Image PlayerSelectOptionImage => playerSelectOptionImage;

    public TextMeshProUGUI PlayerSelectOptionText => playerSelectOptionText;
    public TextMeshProUGUI PlayerProgressionStatsText => playerProgressionStatsText;
    public TextMeshProUGUI PlayerProgressionUpdatePointsText => playerProgressionUpdatePointsText;
    public TextMeshProUGUI Progression3Accuracy => progression3Accuracy;
    public TextMeshProUGUI Progression4Accuracy => progression4Accuracy;
    public TextMeshProUGUI Progression7Accuracy => progression7Accuracy;
    public TextMeshProUGUI ProgressionRange => progressionRange;
    public TextMeshProUGUI ProgressionRelease => progressionRelease;
    public TextMeshProUGUI ProgressionSpeed => progressionSpeed;
    public TextMeshProUGUI ProgressionJump => progressionJump;
    public TextMeshProUGUI ProgressionLuck => progressionLuck;
    public TextMeshProUGUI BonusReleaseText => bonusReleaseText;
    public TextMeshProUGUI BonusRangeText => bonusRangeText;
    public TextMeshProUGUI BonusLuckText => bonusLuckText;
    public TextMeshProUGUI AddTo3Text => addTo3Text;
    public TextMeshProUGUI AddTo4Text => addTo4Text;
    public TextMeshProUGUI AddTo7Text => addTo7Text;

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
        if (playerSelectOptionImage == null) missing.Add("ProgressionUiObjects.playerSelectOptionImage");
        if (playerSelectOptionText == null) missing.Add("ProgressionUiObjects.playerSelectOptionText");
        if (playerProgressionStatsText == null) missing.Add("ProgressionUiObjects.playerProgressionStatsText");
        if (playerProgressionUpdatePointsText == null) missing.Add("ProgressionUiObjects.playerProgressionUpdatePointsText");
        if (progression3Accuracy == null) missing.Add("ProgressionUiObjects.progression3Accuracy");
        if (progression4Accuracy == null) missing.Add("ProgressionUiObjects.progression4Accuracy");
        if (progression7Accuracy == null) missing.Add("ProgressionUiObjects.progression7Accuracy");
        if (progressionRange == null) missing.Add("ProgressionUiObjects.progressionRange");
        if (progressionRelease == null) missing.Add("ProgressionUiObjects.progressionRelease");
        if (progressionSpeed == null) missing.Add("ProgressionUiObjects.progressionSpeed");
        if (progressionJump == null) missing.Add("ProgressionUiObjects.progressionJump");
        if (progressionLuck == null) missing.Add("ProgressionUiObjects.progressionLuck");
        if (bonusReleaseText == null) missing.Add("ProgressionUiObjects.bonusReleaseText");
        if (bonusRangeText == null) missing.Add("ProgressionUiObjects.bonusRangeText");
        if (bonusLuckText == null) missing.Add("ProgressionUiObjects.bonusLuckText");
        if (addTo3Text == null) missing.Add("ProgressionUiObjects.addTo3Text");
        if (addTo4Text == null) missing.Add("ProgressionUiObjects.addTo4Text");
        if (addTo7Text == null) missing.Add("ProgressionUiObjects.addTo7Text");
        return missing.Count == before;
    }
}
