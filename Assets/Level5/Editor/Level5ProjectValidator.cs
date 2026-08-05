using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.InputSystem;

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
