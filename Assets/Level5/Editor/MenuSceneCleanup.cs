using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AUD-101: the start menu scene carried touch-input controllers for screens that do not exist in
/// it - two <c>TouchInputAccountScreenController</c>, plus the stats, progression and generic
/// gameplay controllers. Each one's initializer ends in <c>gameObject.SetActive(false)</c> when it
/// cannot find its manager, so they sat there only to switch their own holder object off on load.
///
/// They live on dedicated holder objects (<c>touchInputAccountScreen</c>, <c>touchInputStatsScreen</c>,
/// and so on) rather than on shared UI, so removing them takes the holder with them. The start
/// screen's own controller stays until the touch controllers are retired as a set, which is gated on
/// device verification (AUD-100).
/// </summary>
public static class MenuSceneCleanup
{
    private const string StartScenePath = "Assets/Scenes/level_00_start.unity";

    /// <summary>Controllers whose manager is never present in the start scene.</summary>
    private static readonly string[] ForeignControllerTypeNames =
    {
        "TouchInputAccountScreenController",
        "TouchInputStatsScreenController",
        "TouchInputProgressionScreenController",
        "TouchInputController",
    };

    [MenuItem("Level5/Remove Foreign Touch Controllers From Start Scene")]
    public static void RemoveForeignTouchControllers()
    {
        Scene scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        List<GameObject> doomed = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (Array.IndexOf(ForeignControllerTypeNames, typeName) < 0)
                {
                    continue;
                }

                if (!doomed.Contains(behaviour.gameObject))
                {
                    doomed.Add(behaviour.gameObject);
                    Debug.Log(
                        "Removing '" + behaviour.gameObject.name + "' (" + typeName
                            + ") from " + StartScenePath + ".");
                }
            }
        }

        if (doomed.Count == 0)
        {
            Debug.Log("No foreign touch controllers found in " + StartScenePath + ".");
            return;
        }

        foreach (GameObject target in doomed)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Removed " + doomed.Count + " foreign touch controller objects.");
    }
}
