using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-092 Phase 6C: the permanent typed UI view for the shared tip dialogue
/// (<c>Assets/Resources/Prefabs/misc/confirm_tip.prefab</c>), the TextMeshProUGUI/Button counterpart of
/// the four legacy <c>UnityEngine.UI.Text</c> components and the two GameObject.Find-resolved Buttons
/// <see cref="StartScreenTipDialogueManager"/> used to depend on directly. Composed on the same
/// GameObject as <see cref="StartScreenTipDialogueManager"/> so that manager has exactly one serialized
/// reference to resolve its UI from, instead of reaching for its own children by name at runtime.
/// </summary>
public class TipDialogueUiObjects : MonoBehaviour
{
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text tip;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    public TMP_Text Header => header;
    public TMP_Text Tip => tip;
    public Button NextButton => nextButton;
    public Button CloseButton => closeButton;

    /// <summary>True once every field above resolves; otherwise every unresolved field name is appended to <paramref name="missing"/>.</summary>
    public bool Validate(List<string> missing)
    {
        int before = missing.Count;
        if (header == null)
        {
            missing.Add("TipDialogueUiObjects.header");
        }

        if (tip == null)
        {
            missing.Add("TipDialogueUiObjects.tip");
        }

        if (nextButton == null)
        {
            missing.Add("TipDialogueUiObjects.nextButton");
        }

        if (closeButton == null)
        {
            missing.Add("TipDialogueUiObjects.closeButton");
        }

        return missing.Count == before;
    }
}
