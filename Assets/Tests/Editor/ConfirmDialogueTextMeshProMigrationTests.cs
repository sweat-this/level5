using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 3: confirm_update.prefab's two directly-owned legacy Text components (confirm_button,
/// cancel_button) were migrated to TextMeshProUGUI. confirm_update.prefab is SHARED - nested inside
/// progression_manager.prefab and separately held by DialogueManager.prefab as a runtime Instantiate()
/// template for Start/Account flows - so this file's tests exercise the prefab directly rather than
/// through either consumer, matching <see cref="Level5ProjectValidator.CollectConfirmationDialogueTextRenderingContractErrors"/>'s
/// own framing.
/// </summary>
public class ConfirmDialogueTextMeshProMigrationTests
{
    private const string ConfirmUpdatePrefabPath = "Assets/Resources/Prefabs/misc/confirm_update.prefab";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private readonly List<Object> _instantiated = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object instance in _instantiated)
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        _instantiated.Clear();
    }

    [Test]
    public void ConfirmUpdateUsesTextMeshProOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectConfirmationDialogueTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void ConfirmUpdateButtonsHaveValidTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmUpdatePrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            Assert.That(
                selectable.targetGraphic,
                Is.Not.Null,
                selectable.gameObject.name + " (" + selectable.GetType().Name + ") has a null targetGraphic.");
        }
    }

    [Test]
    public void ConfirmUpdateUsesNeonPixelFont()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmUpdatePrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        TextMeshProUGUI[] tmpComponents = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert.That(tmpComponents, Is.Not.Empty);

        foreach (TextMeshProUGUI tmp in tmpComponents)
        {
            Assert.That(tmp.font, Is.EqualTo(neonPixel), tmp.gameObject.name + " does not use the shared Neon Pixel-7 SDF font asset.");
        }
    }

    [Test]
    public void ConfirmUpdateKeepsConfirmDialogueComponent()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmUpdatePrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        Assert.That(prefabRoot.GetComponentInChildren<ConfirmDialogue>(true), Is.Not.Null);
    }

    [Test]
    public void ConfirmButtonSetsYesResult()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmUpdatePrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        _instantiated.Add(instance);

        ConfirmDialogue dialogue = instance.GetComponentInChildren<ConfirmDialogue>(true);
        Assume.That(dialogue, Is.Not.Null);
        MenuTextConversion.InvokeAwake(dialogue);
        Assume.That(dialogue.ConfirmButton, Is.Not.Null);

        dialogue.ConfirmButton.onClick.Invoke();

        Assert.That(dialogue.result, Is.EqualTo(dialogue.YES));
    }

    [Test]
    public void CancelButtonSetsCancelResult()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmUpdatePrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        _instantiated.Add(instance);

        ConfirmDialogue dialogue = instance.GetComponentInChildren<ConfirmDialogue>(true);
        Assume.That(dialogue, Is.Not.Null);
        MenuTextConversion.InvokeAwake(dialogue);
        Assume.That(dialogue.CancelButton, Is.Not.Null);

        dialogue.CancelButton.onClick.Invoke();

        Assert.That(dialogue.result, Is.EqualTo(dialogue.CANCEL));
    }
}
