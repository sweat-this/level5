using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The stats screen's own controls, table objects and display text. <c>mainMenuButton</c>
/// ("main_menu") is this screen's own back button - unlike every other screen's footer, it does not
/// share the canonical "press_start" name, so it is not part of <see cref="MenuFooterUiObjects"/>.
///
/// AUD-092 Phase 2: the eleven display-text references used to be serialized directly on
/// <see cref="StatsManager"/> as legacy <c>Text</c> fields. They are now TMP references owned here,
/// matching every other screen reference on this component (AUD-103).
/// </summary>
public class StatsUiObjects : MonoBehaviour
{
    [SerializeField] private GameObject highScoreTableObject;
    [SerializeField] private GameObject allTimeTableObject;
    [SerializeField] private GameObject highScoresRowsObject;

    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button modeSelectButton;
    [SerializeField] private Button modeSelectOnlineButton;
    [SerializeField] private Button allTimeSelectButton;
    [SerializeField] private Button pageNumberLocalButton;
    [SerializeField] private Button pageNumberOnlineButton;
    [SerializeField] private Button trafficOptionButton;
    [SerializeField] private Button hardcoreOptionButton;
    [SerializeField] private Button enemiesOptionButton;
    [SerializeField] private Button sniperOptionButton;

    [SerializeField] private TextMeshProUGUI modeSelectText;
    [SerializeField] private TextMeshProUGUI modeSelectHardcoreText;
    [SerializeField] private TextMeshProUGUI modeSelectOnlineText;
    [SerializeField] private TextMeshProUGUI pageNumberLocalText;
    [SerializeField] private TextMeshProUGUI pageNumberOnlineText;
    [SerializeField] private TextMeshProUGUI trafficOptionValueText;
    [SerializeField] private TextMeshProUGUI hardcoreOptionValueText;
    [SerializeField] private TextMeshProUGUI enemiesOptionValueText;
    [SerializeField] private TextMeshProUGUI sniperOptionValueText;
    [SerializeField] private TextMeshProUGUI submittedHighscoresText;
    [SerializeField] private TextMeshProUGUI numUnsubmittedHighscoresText;

    public GameObject HighScoreTableObject => highScoreTableObject;
    public GameObject AllTimeTableObject => allTimeTableObject;
    public GameObject HighScoresRowsObject => highScoresRowsObject;

    public Button MainMenuButton => mainMenuButton;
    public Button ModeSelectButton => modeSelectButton;
    public Button ModeSelectOnlineButton => modeSelectOnlineButton;
    public Button AllTimeSelectButton => allTimeSelectButton;
    public Button PageNumberLocalButton => pageNumberLocalButton;
    public Button PageNumberOnlineButton => pageNumberOnlineButton;
    public Button TrafficOptionButton => trafficOptionButton;
    public Button HardcoreOptionButton => hardcoreOptionButton;
    public Button EnemiesOptionButton => enemiesOptionButton;
    public Button SniperOptionButton => sniperOptionButton;

    public TextMeshProUGUI ModeSelectText => modeSelectText;
    public TextMeshProUGUI ModeSelectHardcoreText => modeSelectHardcoreText;
    public TextMeshProUGUI ModeSelectOnlineText => modeSelectOnlineText;
    public TextMeshProUGUI PageNumberLocalText => pageNumberLocalText;
    public TextMeshProUGUI PageNumberOnlineText => pageNumberOnlineText;
    public TextMeshProUGUI TrafficOptionValueText => trafficOptionValueText;
    public TextMeshProUGUI HardcoreOptionValueText => hardcoreOptionValueText;
    public TextMeshProUGUI EnemiesOptionValueText => enemiesOptionValueText;
    public TextMeshProUGUI SniperOptionValueText => sniperOptionValueText;
    public TextMeshProUGUI SubmittedHighscoresText => submittedHighscoresText;
    public TextMeshProUGUI NumUnsubmittedHighscoresText => numUnsubmittedHighscoresText;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (highScoreTableObject == null) missing.Add("StatsUiObjects.highScoreTableObject");
        if (allTimeTableObject == null) missing.Add("StatsUiObjects.allTimeTableObject");
        if (highScoresRowsObject == null) missing.Add("StatsUiObjects.highScoresRowsObject");
        if (mainMenuButton == null) missing.Add("StatsUiObjects.mainMenuButton");
        if (modeSelectButton == null) missing.Add("StatsUiObjects.modeSelectButton");
        if (modeSelectOnlineButton == null) missing.Add("StatsUiObjects.modeSelectOnlineButton");
        if (allTimeSelectButton == null) missing.Add("StatsUiObjects.allTimeSelectButton");
        if (pageNumberLocalButton == null) missing.Add("StatsUiObjects.pageNumberLocalButton");
        if (pageNumberOnlineButton == null) missing.Add("StatsUiObjects.pageNumberOnlineButton");
        if (trafficOptionButton == null) missing.Add("StatsUiObjects.trafficOptionButton");
        if (hardcoreOptionButton == null) missing.Add("StatsUiObjects.hardcoreOptionButton");
        if (enemiesOptionButton == null) missing.Add("StatsUiObjects.enemiesOptionButton");
        if (sniperOptionButton == null) missing.Add("StatsUiObjects.sniperOptionButton");
        if (modeSelectText == null) missing.Add("StatsUiObjects.modeSelectText");
        if (modeSelectHardcoreText == null) missing.Add("StatsUiObjects.modeSelectHardcoreText");
        if (modeSelectOnlineText == null) missing.Add("StatsUiObjects.modeSelectOnlineText");
        if (pageNumberLocalText == null) missing.Add("StatsUiObjects.pageNumberLocalText");
        if (pageNumberOnlineText == null) missing.Add("StatsUiObjects.pageNumberOnlineText");
        if (trafficOptionValueText == null) missing.Add("StatsUiObjects.trafficOptionValueText");
        if (hardcoreOptionValueText == null) missing.Add("StatsUiObjects.hardcoreOptionValueText");
        if (enemiesOptionValueText == null) missing.Add("StatsUiObjects.enemiesOptionValueText");
        if (sniperOptionValueText == null) missing.Add("StatsUiObjects.sniperOptionValueText");
        if (submittedHighscoresText == null) missing.Add("StatsUiObjects.submittedHighscoresText");
        if (numUnsubmittedHighscoresText == null) missing.Add("StatsUiObjects.numUnsubmittedHighscoresText");
        return missing.Count == before;
    }
}
