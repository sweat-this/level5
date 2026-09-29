using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Serialized references for the production Local Versus screen (<c>level_00_local_versus</c>):
/// the create form (two display names, ruleset, format), the stored-series list and detail, and the
/// per-turn character/arena/play controls. A passive view container - <see cref="LocalVersusController"/>
/// decides what each control does.
/// </summary>
public class LocalVersusUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_InputField player1NameInputField;
    [SerializeField] private TMP_InputField player2NameInputField;
    [SerializeField] private Button rulesetButton;
    [SerializeField] private TMP_Text rulesetText;
    [SerializeField] private Button formatButton;
    [SerializeField] private TMP_Text formatText;
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text createMessageText;

    [SerializeField] private TMP_Text seriesListText;
    [SerializeField] private Button seriesSelectButton;
    [SerializeField] private TMP_Text seriesSelectText;
    [SerializeField] private TMP_Text seriesDetailText;

    [SerializeField] private Button characterButton;
    [SerializeField] private TMP_Text characterText;
    [SerializeField] private Button levelButton;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button playTurnButton;
    [SerializeField] private TMP_Text playTurnText;
    [SerializeField] private TMP_Text turnMessageText;

    [SerializeField] private Button backButton;

    public TMP_InputField Player1NameInputField => player1NameInputField;
    public TMP_InputField Player2NameInputField => player2NameInputField;
    public Button RulesetButton => rulesetButton;
    public TMP_Text RulesetText => rulesetText;
    public Button FormatButton => formatButton;
    public TMP_Text FormatText => formatText;
    public Button CreateButton => createButton;
    public TMP_Text CreateMessageText => createMessageText;

    public TMP_Text SeriesListText => seriesListText;
    public Button SeriesSelectButton => seriesSelectButton;
    public TMP_Text SeriesSelectText => seriesSelectText;
    public TMP_Text SeriesDetailText => seriesDetailText;

    public Button CharacterButton => characterButton;
    public TMP_Text CharacterText => characterText;
    public Button LevelButton => levelButton;
    public TMP_Text LevelText => levelText;
    public Button PlayTurnButton => playTurnButton;
    public TMP_Text PlayTurnText => playTurnText;
    public TMP_Text TurnMessageText => turnMessageText;

    public Button BackButton => backButton;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (player1NameInputField == null) missing.Add("LocalVersusUiObjects.player1NameInputField");
        if (player2NameInputField == null) missing.Add("LocalVersusUiObjects.player2NameInputField");
        if (rulesetButton == null) missing.Add("LocalVersusUiObjects.rulesetButton");
        if (rulesetText == null) missing.Add("LocalVersusUiObjects.rulesetText");
        if (formatButton == null) missing.Add("LocalVersusUiObjects.formatButton");
        if (formatText == null) missing.Add("LocalVersusUiObjects.formatText");
        if (createButton == null) missing.Add("LocalVersusUiObjects.createButton");
        if (createMessageText == null) missing.Add("LocalVersusUiObjects.createMessageText");
        if (seriesListText == null) missing.Add("LocalVersusUiObjects.seriesListText");
        if (seriesSelectButton == null) missing.Add("LocalVersusUiObjects.seriesSelectButton");
        if (seriesSelectText == null) missing.Add("LocalVersusUiObjects.seriesSelectText");
        if (seriesDetailText == null) missing.Add("LocalVersusUiObjects.seriesDetailText");
        if (characterButton == null) missing.Add("LocalVersusUiObjects.characterButton");
        if (characterText == null) missing.Add("LocalVersusUiObjects.characterText");
        if (levelButton == null) missing.Add("LocalVersusUiObjects.levelButton");
        if (levelText == null) missing.Add("LocalVersusUiObjects.levelText");
        if (playTurnButton == null) missing.Add("LocalVersusUiObjects.playTurnButton");
        if (playTurnText == null) missing.Add("LocalVersusUiObjects.playTurnText");
        if (turnMessageText == null) missing.Add("LocalVersusUiObjects.turnMessageText");
        if (backButton == null) missing.Add("LocalVersusUiObjects.backButton");
        return missing.Count == before;
    }
}
