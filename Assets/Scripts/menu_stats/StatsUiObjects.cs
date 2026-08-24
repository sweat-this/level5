using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The stats screen's own controls and table objects. <c>mainMenuButton</c> ("main_menu") is this
/// screen's own back button - unlike every other screen's footer, it does not share the canonical
/// "press_start" name, so it is not part of <see cref="MenuFooterUiObjects"/>.
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
        return missing.Count == before;
    }
}
