using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The seven footer command buttons shared by every menu screen (press_start, stats_menu,
/// options_menu, credits_menu, update_menu, account_menu, quit_game).
///
/// This is a passive reference container, not a controller: it holds the buttons, nothing more.
/// Each screen manager decides which of these buttons it actually requires and what each one does
/// - registering its own callback on the button reference this exposes. That split is deliberate
/// (see docs/ui-input-architecture.md, AUD-104): press_start on the start screen starts a match, not
/// "load the start scene", and the account/progression screens have their own leave-screen semantics.
/// A shared behaviour-owning footer would have had to know all of that, which is exactly the coupling
/// this avoids.
/// </summary>
public class MenuFooterUiObjects : MonoBehaviour
{
    [SerializeField] private Button startOrPlayButton;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button progressionButton;
    [SerializeField] private Button accountButton;
    [SerializeField] private Button quitButton;

    public Button StartOrPlayButton => startOrPlayButton;
    public Button StatsButton => statsButton;
    public Button OptionsButton => optionsButton;
    public Button CreditsButton => creditsButton;
    public Button ProgressionButton => progressionButton;
    public Button AccountButton => accountButton;
    public Button QuitButton => quitButton;

    /// <summary>
    /// Appends "MenuFooterUiObjects.&lt;fieldName&gt;" to <paramref name="missing"/> for each of the
    /// caller-supplied fields that is unassigned. Screens do not all use the same subset of footer
    /// buttons (the account create/login forms show only accountButton), so the caller names which
    /// of its own fields are required rather than this type assuming all seven always are.
    /// </summary>
    public bool Validate(List<string> missing, params (Button value, string name)[] required)
    {
        int before = missing.Count;
        foreach ((Button value, string name) field in required)
        {
            if (field.value == null)
            {
                missing.Add("MenuFooterUiObjects." + field.name);
            }
        }

        return missing.Count == before;
    }
}
