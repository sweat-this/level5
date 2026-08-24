using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// AUD-028: GameRules and Pause resolve HUD and pause-menu objects by name at runtime.
/// A rename used to surface as a NullReferenceException partway through Start, with nothing
/// naming the missing object. This fails in CI instead.
///
/// It opens every enabled build scene, so it is slower than the other edit-mode tests. It lives
/// here rather than in the build preprocessor because opening scenes during the build pipeline
/// is not safe.
/// </summary>
public class Level5SceneContractTests
{
    [Test]
    public void EveryGameplaySceneProvidesTheObjectsItsManagersLookUpByName()
    {
        List<string> errors = Level5ProjectValidator.CollectGameplaySceneObjectErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Gameplay scenes are missing objects their managers resolve by name:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }

    /// <summary>
    /// AUD-012: nothing in `Assets/Scripts/Dev` may be reachable from a release build. Production
    /// code may call into it only from inside a `#if UNITY_EDITOR || DEVELOPMENT_BUILD` region.
    /// </summary>
    [Test]
    public void ProductionCodeDoesNotReferenceDevScriptsWithoutAGuard()
    {
        List<string> errors = Level5ProjectValidator.CollectDevIsolationErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Production code reaches into Assets/Scripts/Dev without a build guard:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }

    [Test]
    public void DevScenesAreNotEnabledInReleaseBuildSettings()
    {
        List<string> errors = Level5ProjectValidator.CollectEnabledDevSceneErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Dev-only scenes must stay out of enabled build settings:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }

    /// <summary>
    /// AUD-088: the project is on Force Text and `.gitattributes` treats `*.prefab` as text, but
    /// eighteen assets were still Unity 2021.1 binary - including every menu screen's UI except the
    /// start menu. A change to any of them was unreviewable and unmergeable.
    ///
    /// Fixing this needs the editor: run `Level5/Reserialize Binary Assets`. This test is what tells
    /// you it has not been run, or has regressed. It is deliberately not part of
    /// `Level5ProjectValidator.ValidateOrThrow` yet - promoting it to the build preprocessor is the
    /// follow-up once the reserialized assets are committed, so builds are not blocked in between.
    /// </summary>
    [Test]
    public void UnityAssetsAreTextSerialized()
    {
        List<string> errors = Level5ProjectValidator.CollectBinarySerializedAssetErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Assets are binary-serialized while the project is Force Text:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }

    /// <summary>
    /// AUD-091: the start menu had three canvases scaling at three different rates - 800x400 at
    /// 0.9, 800x600, and 1920x1080, all matching on width only - so the layers only lined up at the
    /// aspect they were authored on.
    /// </summary>
    [Test]
    public void MenuCanvasesShareOneScalingContract()
    {
        List<string> errors = Level5ProjectValidator.CollectMenuCanvasContractErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Menu canvases do not follow the shared scaling contract:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }

    [Test]
    public void UnityAssetsDoNotHaveMissingScriptReferences()
    {
        List<string> errors = Level5ProjectValidator.CollectMissingScriptReferenceErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Unity assets/settings have missing MonoBehaviour script references:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }

    /// <summary>
    /// AUD-103/AUD-104: OptionsManager/CreditsManager/StatsManager/ProgressionManager/AccountManager/
    /// StartManager/Pause each carry a serialized `*UiObjects`/`MenuFooterUiObjects` view now instead
    /// of a `GameObject.Find(name)` fallback. A rename cannot break a serialized reference, but a
    /// forgotten or mis-wired field still needs to fail the build - this is that check, delegating to
    /// each manager's own `ValidateMenuUi` rather than a separate name list.
    /// </summary>
    [Test]
    public void EveryMenuManagerHasItsRequiredUiObjectReferencesWired()
    {
        List<string> errors = Level5ProjectValidator.CollectMenuUiObjectContractErrors();

        Assert.That(
            errors,
            Is.Empty,
            "Menu managers are missing serialized UI references:\n- "
                + string.Join("\n- ", errors.ToArray()));
    }
}
