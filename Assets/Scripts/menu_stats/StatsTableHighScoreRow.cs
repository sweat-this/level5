using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the high score table.
///
/// The <see cref="Text"/> references are serialized on the prefab. They used to be bound in
/// <c>Start</c> by <c>transform.GetChild(0..5)</c>, which silently reassigned every column if
/// anyone reordered the row's children, and the six values used to be pushed into those fields
/// from <c>Update</c> every frame whether or not anything had changed (AUD-108). The values are
/// now written once, when the row is given data.
///
/// The child-index fallback is kept for rows whose references are not wired yet, so a prefab that
/// has not been re-saved still renders instead of throwing. It logs, so the gap is visible.
/// </summary>
public class StatsTableHighScoreRow : MonoBehaviour
{
    [SerializeField] public Text userNameText;
    [SerializeField] public Text scoreText;
    [SerializeField] public Text characterText;
    [SerializeField] public Text levelText;
    [SerializeField] public Text dateText;
    [SerializeField] public Text hardcoreText;

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

    private bool textReferencesResolved;

    // No Awake resolve on purpose. DBHelper carries query results as StatsTableHighScoreRow
    // components added to its own GameObject, so most instances of this component are data only
    // and have no Text children to find. Resolution happens the first time a row is actually
    // bound to the table.

    /// <summary>
    /// Falls back to the old child-index binding for any reference the prefab does not supply.
    /// Runs once.
    /// </summary>
    private void ResolveTextReferences()
    {
        if (textReferencesResolved)
        {
            return;
        }

        textReferencesResolved = true;

        userNameText = userNameText != null ? userNameText : TextAtChild(0);
        scoreText = scoreText != null ? scoreText : TextAtChild(1);
        characterText = characterText != null ? characterText : TextAtChild(2);
        levelText = levelText != null ? levelText : TextAtChild(3);
        dateText = dateText != null ? dateText : TextAtChild(4);
        hardcoreText = hardcoreText != null ? hardcoreText : TextAtChild(5);
    }

    private Text TextAtChild(int index)
    {
        if (index < 0 || index >= transform.childCount)
        {
            Debug.LogWarning(
                "StatsTableHighScoreRow has no serialized Text for column " + index
                    + " and no child at that index.",
                this);
            return null;
        }

        return transform.GetChild(index).GetComponent<Text>();
    }

    /// <summary>
    /// Pushes the current field values into the row's <see cref="Text"/> components. Called when
    /// the row is given data, not every frame.
    /// </summary>
    public void Bind()
    {
        ResolveTextReferences();

        SetText(userNameText, UserName);
        SetText(scoreText, Score);
        SetText(characterText, Character);
        SetText(levelText, Level);
        SetText(dateText, Date);
        SetText(hardcoreText, HardcoreEnabled);
    }

    private static void SetText(Text target, string value)
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
