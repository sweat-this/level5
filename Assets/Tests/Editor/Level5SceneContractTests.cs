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
}
