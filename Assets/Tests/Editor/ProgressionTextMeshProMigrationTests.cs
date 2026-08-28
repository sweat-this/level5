using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// AUD-092 Phase 3: progressionScreen.prefab's directly-owned legacy Text components were migrated to
/// TextMeshProUGUI. Mirrors <c>StatsTextMeshProMigrationTests</c>'s shape - permanent contract tests
/// delegate to <see cref="Level5ProjectValidator"/>, the rest inspect the real prefab/scene directly.
///
/// Unlike Stats, <see cref="ProgressionUiObjects"/> is added only to the scene instance of
/// <c>progression_manager</c> in <see cref="ScenePath"/>, not to either source prefab - so
/// <see cref="ProgressionUiDisplayReferencesAreWired"/> opens the scene rather than reading a prefab
/// asset, the same way <see cref="ProgressionTextMeshProMigration.CollectContractErrors"/> does.
/// </summary>
public class ProgressionTextMeshProMigrationTests
{
    private const string ProgressionScreenPrefabPath = "Assets/Resources/Prefabs/menu_progression/progressionScreen.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_progression.unity";

    [Test]
    public void ProgressionScreenUsesTextMeshProOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectProgressionTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void ProgressionSelectablesHaveValidTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ProgressionScreenPrefabPath);
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
    public void ProgressionSceneHasNoLegacyTextOverrides()
    {
        List<string> errors = new List<string>();
        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, ProgressionScreenPrefabPath, errors);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void ProgressionUiDisplayReferencesAreWired()
    {
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyOpen = existing.IsValid() && existing.isLoaded;
        Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            ProgressionManager manager = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                manager = root.GetComponentInChildren<ProgressionManager>(true);
                if (manager != null)
                {
                    break;
                }
            }

            Assume.That(manager, Is.Not.Null);

            ProgressionUiObjects ui = manager.GetComponent<ProgressionUiObjects>();
            Assume.That(ui, Is.Not.Null);

            List<string> missing = new List<string>();
            bool valid = ui.Validate(missing);
            Assert.That(valid, Is.True, string.Join(", ", missing));

            Assert.That(ui.PlayerSelectOptionImage, Is.Not.Null);
            Assert.That(ui.PlayerSelectOptionText, Is.Not.Null);
            Assert.That(ui.PlayerProgressionStatsText, Is.Not.Null);
            Assert.That(ui.PlayerProgressionUpdatePointsText, Is.Not.Null);
            Assert.That(ui.Progression3Accuracy, Is.Not.Null);
            Assert.That(ui.Progression4Accuracy, Is.Not.Null);
            Assert.That(ui.Progression7Accuracy, Is.Not.Null);
            Assert.That(ui.ProgressionRange, Is.Not.Null);
            Assert.That(ui.ProgressionRelease, Is.Not.Null);
            Assert.That(ui.ProgressionSpeed, Is.Not.Null);
            Assert.That(ui.ProgressionJump, Is.Not.Null);
            Assert.That(ui.ProgressionLuck, Is.Not.Null);
            Assert.That(ui.BonusReleaseText, Is.Not.Null);
            Assert.That(ui.BonusRangeText, Is.Not.Null);
            Assert.That(ui.BonusLuckText, Is.Not.Null);
            Assert.That(ui.AddTo3Text, Is.Not.Null);
            Assert.That(ui.AddTo4Text, Is.Not.Null);
            Assert.That(ui.AddTo7Text, Is.Not.Null);
        }
        finally
        {
            if (!alreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
