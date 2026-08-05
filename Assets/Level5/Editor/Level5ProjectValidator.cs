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
        ValidateBuildScenes(errors);
        ValidateInputActions(errors);

        if (errors.Count > 0)
        {
            throw new BuildFailedException("Level5 project validation failed:\n- " + string.Join("\n- ", errors));
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

    private static void ValidateInputActions(List<string> errors)
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        InputAction movement = actions != null ? actions.FindAction("Player/movement") : null;
        if (movement == null)
        {
            errors.Add("Player/movement is missing from PlayerControls.inputactions.");
        }
        else if (!string.Equals(movement.expectedControlType, "Vector2", StringComparison.Ordinal))
        {
            errors.Add("Player/movement must use the Vector2 expected control type.");
        }
    }
}
