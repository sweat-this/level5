using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-092 Phase 2: StatsManager.prefab's directly-owned legacy Text components and
/// highScoreRow.prefab's six columns were migrated to TextMeshProUGUI. Mirrors
/// <c>OptionsTextMeshProMigrationTests</c>'s shape - permanent contract tests delegate to
/// <see cref="Level5ProjectValidator"/>, the rest inspect the real prefab assets directly.
/// </summary>
public class StatsTextMeshProMigrationTests
{
    private const string StatsManagerPrefabPath = "Assets/Resources/Prefabs/menu_stats/StatsManager.prefab";
    private const string HighScoreRowPrefabPath = "Assets/Resources/Prefabs/stats/highScoreRow.prefab";

    [Test]
    public void StatsPrefabUsesTextMeshProOnly()
    {
        List<string> errors = Level5ProjectValidator.CollectStatsTextRenderingContractErrors();
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void StatsUiTextReferencesAreWired()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(StatsManagerPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        StatsUiObjects ui = prefabRoot.GetComponentInChildren<StatsUiObjects>(true);
        Assume.That(ui, Is.Not.Null);

        List<string> missing = new List<string>();
        ui.Validate(missing);
        Assert.That(missing, Is.Empty, string.Join("\n- ", missing));

        Assert.That(ui.ModeSelectText, Is.Not.Null);
        Assert.That(ui.ModeSelectHardcoreText, Is.Not.Null);
        Assert.That(ui.ModeSelectOnlineText, Is.Not.Null);
        Assert.That(ui.PageNumberLocalText, Is.Not.Null);
        Assert.That(ui.PageNumberOnlineText, Is.Not.Null);
        Assert.That(ui.TrafficOptionValueText, Is.Not.Null);
        Assert.That(ui.HardcoreOptionValueText, Is.Not.Null);
        Assert.That(ui.EnemiesOptionValueText, Is.Not.Null);
        Assert.That(ui.SniperOptionValueText, Is.Not.Null);
        Assert.That(ui.SubmittedHighscoresText, Is.Not.Null);
        Assert.That(ui.NumUnsubmittedHighscoresText, Is.Not.Null);
    }

    [Test]
    public void StatsSelectablesHaveValidTargetGraphics()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(StatsManagerPrefabPath);
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
    public void StatsSceneHasNoLegacyTextOverrides()
    {
        List<string> errors = new List<string>();
        MenuTextConversion.CollectDanglingSceneTextOverrides(
            "Assets/Scenes/level_00_stats.unity", StatsManagerPrefabPath, errors);
        Assert.That(errors, Is.Empty, string.Join("\n- ", errors));
    }

    [Test]
    public void HighScoreRowUsesTextMeshProOnly()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HighScoreRowPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        Assert.That(prefabRoot.GetComponentsInChildren<Text>(true), Is.Empty);

        TextMeshProUGUI[] tmpTexts = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert.That(tmpTexts, Has.Length.EqualTo(6));
        foreach (TextMeshProUGUI tmp in tmpTexts)
        {
            Assert.That(tmp.font, Is.Not.Null, tmp.gameObject.name + " has no TMP font asset.");
        }
    }

    [Test]
    public void HighScoreRowHasAllSixTmpReferences()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HighScoreRowPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        StatsTableHighScoreRow row = prefabRoot.GetComponent<StatsTableHighScoreRow>();
        Assume.That(row, Is.Not.Null);

        Assert.That(row.userNameLabel, Is.Not.Null);
        Assert.That(row.scoreLabel, Is.Not.Null);
        Assert.That(row.characterLabel, Is.Not.Null);
        Assert.That(row.levelLabel, Is.Not.Null);
        Assert.That(row.dateLabel, Is.Not.Null);
        Assert.That(row.hardcoreLabel, Is.Not.Null);
    }

    [Test]
    public void HighScoreRowBindWritesEveryColumn()
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HighScoreRowPrefabPath);
        Assume.That(prefabRoot, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefabRoot);
        try
        {
            StatsTableHighScoreRow row = instance.GetComponent<StatsTableHighScoreRow>();
            Assume.That(row, Is.Not.Null);

            row.UserName = "the_doctor";
            row.Score = "123456";
            row.Character = "doctor blood";
            row.Level = "Scrapyard";
            row.Date = "01/02/2026";
            row.HardcoreEnabled = "YES";

            row.Bind();

            Assert.That(row.userNameLabel.text, Is.EqualTo("the_doctor"));
            Assert.That(row.scoreLabel.text, Is.EqualTo("123456"));
            Assert.That(row.characterLabel.text, Is.EqualTo("doctor blood"));
            Assert.That(row.levelLabel.text, Is.EqualTo("Scrapyard"));
            Assert.That(row.dateLabel.text, Is.EqualTo("01/02/2026"));
            Assert.That(row.hardcoreLabel.text, Is.EqualTo("YES"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void HighScoreRowBindIsNoOpForADataOnlyInstance()
    {
        GameObject dataOnly = new GameObject("data_only_row");
        try
        {
            StatsTableHighScoreRow row = dataOnly.AddComponent<StatsTableHighScoreRow>();
            row.UserName = "someone";
            Assert.DoesNotThrow(() => row.Bind());
        }
        finally
        {
            Object.DestroyImmediate(dataOnly);
        }
    }
}
