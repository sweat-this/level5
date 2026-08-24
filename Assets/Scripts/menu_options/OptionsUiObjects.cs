using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The options screen's own controls: the four control-scheme buttons and the four panels each one
/// shows. Footer buttons live on <see cref="MenuFooterUiObjects"/>, not here.
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
        return missing.Count == before;
    }
}
