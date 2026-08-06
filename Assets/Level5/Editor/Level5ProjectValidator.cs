using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class Level5ProjectValidator : IPreprocessBuildWithReport
{
    private const string InputActionsPath = "Assets/Scripts/input/PlayerControls.inputactions";

    public int callbackOrder => -1000;

    [InitializeOnLoadMethod]
    private static void ConfigureSourceControlPolicy()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        VersionControlSettings.mode = "Visible Meta Files";
    }

    [MenuItem("Level5/Validate Project")]
    public static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("Level5 project validation passed.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        ValidateOrThrow();
    }

    public static void ValidateOrThrow()
    {
        List<string> errors = new List<string>();
        ValidateSerializationPolicy(errors);
        ValidateBuildScenes(errors);
        ValidateSelectableLevels(errors);
        ValidateInputActions(errors);

        if (errors.Count > 0)
        {
            throw new BuildFailedException("Level5 project validation failed:\n- " + string.Join("\n- ", errors));
        }
    }

    [MenuItem("Level5/Reserialize Project Assets")]
    public static void ReserializeProjectAssets()
    {
        ConfigureSourceControlPolicy();
        List<string> paths = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            string extension = Path.GetExtension(path);
            if (path.StartsWith("Assets/", StringComparison.Ordinal)
                && (extension == ".unity"
                    || extension == ".prefab"
                    || extension == ".asset"
                    || extension == ".mat"
                    || extension == ".anim"
                    || extension == ".controller"))
            {
                paths.Add(path);
            }
        }

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);
        AssetDatabase.SaveAssets();
        Debug.Log("Reserialized " + paths.Count + " Unity assets for source control.");
    }

    private static void ValidateSerializationPolicy(List<string> errors)
    {
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            errors.Add("Asset serialization mode must be Force Text.");
        }

        if (!string.Equals(VersionControlSettings.mode, "Visible Meta Files", StringComparison.Ordinal))
        {
            errors.Add("Version control mode must use Visible Meta Files.");
        }
    }

    private static void ValidateBuildScenes(List<string> errors)
    {
        HashSet<string> enabledScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(scene.path) || !File.Exists(scene.path))
            {
                errors.Add("An enabled build scene is missing: " + scene.path);
            }
            else if (!enabledScenes.Add(scene.path))
            {
                errors.Add("An enabled build scene is duplicated: " + scene.path);
            }
        }

        if (enabledScenes.Count == 0)
        {
            errors.Add("No enabled scenes are configured in Editor Build Settings.");
        }
    }

    private static void ValidateSelectableLevels(List<string> errors)
    {
        HashSet<string> enabledSceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            {
                enabledSceneNames.Add(Path.GetFileNameWithoutExtension(scene.path));
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            LevelSelected level = prefab != null ? prefab.GetComponent<LevelSelected>() : null;
            if (level == null || !level.IsSelectable)
            {
                continue;
            }

            string sceneName = level.LevelObjectName + "_" + level.LevelDescription;
            if (!enabledSceneNames.Contains(sceneName))
            {
                errors.Add("Selectable level prefab " + path + " maps to missing build scene " + sceneName + ".");
            }
        }
    }

    [MenuItem("Level5/Validate Gameplay Scene Objects")]
    public static void ValidateGameplaySceneObjectsFromMenu()
    {
        List<string> errors = CollectGameplaySceneObjectErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Gameplay scene validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Gameplay scene objects validated.");
    }

    /// <summary>
    /// Every gameplay scene must contain the objects GameRules and Pause resolve by name at
    /// runtime. Those lookups used to be unchecked `GameObject.Find(name).GetComponent&lt;T&gt;()`
    /// chains, so a rename surfaced as a NullReferenceException mid-Start with no indication of
    /// which object was missing.
    ///
    /// This opens scenes, which is not safe to do from inside the build pipeline, so it runs from
    /// the menu and from the edit-mode test suite (which CI already runs on every PR) rather than
    /// from OnPreprocessBuild.
    ///
    /// Limitation: this counts inactive objects as present, while GameObject.Find at runtime only
    /// sees active ones. It catches renames and deletions - the failure this exists for - but not
    /// an object that exists and happens to be inactive when the manager's Start runs.
    /// </summary>
    public static List<string> CollectGameplaySceneObjectErrors()
    {
        List<string> errors = new List<string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path) || !File.Exists(buildScene.path))
            {
                continue;
            }

            // never close a scene the user already had open in the editor
            Scene existing = SceneManager.GetSceneByPath(buildScene.path);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            Scene scene = alreadyOpen
                ? existing
                : EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
            try
            {
                // menu scenes have neither manager, so they have nothing to satisfy
                if (!SceneContainsComponent<GameRules>(scene) && !SceneContainsComponent<Pause>(scene))
                {
                    continue;
                }

                HashSet<string> objectNames = CollectObjectNames(scene);

                if (SceneContainsComponent<GameRules>(scene))
                {
                    AddMissingObjectErrors(errors, buildScene.path, "GameRules", GameRules.RequiredHudObjectNames, objectNames);
                }

                if (SceneContainsComponent<Pause>(scene))
                {
                    AddMissingObjectErrors(errors, buildScene.path, "Pause", Pause.RequiredPauseObjectNames, objectNames);
                }
            }
            finally
            {
                if (!alreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        return errors;
    }

    private static void AddMissingObjectErrors(
        List<string> errors,
        string scenePath,
        string ownerName,
        string[] requiredNames,
        HashSet<string> objectNames)
    {
        foreach (string requiredName in requiredNames)
        {
            if (!objectNames.Contains(requiredName))
            {
                errors.Add(scenePath + " is missing the '" + requiredName + "' object that " + ownerName + " requires.");
            }
        }
    }

    private static bool SceneContainsComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<T>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> CollectObjectNames(Scene scene)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                names.Add(transform.name);
            }
        }

        return names;
    }

    private static void ValidateInputActions(List<string> errors)
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions == null)
        {
            errors.Add("PlayerControls.inputactions could not be loaded.");
            return;
        }

        ValidateAction(actions, "Player/movement", "Vector2", errors);
        ValidateAction(actions, "Player/attack", "Button", errors);
        ValidateAction(actions, "Player/block", "Button", errors);
        ValidateAction(actions, "Player/special", "Button", errors);
        ValidateAction(actions, "Player/submit", "Button", errors);
        ValidateAction(actions, "Player/cancel", "Button", errors);
        ValidateAction(actions, "UINavigation/Submit", "Button", errors);
        ValidateAction(actions, "UINavigation/Cancel", "Button", errors);
    }

    private static void ValidateAction(
        InputActionAsset actions,
        string actionPath,
        string expectedControlType,
        List<string> errors)
    {
        InputAction action = actions.FindAction(actionPath);
        if (action == null)
        {
            errors.Add(actionPath + " is missing from PlayerControls.inputactions.");
        }
        else if (!string.Equals(action.expectedControlType, expectedControlType, StringComparison.Ordinal))
        {
            errors.Add(actionPath + " must use the " + expectedControlType + " expected control type.");
        }
    }
}
