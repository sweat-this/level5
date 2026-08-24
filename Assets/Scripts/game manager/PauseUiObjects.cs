using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pause menu's authored controls. All of them live on the one shared
/// <c>Resources/Prefabs/critical/GameManager.prefab</c> - <see cref="Pause"/> is not per-scene, so
/// this view is not either. <see cref="Footer"/> is intentionally excluded from
/// <see cref="Validate"/>: <c>Pause</c> only shows/hides it and already treats a missing footer as
/// non-fatal (logged, not disabling), which this preserves.
/// </summary>
public class PauseUiObjects : MonoBehaviour
{
    [SerializeField] private GameObject footer;
    [SerializeField] private Image fadeTexture;

    [SerializeField] private Text loadSceneText;
    [SerializeField] private Text loadStartScreenText;
    [SerializeField] private Text cancelMenuText;
    [SerializeField] private Text quitGameText;

    [SerializeField] private Button loadSceneButton;
    [SerializeField] private Button loadStartScreenButton;
    [SerializeField] private Button cancelMenuButton;
    [SerializeField] private Button quitGameButton;

    [SerializeField] private Text toggleUiStatsText;
    [SerializeField] private Text toggleMaxStatsText;
    [SerializeField] private Text toggleFpsText;

    public GameObject Footer => footer;
    public Image FadeTexture => fadeTexture;

    public Text LoadSceneText => loadSceneText;
    public Text LoadStartScreenText => loadStartScreenText;
    public Text CancelMenuText => cancelMenuText;
    public Text QuitGameText => quitGameText;

    public Button LoadSceneButton => loadSceneButton;
    public Button LoadStartScreenButton => loadStartScreenButton;
    public Button CancelMenuButton => cancelMenuButton;
    public Button QuitGameButton => quitGameButton;

    public Text ToggleUiStatsText => toggleUiStatsText;
    public Text ToggleMaxStatsText => toggleMaxStatsText;
    public Text ToggleFpsText => toggleFpsText;

    public GameObject ToggleUiStatsObject => toggleUiStatsText != null ? toggleUiStatsText.gameObject : null;
    public GameObject ToggleMaxStatsObject => toggleMaxStatsText != null ? toggleMaxStatsText.gameObject : null;
    public GameObject ToggleFpsObject => toggleFpsText != null ? toggleFpsText.gameObject : null;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (fadeTexture == null) missing.Add("PauseUiObjects.fadeTexture");
        if (loadSceneText == null) missing.Add("PauseUiObjects.loadSceneText");
        if (loadStartScreenText == null) missing.Add("PauseUiObjects.loadStartScreenText");
        if (cancelMenuText == null) missing.Add("PauseUiObjects.cancelMenuText");
        if (quitGameText == null) missing.Add("PauseUiObjects.quitGameText");
        if (loadSceneButton == null) missing.Add("PauseUiObjects.loadSceneButton");
        if (loadStartScreenButton == null) missing.Add("PauseUiObjects.loadStartScreenButton");
        if (cancelMenuButton == null) missing.Add("PauseUiObjects.cancelMenuButton");
        if (quitGameButton == null) missing.Add("PauseUiObjects.quitGameButton");
        if (toggleUiStatsText == null) missing.Add("PauseUiObjects.toggleUiStatsText");
        if (toggleMaxStatsText == null) missing.Add("PauseUiObjects.toggleMaxStatsText");
        if (toggleFpsText == null) missing.Add("PauseUiObjects.toggleFpsText");
        return missing.Count == before;
    }
}
