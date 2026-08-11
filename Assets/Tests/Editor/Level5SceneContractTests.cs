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
}
