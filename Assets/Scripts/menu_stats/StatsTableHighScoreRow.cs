using TMPro;
using UnityEngine;

/// <summary>
/// One row of the high score table.
///
/// The <see cref="TextMeshProUGUI"/> references are serialized on the prefab (AUD-092 Phase 2; legacy
/// <c>Text</c> before that). The values are written once, when the row is given data, not pushed from
/// <c>Update</c> every frame (AUD-108).
///
/// <see cref="StatsTableHighScoreRow"/> is also used as a data-only container by database/API query
/// paths, which add this component to a throwaway GameObject purely to carry the string fields below -
/// those instances have no label references and never call <see cref="Bind"/>.
/// </summary>
public class StatsTableHighScoreRow : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI userNameLabel;
    [SerializeField] public TextMeshProUGUI scoreLabel;
    [SerializeField] public TextMeshProUGUI characterLabel;
    [SerializeField] public TextMeshProUGUI levelLabel;
    [SerializeField] public TextMeshProUGUI dateLabel;
    [SerializeField] public TextMeshProUGUI hardcoreLabel;

    [SerializeField]
    public string Score;
    [SerializeField]
    public string Character;
    [SerializeField]
    public string Level;
    [SerializeField]
    public string Date;
    [SerializeField]
    public string HardcoreEnabled;
    [SerializeField]
    public string UserName;
    [SerializeField]
    public string TrafficEnabled;
    [SerializeField]
    public string EnemiesEnabled;
    [SerializeField]
    public string Platform;

    /// <summary>
    /// Pushes the current field values into the row's <see cref="TextMeshProUGUI"/> labels. Called when
    /// the row is given data, not every frame. A data-only instance (no labels wired at all) is a silent
    /// no-op; a row with only some labels wired logs, since that's not the data-only case - it means a
    /// display row is missing a reference it should have.
    /// </summary>
    public void Bind()
    {
        bool anyLabelWired = userNameLabel != null || scoreLabel != null || characterLabel != null
            || levelLabel != null || dateLabel != null || hardcoreLabel != null;
        if (anyLabelWired)
        {
            WarnIfMissing(userNameLabel, nameof(userNameLabel));
            WarnIfMissing(scoreLabel, nameof(scoreLabel));
            WarnIfMissing(characterLabel, nameof(characterLabel));
            WarnIfMissing(levelLabel, nameof(levelLabel));
            WarnIfMissing(dateLabel, nameof(dateLabel));
            WarnIfMissing(hardcoreLabel, nameof(hardcoreLabel));
        }

        SetText(userNameLabel, UserName);
        SetText(scoreLabel, Score);
        SetText(characterLabel, Character);
        SetText(levelLabel, Level);
        SetText(dateLabel, Date);
        SetText(hardcoreLabel, HardcoreEnabled);
    }

    private void WarnIfMissing(TextMeshProUGUI label, string fieldName)
    {
        if (label == null)
        {
            Debug.LogWarning("StatsTableHighScoreRow." + fieldName + " is not wired.", this);
        }
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    public void setRowValues(string scor, string charact, string lvl, string dat, string hrdcor, string uname)
    {
        UserName = uname;
        Score = scor;
        Character = charact;
        Level = lvl;
        Date = dat;
        HardcoreEnabled = hrdcor;
    }
}
