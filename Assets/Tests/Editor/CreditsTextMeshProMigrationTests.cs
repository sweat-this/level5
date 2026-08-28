using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-092 Phase 4A: creditsManager.prefab's 21 ordinary display/button legacy Text components were
/// migrated to TextMeshProUGUI, while the legacy <c>ReportInputField</c> (<see cref="InputField"/>) and
/// its two structural Text dependencies (<c>textComponent</c>, <c>placeholder</c>) deliberately remain
/// legacy Text until Phase 4B migrates the InputField itself. Mirrors
/// <c>ProgressionTextMeshProMigrationTests</c>'s shape - permanent contract tests delegate to
/// <see cref="Level5ProjectValidator"/>, the rest inspect the real prefab/scene directly.
/// </summary>
public class CreditsTextMeshProMigrationTests
{
    private const string CreditsManagerPrefabPath = "Assets/Resources/Prefabs/menu_credits/creditsManager.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_credits.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private static readonly string[] ExpectedUnderlayLabelNames =
    {
        "press_start", "stats_menu", "options", "quit_game",
    };

    [Test]
    public void CreditsPrefabUsesTextMeshProForOrdinaryLabelsOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectCreditsTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void CreditsOrdinaryLabelsAreTextMeshProOnNeonPixelFont()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        List<Text> ownedTexts = new List<Text>();
        List<Text> nestedTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedTexts, nestedTexts);

        TextMeshProUGUI[] tmpComponents = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert.That(tmpComponents.Length, Is.EqualTo(21), "Expected exactly 21 migrated Phase 4A display/button labels.");

        foreach (TextMeshProUGUI tmp in tmpComponents)
        {
            Assert.That(tmp.font, Is.EqualTo(neonPixel), tmp.gameObject.name + " does not use the shared Neon Pixel-7 SDF font asset.");
            Assert.That(tmp.GetComponent<Text>(), Is.Null, tmp.gameObject.name + " still carries a legacy Text component.");
            Assert.That(tmp.GetComponent<Outline>(), Is.Null, tmp.gameObject.name + " still carries a legacy Outline component.");
        }
    }

    [Test]
    public void ExactlyTwoLegacyTextRemainAndTheyAreTheInputFieldDependencies()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        List<Text> ownedTexts = new List<Text>();
        List<Text> nestedTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedTexts, nestedTexts);

        Assert.That(ownedTexts, Has.Count.EqualTo(2), "Expected exactly 2 directly-owned legacy Text components (the InputField boundary).");

        InputField[] inputFields = prefabRoot.GetComponentsInChildren<InputField>(true);
        Assert.That(inputFields, Has.Length.EqualTo(1), "Expected exactly 1 legacy InputField (ReportInputField).");

        InputField reportInputField = inputFields[0];
        Assert.That(reportInputField.gameObject.name, Is.EqualTo("ReportInputField"));
        Assert.That(reportInputField.textComponent, Is.Not.Null);
        Assert.That(reportInputField.placeholder, Is.Not.Null);

        CollectionAssert.Contains(ownedTexts, reportInputField.textComponent, "InputField.textComponent must be one of the two remaining legacy Text components.");
        CollectionAssert.Contains(ownedTexts, reportInputField.placeholder, "InputField.placeholder must be one of the two remaining legacy Text components.");
        Assert.That(
            reportInputField.textComponent,
            Is.Not.SameAs(reportInputField.placeholder),
            "textComponent and placeholder must be two distinct Text components, not the same one serving both roles.");
    }

    [Test]
    public void ReportInputFieldRemainsAValidLegacyInputField()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        InputField reportInputField = prefabRoot.GetComponentInChildren<InputField>(true);
        Assert.That(reportInputField, Is.Not.Null);
        Assert.That(reportInputField.characterLimit, Is.EqualTo(255));

        Text textComponent = reportInputField.textComponent;
        Assert.That(textComponent, Is.Not.Null);
        Assert.That(textComponent.gameObject.name, Is.EqualTo("Text"));
        Assert.That(textComponent.transform.parent, Is.EqualTo(reportInputField.transform));

        Assert.That(reportInputField.placeholder, Is.Not.Null);
        Text placeholderText = reportInputField.placeholder as Text;
        Assert.That(placeholderText, Is.Not.Null, "placeholder must still be a legacy Text/Graphic dependency.");
        Assert.That(placeholderText.gameObject.name, Is.EqualTo("Placeholder"));
        Assert.That(placeholderText.transform.parent, Is.EqualTo(reportInputField.transform));
    }

    [Test]
    public void CreditsSceneHasNoLegacyTextOverrides()
    {
        List<string> errors = new List<string>();
        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, CreditsManagerPrefabPath, errors);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void CreditsScenePreservesTheFormerlyOverriddenStrings()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TextMeshProUGUI website = FindNamed(prefabRoot, "website", "column.1");
        TextMeshProUGUI music = FindNamed(prefabRoot, "music", "column.1");

        Assert.That(website, Is.Not.Null, "expected the 'website' label under column.1 to exist.");
        Assert.That(website.text, Is.EqualTo("sweatthis.com"));

        Assert.That(music, Is.Not.Null, "expected the 'music' label under column.1 to exist.");
        Assert.That(music.text, Is.EqualTo("IG @stustumaru"));
    }

    [Test]
    public void CreditsSelectablesHaveValidTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (selectable is InputField)
            {
                continue; // ReportInputField's own targetGraphic is unrelated to this migration
            }

            Assert.That(
                selectable.targetGraphic,
                Is.Not.Null,
                selectable.gameObject.name + " (" + selectable.GetType().Name + ") has a null targetGraphic.");
            Assert.That(
                selectable.targetGraphic,
                Is.Not.InstanceOf<Text>(),
                selectable.gameObject.name + " still targets a legacy Text component.");
        }
    }

    /// <summary>
    /// All four footer buttons clone the exact same outline compensation (same color/offset/softness),
    /// so <see cref="MenuTextConversion.PersistLooseUnderlayMaterials"/> collapses them onto ONE
    /// persisted, screen-qualified material rather than one per button - see that method's doc comment
    /// for why this is safe within a single screen but must never happen across two different screens.
    /// </summary>
    [Test]
    public void FourOutlineLabelsShareOnePersistedUnderlayMaterial()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        foreach (string labelName in ExpectedUnderlayLabelNames)
        {
            TextMeshProUGUI tmp = FindNamed(prefabRoot, labelName, "footer");
            Assert.That(tmp, Is.Not.Null, "expected footer label '" + labelName + "' to exist.");
            labels.Add(tmp);
        }

        Material sharedMaterial = labels[0].fontSharedMaterial;
        Assert.That(sharedMaterial, Is.Not.Null, labels[0].gameObject.name + " has no font material.");
        Assert.That(
            AssetDatabase.GetAssetPath(sharedMaterial),
            Does.Match(@"Neon Pixel-7 SDF - creditsManager - \w+ Underlay\.mat$"),
            "expected a persisted, screen-qualified underlay material asset.");
        Assert.That(sharedMaterial.IsKeywordEnabled(TMPro.ShaderUtilities.Keyword_Underlay), Is.True, "underlay keyword is not enabled.");

        foreach (TextMeshProUGUI tmp in labels)
        {
            Assert.That(
                tmp.fontSharedMaterial,
                Is.SameAs(sharedMaterial),
                tmp.gameObject.name + " does not share the deduplicated underlay material with the other footer buttons.");
        }
    }

    [Test]
    public void NestedTouchJoystickInstanceIsUntouched()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        Text[] allTexts = prefabRoot.GetComponentsInChildren<Text>(true);
        List<Text> owned = new List<Text>();
        List<Text> nested = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, prefabRoot, owned, nested);

        Assert.That(nested, Has.Count.EqualTo(1), "expected exactly 1 legacy Text inside the nested touch_joystick.prefab instance.");

        List<string> errors = new List<string>();
        MenuTextConversion.CollectDanglingPrefabTextOverrides(
            CreditsManagerPrefabPath, "Assets/Resources/Prefabs/critical/touch_joystick.prefab", errors);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void MigrationIsIdempotent()
    {
        // The contract requiring exactly 2 protected legacy Text (not 0) already proves the migration
        // has nothing further to convert on this prefab as it sits on disk; this test guards the
        // invariant that would break that: no unmigrated Phase 4A candidate remains.
        List<string> errors = Level5ProjectValidator.CollectCreditsTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void NoUnsupportedConsumerReferencesTheProtectedOrDestroyedTextComponents()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        HashSet<Object> textSet = new HashSet<Object>(prefabRoot.GetComponentsInChildren<Text>(true));
        List<string> findings = new List<string>();
        MenuTextConversion.CollectUnsupportedConsumers(prefabRoot, textSet, findings);
        Assert.That(findings, Is.Empty, string.Join("\n- ", findings));
    }

    private static TextMeshProUGUI FindNamed(GameObject root, string objectName, string expectedParentName)
    {
        foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == objectName && tmp.transform.parent.name == expectedParentName)
            {
                return tmp;
            }
        }

        return null;
    }
}
