using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 4B: creditsManager.prefab's legacy <c>ReportInputField</c> (<see cref="InputField"/>)
/// and its two structural Text dependencies were migrated to <see cref="TMP_InputField"/> and
/// <see cref="TextMeshProUGUI"/>, completing the Credits screen's TMP migration Phase 4A deliberately
/// left this one field out of. Mirrors <c>ProgressionTextMeshProMigrationTests</c>'s shape - permanent
/// contract tests delegate to <see cref="Level5ProjectValidator"/>, the rest inspect the real
/// prefab/scene directly, and a handful instantiate a scratch copy to exercise runtime lifecycle.
/// </summary>
public class CreditsTextMeshProMigrationTests
{
    private const string CreditsManagerPrefabPath = "Assets/Resources/Prefabs/menu_credits/creditsManager.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_credits.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";
    private const string TouchJoystickPrefabPath = "Assets/Resources/Prefabs/critical/touch_joystick.prefab";
    private const int ExpectedTextMeshProCount = 23;

    private static readonly string[] ExpectedUnderlayLabelNames =
    {
        "press_start", "stats_menu", "options", "quit_game",
    };

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

    // ---------------------------------------------------------------------------------------------
    // 1-2: zero legacy Text / InputField
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreditsPrefabHasNoDirectlyOwnedLegacyTextOrInputField()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        List<Text> ownedTexts = new List<Text>();
        List<Text> nestedTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedTexts, nestedTexts);
        Assert.That(ownedTexts, Is.Empty, "expected zero directly-owned legacy Text - AUD-092 Phase 4B is complete.");

        List<InputField> ownedInputFields = new List<InputField>();
        foreach (InputField candidate in prefabRoot.GetComponentsInChildren<InputField>(true))
        {
            if (!MenuTextConversion.IsPartOfNestedPrefabInstance(candidate.gameObject, prefabRoot))
            {
                ownedInputFields.Add(candidate);
            }
        }

        Assert.That(ownedInputFields, Is.Empty, "expected zero directly-owned legacy InputField.");
    }

    [Test]
    public void CreditsPrefabUsesTextMeshProOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectCreditsTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    // ---------------------------------------------------------------------------------------------
    // 3-4: exactly one TMP_InputField, expected total TextMeshProUGUI count
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void ExactlyOneTmpInputFieldExists()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TMP_InputField[] inputFields = prefabRoot.GetComponentsInChildren<TMP_InputField>(true);
        Assert.That(inputFields, Has.Length.EqualTo(1));
        Assert.That(inputFields[0].gameObject.name, Is.EqualTo("ReportInputField"));
    }

    [Test]
    public void CreditsPrefabHasExpectedTextMeshProCount()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        int ownedCount = 0;
        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (MenuTextConversion.IsPartOfNestedPrefabInstance(tmp.gameObject, prefabRoot))
            {
                continue;
            }

            ownedCount++;
            Assert.That(tmp.font, Is.EqualTo(neonPixel), tmp.gameObject.name + " does not use the shared Neon Pixel-7 SDF font asset.");
        }

        Assert.That(
            ownedCount,
            Is.EqualTo(ExpectedTextMeshProCount),
            "expected 21 ordinary Phase 4A labels plus the InputField's own content/placeholder.");
    }

    // ---------------------------------------------------------------------------------------------
    // 5-11: TMP_InputField viewport/content/placeholder/characterLimit/lineType contract
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void TextViewportIsValidAndMasksItsContent()
    {
        TMP_InputField reportInputField = LoadReportInputField();

        Assert.That(reportInputField.textViewport, Is.Not.Null);
        Assert.That(reportInputField.textViewport.gameObject.name, Is.EqualTo("Text Area"));
        Assert.That(reportInputField.textViewport.parent, Is.EqualTo(reportInputField.transform));
        Assert.That(
            reportInputField.textViewport.GetComponent<RectMask2D>(),
            Is.Not.Null,
            "textViewport must carry a RectMask2D to clip text/caret/selection.");
    }

    [Test]
    public void ContentAndPlaceholderAreTextMeshProOnNeonPixelFont()
    {
        TMP_InputField reportInputField = LoadReportInputField();
        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assume.That(neonPixel, Is.Not.Null);

        Assert.That(reportInputField.textComponent, Is.Not.Null);
        TextMeshProUGUI contentTmp = reportInputField.textComponent as TextMeshProUGUI;
        Assert.That(contentTmp, Is.Not.Null, "textComponent must be a TextMeshProUGUI.");
        Assert.That(contentTmp.font, Is.EqualTo(neonPixel));
        Assert.That(contentTmp.transform.parent, Is.EqualTo(reportInputField.textViewport));

        Assert.That(reportInputField.placeholder, Is.Not.Null);
        TextMeshProUGUI placeholderTmp = reportInputField.placeholder as TextMeshProUGUI;
        Assert.That(placeholderTmp, Is.Not.Null, "placeholder must be a TextMeshProUGUI.");
        Assert.That(placeholderTmp.font, Is.EqualTo(neonPixel));
        Assert.That(placeholderTmp.transform.parent, Is.EqualTo(reportInputField.textViewport));

        Assert.That(contentTmp, Is.Not.SameAs(placeholderTmp));
    }

    [Test]
    public void CharacterLimitAndLineTypeArePreserved()
    {
        TMP_InputField reportInputField = LoadReportInputField();

        Assert.That(reportInputField.characterLimit, Is.EqualTo(255));
        Assert.That(reportInputField.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineSubmit));
        Assert.That(reportInputField.contentType, Is.EqualTo(TMP_InputField.ContentType.Standard));
        Assert.That(reportInputField.characterValidation, Is.EqualTo(TMP_InputField.CharacterValidation.None));
    }

    /// <summary>
    /// The legacy InputField this replaced had no rich-text concept at all - it was always plain text.
    /// TMP_InputField.richText defaults to true, which would let both the user's typed report and the
    /// server's echoed result message (CreditsManager.PresentReportResult) interpret TMP markup -
    /// including clickable &lt;link&gt; tags - the legacy field could never render. Must stay explicitly
    /// disabled.
    /// </summary>
    [Test]
    public void RichTextRemainsDisabled()
    {
        TMP_InputField reportInputField = LoadReportInputField();
        Assert.That(reportInputField.richText, Is.False);
    }

    // ---------------------------------------------------------------------------------------------
    // 12-13: targetGraphic / navigation preserved (including the reverse edge from submit_report)
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreditsSelectablesHaveValidNonLegacyTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, prefabRoot))
            {
                continue; // touch_joystick - out of scope
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

    [Test]
    public void TmpInputFieldNavigationIsPreservedInBothDirections()
    {
        TMP_InputField reportInputField = LoadReportInputField();

        Navigation navigation = reportInputField.navigation;
        Assert.That(navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(navigation.selectOnUp, Is.Not.Null);
        Assert.That(navigation.selectOnUp.gameObject.name, Is.EqualTo("itch.io"));
        Assert.That(navigation.selectOnDown, Is.Not.Null);
        Assert.That(navigation.selectOnDown.gameObject.name, Is.EqualTo("submit_report"));

        // The reverse edge: submit_report's own upward navigation used to point at the destroyed
        // legacy InputField component and had to be rewired to the new TMP_InputField, since Unity
        // does not do this automatically when a Selectable component is replaced on the same GameObject.
        Button submitButton = navigation.selectOnDown as Button;
        Assert.That(submitButton, Is.Not.Null);
        Assert.That(
            submitButton.navigation.selectOnUp,
            Is.SameAs(reportInputField),
            "submit_report's navigation.selectOnUp must reference the new TMP_InputField, not a dangling reference to the destroyed legacy InputField.");
    }

    // ---------------------------------------------------------------------------------------------
    // 14: CreditsUiObjects.ReportInputField resolves correctly
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreditsUiObjectsResolvesTheTmpInputField()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        CreditsUiObjects ui = prefabRoot.GetComponentInChildren<CreditsUiObjects>(true);
        Assume.That(ui, Is.Not.Null);

        List<string> missing = new List<string>();
        Assert.That(ui.Validate(missing), Is.True, string.Join(", ", missing));

        TMP_InputField reportInputField = prefabRoot.GetComponentInChildren<TMP_InputField>(true);
        Assert.That(ui.ReportInputField, Is.Not.Null);
        Assert.That(ui.ReportInputField, Is.SameAs(reportInputField));
    }

    // ---------------------------------------------------------------------------------------------
    // 16-19: scene overrides, nested prefab, formerly-overridden strings, footer material sharing
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void CreditsSceneHasNoLegacyTextOverrides()
    {
        List<string> errors = new List<string>();
        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, CreditsManagerPrefabPath, errors);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
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
        MenuTextConversion.CollectDanglingPrefabTextOverrides(CreditsManagerPrefabPath, TouchJoystickPrefabPath, errors);
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

    /// <summary>
    /// All four footer buttons clone the exact same outline compensation (same color/offset/softness),
    /// so <see cref="MenuTextConversion.PersistLooseUnderlayMaterials"/> collapses them onto ONE
    /// persisted, screen-qualified material rather than one per button - unaffected by this phase's
    /// InputField migration; re-asserted here as a regression guard.
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
        Assert.That(sharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay), Is.True, "underlay keyword is not enabled.");

        foreach (TextMeshProUGUI tmp in labels)
        {
            Assert.That(
                tmp.fontSharedMaterial,
                Is.SameAs(sharedMaterial),
                tmp.gameObject.name + " does not share the deduplicated underlay material with the other footer buttons.");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 20/25: idempotency (the contract itself is the idempotency guard: Migrate/MigrateInputField
    // only ever move the prefab TOWARD this state, so an empty error list here is exactly what a
    // second migration run - and this test suite re-run against it - both require).
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void MigrationIsIdempotent()
    {
        List<string> errors = Level5ProjectValidator.CollectCreditsTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    // ---------------------------------------------------------------------------------------------
    // 21: no destroyed legacy object remains referenced
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void NoSerializedConsumerReferencesADestroyedLegacyTextComponent()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        HashSet<Object> textSet = new HashSet<Object>(prefabRoot.GetComponentsInChildren<Text>(true));
        List<string> findings = new List<string>();
        MenuTextConversion.CollectUnsupportedConsumers(prefabRoot, textSet, findings);
        Assert.That(findings, Is.Empty, string.Join("\n- ", findings));
    }

    // ---------------------------------------------------------------------------------------------
    // 22-23: single guarded submit route, no listener accumulation across enable/disable
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void SubmitReportButtonHasNoPersistentOnClickListeners()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        CreditsUiObjects ui = prefabRoot.GetComponentInChildren<CreditsUiObjects>(true);
        Assume.That(ui, Is.Not.Null);
        Assume.That(ui.SubmitReportButton, Is.Not.Null);

        Assert.That(
            ui.SubmitReportButton.onClick.GetPersistentEventCount(),
            Is.EqualTo(0),
            "submit_report must carry no persistent OnClick listener - CreditsManager registers the sole"
                + " guarded route (SubmitReportIfAllowed) at runtime; an authored persistent listener here"
                + " would let one click produce two report submissions.");
    }

    /// <summary>
    /// Proves <c>CreditsManager.RegisterReportInputSubmit</c>'s <c>RemoveListener</c>-before-<c>AddListener</c>
    /// pattern is actually idempotent: registering 3 times still leaves exactly one live listener, so a
    /// single Unregister call fully silences it. If Register had instead accumulated 3 separate
    /// listener entries, the second Invoke below would still fire after only one Unregister call.
    /// </summary>
    [Test]
    public void RepeatedRegisterCallsDoNotAccumulateSubmitListeners()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        _instantiated.Add(instance);

        CreditsManager manager = instance.GetComponentInChildren<CreditsManager>(true);
        TMP_InputField reportInputField = instance.GetComponentInChildren<TMP_InputField>(true);
        Assume.That(manager, Is.Not.Null);
        Assume.That(reportInputField, Is.Not.Null);

        GameObject eventSystemObject = new GameObject("TestEventSystem", typeof(EventSystem));
        _instantiated.Add(eventSystemObject);
        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();

        // EventSystem.current is not directly settable to an arbitrary instance - its setter can only
        // reorder an EventSystem already present in the internal m_EventSystems list (and logs an error
        // for anything else), and that list is only ever populated by EventSystem.OnEnable() itself.
        // Confirmed empirically that OnEnable() does not fire for this freshly-constructed GameObject in
        // this project's batchmode EditMode test harness - the same class of gap
        // MenuTextConversion.InvokeAwake exists for, here affecting OnEnable instead of Awake - so it
        // must be invoked directly.
        typeof(EventSystem)
            .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(eventSystem, null);
        Assume.That(EventSystem.current, Is.SameAs(eventSystem));

        GameObject dummySubmitButton = new GameObject("dummy_submit", typeof(RectTransform));
        _instantiated.Add(dummySubmitButton);

        SetPrivateField(manager, "reportInputField", reportInputField);
        SetPrivateField(manager, "submitReportButtonObject", dummySubmitButton);

        MethodInfo register = typeof(CreditsManager).GetMethod("RegisterReportInputSubmit", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo unregister = typeof(CreditsManager).GetMethod("UnregisterReportInputSubmit", BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(register, Is.Not.Null);
        Assume.That(unregister, Is.Not.Null);

        for (int i = 0; i < 3; i++)
        {
            register.Invoke(manager, null);
        }

        reportInputField.onSubmit.Invoke(string.Empty);
        Assert.That(
            EventSystem.current.currentSelectedGameObject,
            Is.SameAs(dummySubmitButton),
            "expected the submit listener to select the submit button after 3 Register calls.");

        unregister.Invoke(manager, null);
        EventSystem.current.SetSelectedGameObject(null);

        reportInputField.onSubmit.Invoke(string.Empty);
        Assert.That(
            EventSystem.current.currentSelectedGameObject,
            Is.Null,
            "a single Unregister call must fully clear the listener - if Register had accumulated duplicate listeners, this would still fire.");
    }

    // ---------------------------------------------------------------------------------------------
    // 24: API result presentation remains owned by CreditsManager
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void ApiHelperPostReportDoesNotAcceptAConcreteUiWidget()
    {
        MethodInfo postReport = typeof(Assets.Scripts.restapi.APIHelper).GetMethod("PostReport");
        Assume.That(postReport, Is.Not.Null);

        foreach (ParameterInfo parameter in postReport.GetParameters())
        {
            string ns = parameter.ParameterType.Namespace ?? string.Empty;
            Assert.That(
                ns,
                Is.Not.EqualTo("UnityEngine.UI"),
                "APIHelper.PostReport must not accept a concrete UI widget (" + parameter.ParameterType.FullName
                    + ") - report success/failure presentation belongs to CreditsManager.");
            Assert.That(
                ns,
                Does.Not.StartWith("TMPro"),
                "APIHelper.PostReport must not accept a concrete TMP widget (" + parameter.ParameterType.FullName
                    + ") either - the same UI-ownership rule applies regardless of legacy vs TMP.");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------------------------------

    private static TMP_InputField LoadReportInputField()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TMP_InputField reportInputField = prefabRoot.GetComponentInChildren<TMP_InputField>(true);
        Assume.That(reportInputField, Is.Not.Null);
        return reportInputField;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(field, Is.Not.Null, "expected a private field named '" + fieldName + "'.");
        field.SetValue(target, value);
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
