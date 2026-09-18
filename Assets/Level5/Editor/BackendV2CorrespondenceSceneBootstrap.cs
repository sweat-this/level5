using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-off generator for <c>Assets/Scenes/level_00_multiplayer.unity</c> (#159). The scene itself
/// only needs to host <see cref="CorrespondenceScreenController"/>, which builds its own
/// Canvas/EventSystem/UI hierarchy at runtime (see that script's own doc comment for why), so there
/// is nothing else for this scene to contain.
///
/// Re-run via Tools/Backend V2/Generate Correspondence Scene if the scene file is ever deleted or
/// needs regenerating; it is idempotent (overwrites the existing scene, and only adds itself to
/// Build Settings once).
/// </summary>
public static class BackendV2CorrespondenceSceneBootstrap
{
    private const string ScenePath = "Assets/Scenes/level_00_multiplayer.unity";

    [MenuItem("Tools/Backend V2/Generate Correspondence Scene")]
    public static void GenerateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject controllerGo = new GameObject("CorrespondenceScreen");
        controllerGo.AddComponent<CorrespondenceScreenController>();

        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            Debug.LogError("BackendV2CorrespondenceSceneBootstrap: failed to save " + ScenePath);
            return;
        }

        AddSceneToBuildSettings(ScenePath);
        Debug.Log("BackendV2CorrespondenceSceneBootstrap: generated " + ScenePath);
    }

    private static void AddSceneToBuildSettings(string path)
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        if (existing.Any(s => s.path == path))
        {
            return;
        }

        EditorBuildSettingsScene[] updated = existing
            .Append(new EditorBuildSettingsScene(path, true))
            .ToArray();
        EditorBuildSettings.scenes = updated;
    }
}
