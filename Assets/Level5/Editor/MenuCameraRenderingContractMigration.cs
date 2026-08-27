using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// AUD-093 completion: menu cameras already carry a <see cref="UniversalAdditionalCameraData"/> with
/// shadows, post-processing and HDR-display output off, but <c>Camera.allowHDR</c> and
/// <c>Camera.allowMSAA</c> still serialize true. Those are different settings - allowHDR is what
/// actually enables HDR rendering, allowHDROutput only controls HDR-capable display output - so every
/// menu camera still renders a full-resolution HDR clear behind an opaque canvas that never needs it.
///
/// Sets <c>allowHDR</c> and <c>allowMSAA</c> false on every Camera in every enabled
/// <c>level_00_*</c> build scene. Authors a <see cref="UniversalAdditionalCameraData"/> only when one
/// is missing, matching the contract <see cref="Level5ProjectValidator.CollectMenuCameraContractErrors"/>
/// enforces; every menu camera characterized for AUD-093 already has one, so that path is a safety net
/// for a future menu camera, not something this migration currently exercises.
///
/// Saving a touched scene through <see cref="EditorSceneManager.SaveScene"/> can still relocate an
/// unrelated, untouched component's block in the saved YAML - this is an artifact of Unity's own
/// scene writer, not something a script driving it through the sanctioned Editor APIs (rather than
/// hand-editing YAML) can prevent. Scoping <see cref="EditorUtility.SetDirty(UnityEngine.Object)"/> to
/// only the component whose fields actually changed keeps this migration from adding reordering of
/// its own on top of that; it does not eliminate what Unity's own save already does.
///
/// Safe to run more than once: a scene with nothing to change is left untouched and unsaved.
/// </summary>
public static class MenuCameraRenderingContractMigration
{
    [MenuItem("Level5/Migrate Menu Camera Rendering Contract")]
    public static void Migrate()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        int changedCameraCount = 0;
        int changedSceneCount = 0;
        List<string> touchedScenes = new List<string>();

        try
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!Level5ProjectValidator.IsEnabledMenuBuildScene(buildScene))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                bool sceneChanged = false;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        if (ApplyContract(camera))
                        {
                            changedCameraCount++;
                            sceneChanged = true;
                        }
                    }
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changedSceneCount++;
                    touchedScenes.Add(buildScene.path);
                }
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        Debug.Log(
            "Menu camera rendering contract migration: updated " + changedCameraCount
                + " camera(s) across " + changedSceneCount + " scene(s)"
                + (touchedScenes.Count > 0 ? ":\n- " + string.Join("\n- ", touchedScenes.ToArray()) : "."));
    }

    /// <summary>
    /// Returns true if the camera (or its UACD) had to change. Dirties only the component whose own
    /// fields actually changed, per the class-level note on why that is scoped this narrowly.
    /// </summary>
    private static bool ApplyContract(Camera camera)
    {
        bool cameraChanged = false;

        if (camera.allowHDR)
        {
            camera.allowHDR = false;
            cameraChanged = true;
        }

        if (camera.allowMSAA)
        {
            camera.allowMSAA = false;
            cameraChanged = true;
        }

        if (cameraChanged)
        {
            EditorUtility.SetDirty(camera);
        }

        bool cameraDataChanged = false;
        if (!camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraDataChanged = true;
        }

        if (cameraData.renderShadows)
        {
            cameraData.renderShadows = false;
            cameraDataChanged = true;
        }

        if (cameraData.renderPostProcessing)
        {
            cameraData.renderPostProcessing = false;
            cameraDataChanged = true;
        }

        if (cameraData.allowHDROutput)
        {
            cameraData.allowHDROutput = false;
            cameraDataChanged = true;
        }

        if (cameraDataChanged)
        {
            EditorUtility.SetDirty(cameraData);
        }

        return cameraChanged || cameraDataChanged;
    }
}
