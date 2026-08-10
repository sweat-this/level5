using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-off, non-destructive validation for the player-select architecture overhaul: opens the
/// start scene, confirms every GameObject resolves its script (no missing MonoBehaviour
/// references introduced by removing fields from StartManager/StartMenuUiObjects), and confirms
/// StartManager/StartMenuUiObjects still resolve with the widgets PlayerSelectView needs.
///
/// Read-only: never calls EditorSceneManager.SaveScene. Intended to be run once from the command
/// line (-executeMethod) as part of this migration's verification, not as a permanent part of the
/// test suite.
/// </summary>
public static class PlayerSelectSceneValidation
{
    public static void ValidateStartScene()
    {
        string scenePath = "Assets/Scenes/level_00_start.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        bool ok = true;
        ok &= CheckNoMissingScripts(scene);
        ok &= CheckStartManagerAndUiObjects();

        Debug.Log(ok ? "PLAYER_SELECT_SCENE_VALIDATION: PASS" : "PLAYER_SELECT_SCENE_VALIDATION: FAIL");
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool CheckNoMissingScripts(Scene scene)
    {
        bool ok = true;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        Debug.LogError($"PLAYER_SELECT_SCENE_VALIDATION: missing script on {GetPath(transform)}");
                        ok = false;
                    }
                }
            }
        }

        return ok;
    }

    private static bool CheckStartManagerAndUiObjects()
    {
        StartManager startManager = Object.FindAnyObjectByType<StartManager>(FindObjectsInactive.Include);
        StartMenuUiObjects uiObjects = Object.FindAnyObjectByType<StartMenuUiObjects>(FindObjectsInactive.Include);

        bool ok = true;
        if (startManager == null)
        {
            Debug.LogError("PLAYER_SELECT_SCENE_VALIDATION: no StartManager found in the start scene.");
            ok = false;
        }

        if (uiObjects == null)
        {
            Debug.LogError("PLAYER_SELECT_SCENE_VALIDATION: no StartMenuUiObjects found in the start scene.");
            return false;
        }

        ok &= RequireField(uiObjects.column1_subgroup_column2_player_select_name_text, "column1_subgroup_column2_player_select_name_text");
        ok &= RequireField(uiObjects.column2_players_tab_player_selected_image, "column2_players_tab_player_selected_image");
        ok &= RequireField(uiObjects.column2_players_tab_lock, "column2_players_tab_lock");
        ok &= RequireField(uiObjects.column3_player_selected_stats_numbers_text, "column3_player_selected_stats_numbers_text");
        ok &= RequireField(uiObjects.column3_player_selected_progression_stats_text, "column3_player_selected_progression_stats_text");
        ok &= RequireField(uiObjects.column3_player_selected_progression_update_points_text, "column3_player_selected_progression_update_points_text");
        ok &= RequireField(uiObjects.column1_subgroup_column2_num_players_selected_name_text, "column1_subgroup_column2_num_players_selected_name_text");
        ok &= RequireField(uiObjects.column4_cpu_selected_stats_numbers_text, "column4_cpu_selected_stats_numbers_text");
        ok &= RequireField(uiObjects.column4_cpu1_button, "column4_cpu1_button");
        ok &= RequireField(uiObjects.column4_cpu1_image, "column4_cpu1_image");
        ok &= RequireField(uiObjects.column4_cpu1_name_text, "column4_cpu1_name_text");
        ok &= RequireField(uiObjects.column4_cpu2_button, "column4_cpu2_button");
        ok &= RequireField(uiObjects.column4_cpu2_image, "column4_cpu2_image");
        ok &= RequireField(uiObjects.column4_cpu2_name_text, "column4_cpu2_name_text");
        ok &= RequireField(uiObjects.column4_cpu3_button, "column4_cpu3_button");
        ok &= RequireField(uiObjects.column4_cpu3_image, "column4_cpu3_image");
        ok &= RequireField(uiObjects.column4_cpu3_name_text, "column4_cpu3_name_text");

        return ok;
    }

    private static bool RequireField(Object value, string name)
    {
        if (value == null)
        {
            Debug.LogError($"PLAYER_SELECT_SCENE_VALIDATION: StartMenuUiObjects.{name} is not assigned - PlayerSelectView needs it.");
            return false;
        }

        return true;
    }

    private static string GetPath(Transform transform)
    {
        return string.Join("/", transform.GetComponentsInParent<Transform>(true).Reverse().Select(t => t.name));
    }
}
