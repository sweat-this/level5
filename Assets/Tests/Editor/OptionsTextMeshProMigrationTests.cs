using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 1: OptionManager.prefab's legacy Text components were migrated to TextMeshProUGUI on
/// a project-owned Neon Pixel-7 SDF font asset. <see cref="OptionsPrefabUsesTextMeshProOnly"/> and its
/// siblings cover the permanent contract (delegating to <see cref="Level5ProjectValidator"/>, matching
/// every other menu-screen contract test's pattern); the remaining tests exercise the conversion itself
/// against throwaway objects, independent of the real prefab asset on disk.
/// </summary>
public class OptionsTextMeshProMigrationTests
{
    private const string FontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject gameObject in spawned)
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        spawned.Add(gameObject);
        return gameObject;
    }

    private static TMP_FontAsset LoadNeonPixelFontAsset()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    [Test]
    public void OptionsPrefabUsesTextMeshProOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectOptionsTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void OptionsTextMeshProComponentsHaveFontAssets()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/critical/OptionManager.prefab");
        Assume.That(prefabRoot, Is.Not.Null);

        TextMeshProUGUI[] tmpTexts = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert.That(tmpTexts, Is.Not.Empty, "Expected OptionManager.prefab to contain migrated TextMeshProUGUI components.");
        foreach (TextMeshProUGUI tmp in tmpTexts)
        {
            Assert.That(tmp.font, Is.Not.Null, tmp.gameObject.name + " has no TMP font asset.");
        }
    }

    [Test]
    public void OptionsSelectablesHaveValidTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/critical/OptionManager.prefab");
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
    public void ConvertSingleTextPreservesVisualPropertiesAndRewiresTargetGraphic()
    {
        TMP_FontAsset font = LoadNeonPixelFontAsset();
        Assume.That(font, Is.Not.Null, "Neon Pixel-7 SDF font asset must exist for this test.");

        GameObject container = Spawn("container");
        Button button = container.AddComponent<Button>();

        GameObject label = Spawn("label");
        label.transform.SetParent(container.transform, false);
        Text text = label.AddComponent<Text>();
        text.text = "shoot";
        text.fontSize = 42;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        text.raycastTarget = false;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.5f;

        button.targetGraphic = text;

        TextMeshProUGUI tmp = MenuTextMeshProMigration.ConvertSingleText(container, text, font);

        Assert.That(tmp, Is.Not.Null);
        Assert.That(label.GetComponent<Text>(), Is.Null, "legacy Text should have been destroyed");
        Assert.That(tmp.text, Is.EqualTo("shoot"));
        Assert.That(tmp.font, Is.SameAs(font));
        Assert.That(tmp.fontSize, Is.EqualTo(42f));
        Assert.That(tmp.fontStyle, Is.EqualTo(FontStyles.Bold));
        Assert.That(tmp.alignment, Is.EqualTo(TextAlignmentOptions.Center));
        Assert.That(tmp.color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
        Assert.That(tmp.raycastTarget, Is.False);
        Assert.That(tmp.richText, Is.True);
        Assert.That(tmp.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        Assert.That(tmp.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        Assert.That(tmp.lineSpacing, Is.EqualTo(50f).Within(0.01f));
        Assert.That(tmp.enableAutoSizing, Is.False);
        Assert.That(button.targetGraphic, Is.SameAs(tmp), "Button.targetGraphic should now point at the new TMP component.");
    }

    [Test]
    public void ConvertSingleTextCompensatesOutlineWithUnderlayAndRemovesIt()
    {
        TMP_FontAsset font = LoadNeonPixelFontAsset();
        Assume.That(font, Is.Not.Null, "Neon Pixel-7 SDF font asset must exist for this test.");

        GameObject go = Spawn("label_with_outline");
        Text text = go.AddComponent<Text>();
        text.text = "quit_game";
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI tmp = MenuTextMeshProMigration.ConvertSingleText(go, text, font);

        Assert.That(tmp, Is.Not.Null);
        Assert.That(go.GetComponent<Outline>(), Is.Null, "the now-inert legacy Outline should have been removed");
        Assert.That(tmp.fontMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay), Is.True);
        Assert.That(tmp.fontMaterial.GetColor(ShaderUtilities.ID_UnderlayColor), Is.EqualTo(new Color(0f, 0f, 0f, 0.5f)));
    }

    [Test]
    public void ConvertSingleTextSkipsUnderlayCompensationForADisabledOutline()
    {
        TMP_FontAsset font = LoadNeonPixelFontAsset();
        Assume.That(font, Is.Not.Null, "Neon Pixel-7 SDF font asset must exist for this test.");

        GameObject go = Spawn("label_with_disabled_outline");
        Text text = go.AddComponent<Text>();
        Outline outline = go.AddComponent<Outline>();
        outline.enabled = false;

        TextMeshProUGUI tmp = MenuTextMeshProMigration.ConvertSingleText(go, text, font);

        Assert.That(tmp, Is.Not.Null);
        Assert.That(go.GetComponent<Outline>(), Is.Null);
        Assert.That(tmp.fontMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay), Is.False);
    }
}
