using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Level5CampaignRouteTests
{
    private PlayerData source;
    private List<LevelSelected> originalRoute;
    private EditorBuildSettingsScene[] originalScenes;

    [SetUp]
    public void SetUp()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/critical/player_data.prefab");
        Assert.That(prefab, Is.Not.Null);
        source = prefab.GetComponent<PlayerData>();
        Assert.That(source, Is.Not.Null);
        originalRoute = source.LevelsList;
        originalScenes = EditorBuildSettings.scenes;
    }

    [TearDown]
    public void TearDown()
    {
        if (source != null) source.LevelsList = originalRoute;
        if (originalScenes != null) EditorBuildSettings.scenes = originalScenes;
    }

    [Test]
    public void AuthoredRouteRetainsItsOrderOpponentsAndApprovedDevFinale()
    {
        int[] ids = { 1, 3, 2, 8, 6, 13, 19, 14, 20, 7, 21, 9, 16, 5, 24, 4, 10, 11, 22, 15, 23 };
        Assert.That(originalRoute.Select(level => level.LevelId), Is.EqualTo(ids));
        Assert.That(originalRoute[0].LevelObjectName + "_" + originalRoute[0].LevelDescription,
            Is.EqualTo(Constants.SCENE_NAME_level_01_scrapyard));
        Assert.That(originalRoute[20].LevelObjectName + "_" + originalRoute[20].LevelDescription,
            Is.EqualTo("level_23_dev"));
        Assert.That(originalRoute[14].IsSelectable, Is.False, "hidden Boneyard remains legitimate campaign content");
        Assert.That(originalRoute[20].IsSelectable, Is.False, "shipping the finale must not enable ordinary arena selection");
        Assert.That(originalRoute[20].LevelSupportsMultiplayer, Is.False);
        foreach (LevelSelected level in originalRoute)
        {
            Assert.That(level.CpuPlayer, Is.Not.Null, level.LevelDisplayName);
            Assert.That(level.CpuPlayer.GetComponent<CharacterProfile>(), Is.Not.Null, level.LevelDisplayName);
        }
        Assert.That(originalRoute[20].CpuPlayer.name, Is.EqualTo("cpu_player_pony"));
        Assert.That(Level5ProjectValidator.CollectCampaignRouteErrors(), Is.Empty);
    }

    [Test]
    public void VictoriesAdvanceThroughAuthoredRoundsAndCompleteOnlyAtDev()
    {
        for (int index = 0; index < originalRoute.Count; index++)
        {
            bool final = index == originalRoute.Count - 1;
            Assert.That(CampaignRoundDecision.Decide(final, false, false, 2),
                Is.EqualTo(final ? CampaignNextAction.Complete : CampaignNextAction.Advance),
                originalRoute[index].LevelDisplayName);
        }
        Assert.That(originalRoute[originalRoute.Count - 2].LevelId, Is.EqualTo(15));
        Assert.That(originalRoute[originalRoute.Count - 1].LevelId, Is.EqualTo(23));
    }

    [Test]
    public void DisablingApprovedFinaleFailsWithRouteIndexLevelAndScene()
    {
        EditorBuildSettings.scenes = originalScenes.Select(scene =>
            new EditorBuildSettingsScene(scene.path, scene.path == "Assets/Scenes/level_23_dev.unity" ? false : scene.enabled)).ToArray();
        AssertDiagnostic("LevelsList[20]", "Dev (id 23", "level_23_dev", "disabled");
    }

    [Test]
    public void MissingRouteReferenceFailsAtItsAuthoredIndex()
    {
        source.LevelsList = new List<LevelSelected>(originalRoute);
        source.LevelsList[19] = null;
        AssertDiagnostic("LevelsList[19]", "missing/null LevelSelected");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrEmptyRouteFails(bool missing)
    {
        source.LevelsList = missing ? null : new List<LevelSelected>();
        AssertDiagnostic("player_data.prefab", "nonempty LevelsList");
    }

    [Test]
    public void UnlistedDestinationFailsEvenWhenItsSceneExists()
    {
        EditorBuildSettings.scenes = originalScenes.Where(scene => scene.path != "Assets/Scenes/level_23_dev.unity").ToArray();
        AssertDiagnostic("LevelsList[20]", "level_23_dev", "no matching scene");
    }

    [Test]
    public void ListedButMissingSceneAssetFails()
    {
        EditorBuildSettings.scenes = originalScenes.Select(scene => new EditorBuildSettingsScene(
            scene.path == "Assets/Scenes/level_23_dev.unity" ? "Assets/Missing/level_23_dev.unity" : scene.path,
            scene.enabled)).ToArray();
        AssertDiagnostic("LevelsList[20]", "level_23_dev", "missing scene asset");
        Assert.That(Level5ProjectValidator.CollectEnabledDevSceneErrors(), Is.Not.Empty,
            "the exemption belongs to the exact approved path, not every scene with the same filename");
    }

    [Test]
    public void OtherDeveloperDestinationsRemainRejected()
    {
        GameObject invalid = new GameObject("unapproved campaign test level");
        try
        {
            LevelSelected level = invalid.AddComponent<LevelSelected>();
            level.LevelId = 999;
            level.LevelDisplayName = "Unapproved";
            level.LevelObjectName = "dev";
            level.LevelDescription = "arena";
            source.LevelsList = new List<LevelSelected>(originalRoute) { level };
            EditorBuildSettings.scenes = originalScenes.Concat(new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/dev_arena.unity", true)
            }).ToArray();
            AssertDiagnostic("LevelsList[21]", "Unapproved (id 999", "dev_arena", "unapproved developer scene");
            Assert.That(Level5ProjectValidator.CollectEnabledDevSceneErrors(),
                Has.Some.Contains("dev_arena.unity"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(invalid);
        }
    }

    [Test]
    public void DuplicateSceneNamesCannotResolveAnAmbiguousCampaignDestination()
    {
        EditorBuildSettings.scenes = originalScenes.Concat(new[]
        {
            new EditorBuildSettingsScene("Assets/Other/level_23_dev.unity", true)
        }).ToArray();
        AssertDiagnostic("LevelsList[20]", "level_23_dev", "ambiguous");
    }

    [TestCase(null, "dev")]
    [TestCase("level_23", " ")]
    public void IncompleteProductionSceneNameFails(string objectName, string description)
    {
        GameObject invalid = new GameObject("incomplete campaign level");
        try
        {
            LevelSelected level = invalid.AddComponent<LevelSelected>();
            level.LevelObjectName = objectName;
            level.LevelDescription = description;
            source.LevelsList = new List<LevelSelected> { level };
            AssertDiagnostic("LevelsList[0]", "incomplete authored scene name");
        }
        finally { UnityEngine.Object.DestroyImmediate(invalid); }
    }

    [Test]
    public void ShippingFinaleAddsNoDeveloperScriptsBeyondTheExistingShippingBootstrap()
    {
        IEnumerable<string> DevScripts(string scene) => AssetDatabase.GetDependencies(scene, true)
            .Where(path => path.StartsWith("Assets/Scripts/Dev/") && path.EndsWith(".cs"));
        Assert.That(DevScripts("Assets/Scenes/level_23_dev.unity")
            .Except(DevScripts("Assets/Scenes/level_01_scrapyard.unity")), Is.Empty);
        Assert.That(Level5ProjectValidator.CollectDevIsolationErrors(), Is.Empty);
    }

    private static void AssertDiagnostic(params string[] parts)
    {
        List<string> errors = Level5ProjectValidator.CollectCampaignRouteErrors();
        Assert.That(errors.Any(error => parts.All(part => error.Contains(part))), Is.True,
            string.Join("\n", errors));
    }
}
