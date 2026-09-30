using Assets.Scripts.Utility;
using System.Collections.Generic;
using Level5.Core.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The production Local Versus screen (<c>level_00_local_versus</c>): create a local alternating
/// series, see every stored one, and take each turn in order.
///
/// A thin uGUI binding over <see cref="LocalVersusScreenModel"/>, which delegates to
/// <see cref="LocalVersusFlow"/>, <see cref="VersusRuntime"/>'s coordinator and
/// <see cref="VersusLauncher"/>. It constructs no series, saves nothing, compares no results and
/// tracks neither wins nor turns: it renders what the loaded series says.
///
/// Fully local. It reads no Backend V2 session and calls no remote client, so it works signed out,
/// offline, and leaves any existing online session untouched. Correspondence is a separate screen.
/// </summary>
public class LocalVersusController : MonoBehaviour
{
    [SerializeField] private LocalVersusUiObjects ui;

    private LocalVersusScreenModel model;
    private bool initialized;

    /// <summary>The screen's selection state. Exposed so tests and tools can drive the same seam the buttons do.</summary>
    public LocalVersusScreenModel Model => model;

    void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        if (initialized)
        {
            RegisterButtonCallbacks();
        }
    }

    void OnDisable()
    {
        UnregisterButtonCallbacks();
        PlayerControlsProvider.DisableMenuMaps();
    }

    void Start()
    {
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            Debug.LogError(
                "LocalVersusController is missing required serialized UI references and will be disabled: "
                    + string.Join(", ", missing.ToArray()),
                this);
            enabled = false;
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();

        model = new LocalVersusScreenModel(CurrentUnlockSnapshot, ProjectCharacters);
        RegisterButtonCallbacks();
        initialized = true;

        // Lists the stored series, opens on the one a just-finished turn belonged to, and consumes
        // that transient hint. With no hint (first visit, or the app restarted) the first
        // unfinished series is shown - the stored series is the only authority.
        model.Open();
        Render();
    }

    void Update()
    {
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    /// <summary>True once <see cref="ui"/> carries every reference this screen needs.</summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        if (ui == null)
        {
            missing.Add("LocalVersusController.ui");
            return false;
        }

        return ui.Validate(missing);
    }

    private void RegisterButtonCallbacks()
    {
        if (ui == null)
        {
            return;
        }

        UiSelectionAdapter.RegisterButton(ui.ModeButton, OnModeClicked);
        UiSelectionAdapter.RegisterButton(ui.RulesetButton, OnRulesetClicked);
        UiSelectionAdapter.RegisterButton(ui.FormatButton, OnFormatClicked);
        UiSelectionAdapter.RegisterButton(ui.CreateButton, OnCreateClicked);
        UiSelectionAdapter.RegisterButton(ui.SeriesSelectButton, OnSeriesSelectClicked);
        UiSelectionAdapter.RegisterButton(ui.CharacterButton, OnCharacterClicked);
        UiSelectionAdapter.RegisterButton(ui.Character2Button, OnCharacter2Clicked);
        UiSelectionAdapter.RegisterButton(ui.LevelButton, OnLevelClicked);
        UiSelectionAdapter.RegisterButton(ui.PlayTurnButton, OnPlayTurnClicked);
        UiSelectionAdapter.RegisterButton(ui.BackButton, OnBackClicked);
    }

    private void UnregisterButtonCallbacks()
    {
        if (ui == null)
        {
            return;
        }

        UiSelectionAdapter.UnregisterButton(ui.ModeButton, OnModeClicked);
        UiSelectionAdapter.UnregisterButton(ui.RulesetButton, OnRulesetClicked);
        UiSelectionAdapter.UnregisterButton(ui.FormatButton, OnFormatClicked);
        UiSelectionAdapter.UnregisterButton(ui.CreateButton, OnCreateClicked);
        UiSelectionAdapter.UnregisterButton(ui.SeriesSelectButton, OnSeriesSelectClicked);
        UiSelectionAdapter.UnregisterButton(ui.CharacterButton, OnCharacterClicked);
        UiSelectionAdapter.UnregisterButton(ui.Character2Button, OnCharacter2Clicked);
        UiSelectionAdapter.UnregisterButton(ui.LevelButton, OnLevelClicked);
        UiSelectionAdapter.UnregisterButton(ui.PlayTurnButton, OnPlayTurnClicked);
        UiSelectionAdapter.UnregisterButton(ui.BackButton, OnBackClicked);
    }

    private void OnModeClicked()
    {
        model.CycleMode();
        Render();
    }

    private void OnRulesetClicked()
    {
        model.CycleRuleset();
        Render();
    }

    private void OnFormatClicked()
    {
        model.CycleFormat();
        Render();
    }

    private void OnCreateClicked()
    {
        model.Create(ui.Player1NameInputField.text, ui.Player2NameInputField.text);
        Render();
    }

    private void OnSeriesSelectClicked()
    {
        model.CycleSeries();
        Render();
    }

    private void OnCharacterClicked()
    {
        model.CycleCharacter();
        Render();
    }

    private void OnCharacter2Clicked()
    {
        model.CycleSecondCharacter();
        Render();
    }

    private void OnLevelClicked()
    {
        model.CycleLevel();
        Render();
    }

    private void OnPlayTurnClicked()
    {
        // On success the launcher has already loaded the gameplay scene (for a simultaneous series,
        // one two-human match); on failure the model holds the reason and the screen stays put.
        model.PlayTurn();
        Render();
    }

    private void OnBackClicked()
    {
        SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_start);
    }

    private void Render()
    {
        ui.ModeText.text = model.ModeText;
        ui.RulesetText.text = model.RulesetText;
        ui.FormatText.text = model.FormatText;
        ui.CreateMessageText.text = model.CreateMessage;
        ui.SeriesListText.text = model.ListText;
        ui.SeriesSelectText.text = model.SeriesSelectText;
        ui.SeriesDetailText.text = model.DetailText;
        ui.CharacterText.text = model.CharacterText;
        ui.Character2Text.text = model.SecondCharacterText;
        ui.LevelText.text = model.LevelText;
        ui.PlayTurnText.text = model.PlayTurnText;
        ui.TurnMessageText.text = model.TurnMessage;

        ui.SeriesSelectButton.interactable = model.Summaries.Count > 1;
        // Player 2's selector exists only for a simultaneous series; an alternating series keeps the
        // single character selector and the screen it always had.
        ui.Character2Button.gameObject.SetActive(model.IsSimultaneousSelected);
        ui.Character2Button.interactable = model.HasPlayableTurn && model.Characters.Count > 1;
        ui.CharacterButton.interactable = model.HasPlayableTurn && model.Characters.Count > 1;
        ui.LevelButton.interactable = model.HasPlayableTurn && model.Levels.Count > 1;
        ui.PlayTurnButton.interactable = model.CanPlayTurn;
        ui.RulesetButton.interactable = model.Rulesets.Count > 1;
        ui.CreateButton.interactable = model.SelectedRuleset != null;

        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    /// <summary>
    /// Deterministic default: the turn button when a turn can be played, otherwise the series
    /// selector when there is one, otherwise Create.
    /// </summary>
    private GameObject GetDefaultSelectedButton()
    {
        if (ui == null || model == null)
        {
            return null;
        }

        Selectable preferred = PreferredSelectable();
        GameObject current = UiSelectionAdapter.CurrentSelected;
        if (current != null && current.activeInHierarchy)
        {
            Selectable selectable = current.GetComponent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
            {
                return current;
            }
        }

        return preferred != null ? preferred.gameObject : null;
    }

    private Selectable PreferredSelectable()
    {
        if (ui.PlayTurnButton.IsInteractable())
        {
            return ui.PlayTurnButton;
        }

        if (ui.SeriesSelectButton.IsInteractable())
        {
            return ui.SeriesSelectButton;
        }

        return ui.CreateButton.IsInteractable() ? ui.CreateButton : (Selectable)ui.BackButton;
    }

    /// <summary>The same current-account snapshot every launch path builds, via <see cref="UnlockSnapshotBuilder"/>.</summary>
    private static UnlockSnapshot CurrentUnlockSnapshot()
    {
        IReadOnlyList<CharacterProfile> primary = LoadedData.instance != null ? LoadedData.instance.PlayerSelectedData : null;
        IReadOnlyList<CharacterProfile> cpu = LoadedData.instance != null ? LoadedData.instance.CpuPlayerSelectedData : null;
        return UnlockSnapshotBuilder.Build(primary, cpu, MatchCatalogs.Levels);
    }

    private static IReadOnlyList<Level5.Core.PlayerSelection.CharacterSelectOption> ProjectCharacters(UnlockSnapshot unlock)
    {
        IReadOnlyList<CharacterProfile> primary = LoadedData.instance != null ? LoadedData.instance.PlayerSelectedData : null;
        IReadOnlyList<CharacterProfile> cpu = LoadedData.instance != null ? LoadedData.instance.CpuPlayerSelectedData : null;
        return PlayerSelectCatalogAdapter.Project(primary, cpu, unlock).PrimaryOptions;
    }
}
