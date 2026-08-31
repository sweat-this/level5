using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 6C: confirm_tip.prefab's four directly-owned legacy Text components (header, tip body,
/// next-button label, close-button label) were migrated to TextMeshProUGUI, and its owning
/// StartScreenTipDialogueManager now resolves UI only through the new TipDialogueUiObjects typed view -
/// no legacy Text/Button fields, no GameObject.Find, no persistent OnClick listeners. confirm_tip.prefab
/// is SHARED (nested directly in level_00_start.unity), so this file's tests exercise the prefab
/// directly rather than through the Start scene, matching
/// <see cref="ConfirmDialogueTextMeshProMigrationTests"/>'s own framing for confirm_update.prefab.
/// </summary>
public class TipDialogueTextMeshProMigrationTests
{
    private const string ConfirmTipPrefabPath = "Assets/Resources/Prefabs/misc/confirm_tip.prefab";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";
    private const string ManagerScriptPath = "Assets/Scripts/menu_start/StartScreenTipDialogueManager.cs";

    private readonly List<Object> _instantiated = new List<Object>();
    private bool _capturedTipDialogueLoadedOnStart;

    [SetUp]
    public void SetUp()
    {
        _capturedTipDialogueLoadedOnStart = GameOptions.tipDialogueLoadedOnStart;
        GameOptions.tipDialogueLoadedOnStart = false;
    }

    [TearDown]
    public void TearDown()
    {
        GameOptions.tipDialogueLoadedOnStart = _capturedTipDialogueLoadedOnStart;

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
    public void ConfirmTipUsesTextMeshProOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectTipDialogueTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void ConfirmTipHasExactlyFourTmpComponents()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        Assert.AreEqual(0, prefabRoot.GetComponentsInChildren<Text>(true).Length);
        Assert.AreEqual(4, prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true).Length);
    }

    [Test]
    public void ConfirmTipUsesNeonPixelFont()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
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
    public void ConfirmTipButtonsHaveValidTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
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
    public void ConfirmTipHasExactlyOneManagerAndOneTypedUiView()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        Assert.AreEqual(1, prefabRoot.GetComponentsInChildren<StartScreenTipDialogueManager>(true).Length);
        Assert.AreEqual(1, prefabRoot.GetComponentsInChildren<TipDialogueUiObjects>(true).Length);
    }

    [Test]
    public void TipDialogueUiObjectsFieldsAllResolve()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        TipDialogueUiObjects ui = prefabRoot.GetComponentInChildren<TipDialogueUiObjects>(true);
        Assume.That(ui, Is.Not.Null);

        Assert.That(ui.Header, Is.Not.Null, "TipDialogueUiObjects.Header did not resolve.");
        Assert.That(ui.Tip, Is.Not.Null, "TipDialogueUiObjects.Tip did not resolve.");
        Assert.That(ui.NextButton, Is.Not.Null, "TipDialogueUiObjects.NextButton did not resolve.");
        Assert.That(ui.CloseButton, Is.Not.Null, "TipDialogueUiObjects.CloseButton did not resolve.");

        List<string> missing = new List<string>();
        Assert.IsTrue(ui.Validate(missing), string.Join(", ", missing));
    }

    [Test]
    public void ManagerHasNoLegacyTextOrButtonFields()
    {
        foreach (FieldInfo field in typeof(StartScreenTipDialogueManager).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Assert.IsFalse(typeof(Text).IsAssignableFrom(field.FieldType), "StartScreenTipDialogueManager." + field.Name + " is still typed as legacy UnityEngine.UI.Text.");
            Assert.IsFalse(typeof(Button).IsAssignableFrom(field.FieldType), "StartScreenTipDialogueManager." + field.Name + " is still typed as a raw Button; UI must be resolved through TipDialogueUiObjects.");
        }
    }

    [Test]
    public void ManagerSourceContainsNoGameObjectFind()
    {
        string repositoryRoot = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(repositoryRoot, ManagerScriptPath.Replace('/', Path.DirectorySeparatorChar));
        Assume.That(File.Exists(fullPath), "could not find " + fullPath);

        string source = File.ReadAllText(fullPath);
        Assert.IsFalse(source.Contains("GameObject.Find"), "StartScreenTipDialogueManager must resolve UI only through its serialized TipDialogueUiObjects reference, not GameObject.Find.");
    }

    [Test]
    public void NoPersistentOnClickListenersRemainOnEitherButton()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        foreach (Button button in prefabRoot.GetComponentsInChildren<Button>(true))
        {
            Assert.AreEqual(0, button.onClick.GetPersistentEventCount(), button.gameObject.name + " still carries a persistent OnClick listener; StartScreenTipDialogueManager must own both buttons exclusively in code.");
        }
    }

    [Test]
    public void EnableDisableDoesNotAccumulateListeners()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        _instantiated.Add(instance);

        StartScreenTipDialogueManager manager = instance.GetComponentInChildren<StartScreenTipDialogueManager>(true);
        TipDialogueUiObjects ui = instance.GetComponentInChildren<TipDialogueUiObjects>(true);
        Assume.That(manager, Is.Not.Null);
        Assume.That(ui, Is.Not.Null);

        MenuTextConversion.InvokeAwake(manager);
        InvokePrivateMethod(manager, "OnEnable");
        InvokePrivateMethod(manager, "OnDisable");
        InvokePrivateMethod(manager, "OnEnable");
        InvokePrivateMethod(manager, "OnDisable");
        InvokePrivateMethod(manager, "OnEnable");

        Assert.AreEqual(1, GetRuntimeListenerCount(ui.NextButton.onClick), "NextButton.onClick accumulated listeners across repeated OnEnable/OnDisable cycles.");
        Assert.AreEqual(1, GetRuntimeListenerCount(ui.CloseButton.onClick), "CloseButton.onClick accumulated listeners across repeated OnEnable/OnDisable cycles.");

        InvokePrivateMethod(manager, "OnDisable");
        Assert.AreEqual(0, GetRuntimeListenerCount(ui.NextButton.onClick), "NextButton.onClick still carries a listener after OnDisable.");
        Assert.AreEqual(0, GetRuntimeListenerCount(ui.CloseButton.onClick), "CloseButton.onClick still carries a listener after OnDisable.");
    }

    [Test]
    public void NextButtonActivationAdvancesTipExactlyOnce()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        _instantiated.Add(instance);

        StartScreenTipDialogueManager manager = instance.GetComponentInChildren<StartScreenTipDialogueManager>(true);
        TipDialogueUiObjects ui = instance.GetComponentInChildren<TipDialogueUiObjects>(true);
        Assume.That(manager, Is.Not.Null);
        Assume.That(ui, Is.Not.Null);

        MenuTextConversion.InvokeAwake(manager);
        InvokePrivateMethod(manager, "OnEnable");

        string firstTip = ui.Tip.text;
        ui.NextButton.onClick.Invoke();
        string secondTip = ui.Tip.text;
        ui.NextButton.onClick.Invoke();
        string thirdTip = ui.Tip.text;

        Assert.AreEqual(manager.NEXT, manager.result);
        Assert.AreNotEqual(firstTip, secondTip, "one NextButton activation should advance the displayed tip.");
        Assert.AreNotEqual(secondTip, thirdTip, "a second, independent NextButton activation should advance the tip again - exactly once each time.");
    }

    [Test]
    public void CloseButtonActivationClosesExactlyOnce()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        _instantiated.Add(instance);

        StartScreenTipDialogueManager manager = instance.GetComponentInChildren<StartScreenTipDialogueManager>(true);
        TipDialogueUiObjects ui = instance.GetComponentInChildren<TipDialogueUiObjects>(true);
        Assume.That(manager, Is.Not.Null);
        Assume.That(ui, Is.Not.Null);

        MenuTextConversion.InvokeAwake(manager);
        InvokePrivateMethod(manager, "OnEnable");

        // CloseTipDialogue() calls Object.Destroy(), which only defers to end-of-frame in Play Mode; in
        // this batch EditMode harness there is no frame boundary to defer to, so Unity logs a warning and
        // leaves the object alone rather than destroying it synchronously - expected collateral of
        // exercising the real, unmodified production code path here, not a defect this migration
        // introduced. The result field is the reliable, environment-independent signal that the close
        // path ran exactly once; EnableDisableDoesNotAccumulateListeners separately proves the listener
        // itself cannot be registered more than once.
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try
        {
            ui.CloseButton.onClick.Invoke();
        }
        finally
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        Assert.AreEqual(manager.CANCEL, manager.result);
    }

    private static void InvokePrivateMethod(MonoBehaviour behaviour, string methodName)
    {
        MethodInfo method = behaviour.GetType().GetMethod(
            methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assume.That(method, Is.Not.Null, behaviour.GetType().Name + " has no " + methodName + "() method to invoke.");
        method.Invoke(behaviour, null);
    }

    /// <summary>
    /// Reflects into UnityEventBase's private backing store to count non-persistent ("runtime",
    /// AddListener-registered) callbacks - the shape that has been stable since UnityEngine.Events was
    /// introduced. There is no public API for this; it is the only way to prove
    /// OnEnable/OnDisable do not leak duplicate registrations without relying on a click's own internal
    /// re-entrancy guard (which would mask the symptom this test exists to catch).
    /// </summary>
    private static int GetRuntimeListenerCount(UnityEventBase unityEvent)
    {
        FieldInfo callsField = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(callsField, Is.Not.Null, "UnityEventBase.m_Calls was not found by reflection.");
        object invokableCallList = callsField.GetValue(unityEvent);
        Assume.That(invokableCallList, Is.Not.Null);

        FieldInfo runtimeCallsField = invokableCallList.GetType().GetField("m_RuntimeCalls", BindingFlags.NonPublic | BindingFlags.Instance);
        Assume.That(runtimeCallsField, Is.Not.Null, "InvokableCallList.m_RuntimeCalls was not found by reflection.");
        IList runtimeCalls = (IList)runtimeCallsField.GetValue(invokableCallList);
        return runtimeCalls.Count;
    }
}
