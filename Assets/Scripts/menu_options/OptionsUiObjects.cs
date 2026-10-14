using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The options screen's audio controls, control-scheme buttons and reference panels.
/// Footer buttons live on <see cref="MenuFooterUiObjects"/>, not here.
/// </summary>
public class OptionsUiObjects : MonoBehaviour
{
    [SerializeField] private Button keyboardOnlyButton;
    [SerializeField] private Button keyboardMouseButton;
    [SerializeField] private Button gamepadButton;
    [SerializeField] private Button touchButton;

    [SerializeField] private GameObject keyboardOnlyObject;
    [SerializeField] private GameObject keyboardMouseObject;
    [SerializeField] private GameObject gamepadObject;
    [SerializeField] private GameObject touchObject;

    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text masterLabel;
    [SerializeField] private TMP_Text musicLabel;
    [SerializeField] private TMP_Text sfxLabel;

    public Slider MasterSlider => masterSlider;
    public Slider MusicSlider => musicSlider;
    public Slider SfxSlider => sfxSlider;

    public void RefreshAudioControls()
    {
        Level5.Core.PlayerSettings settings = PlayerSettingsRuntime.Current;
        masterSlider.SetValueWithoutNotify(settings.Master * 100f);
        musicSlider.SetValueWithoutNotify(settings.Music * 100f);
        sfxSlider.SetValueWithoutNotify(settings.Sfx * 100f);
        RefreshAudioLabels();
    }

    public void RefreshAudioLabels()
    {
        masterLabel.text = $"Master {masterSlider.value:0}%";
        musicLabel.text = $"Music {musicSlider.value:0}%";
        sfxLabel.text = $"SFX {sfxSlider.value:0}%";
    }

    public Button KeyboardOnlyButton => keyboardOnlyButton;
    public Button KeyboardMouseButton => keyboardMouseButton;
    public Button GamepadButton => gamepadButton;
    public Button TouchButton => touchButton;

    public GameObject KeyboardOnlyObject => keyboardOnlyObject;
    public GameObject KeyboardMouseObject => keyboardMouseObject;
    public GameObject GamepadObject => gamepadObject;
    public GameObject TouchObject => touchObject;

    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (keyboardOnlyButton == null) missing.Add("OptionsUiObjects.keyboardOnlyButton");
        if (keyboardMouseButton == null) missing.Add("OptionsUiObjects.keyboardMouseButton");
        if (gamepadButton == null) missing.Add("OptionsUiObjects.gamepadButton");
        if (touchButton == null) missing.Add("OptionsUiObjects.touchButton");
        if (keyboardOnlyObject == null) missing.Add("OptionsUiObjects.keyboardOnlyObject");
        if (keyboardMouseObject == null) missing.Add("OptionsUiObjects.keyboardMouseObject");
        if (gamepadObject == null) missing.Add("OptionsUiObjects.gamepadObject");
        if (touchObject == null) missing.Add("OptionsUiObjects.touchObject");
        if (masterSlider == null) missing.Add("OptionsUiObjects.masterSlider");
        if (musicSlider == null) missing.Add("OptionsUiObjects.musicSlider");
        if (sfxSlider == null) missing.Add("OptionsUiObjects.sfxSlider");
        if (masterLabel == null) missing.Add("OptionsUiObjects.masterLabel");
        if (musicLabel == null) missing.Add("OptionsUiObjects.musicLabel");
        if (sfxLabel == null) missing.Add("OptionsUiObjects.sfxLabel");
        if (masterSlider != null && musicSlider != null && sfxSlider != null
            && (masterSlider == musicSlider || masterSlider == sfxSlider || musicSlider == sfxSlider))
            missing.Add("OptionsUiObjects audio sliders must be distinct");
        return missing.Count == before;
    }
}
