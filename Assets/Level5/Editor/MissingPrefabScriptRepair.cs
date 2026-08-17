using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs the prefabs that <c>Level5/Reserialize Binary Assets</c> cannot convert because Unity
/// refuses to save a prefab containing a missing script.
///
/// What each missing script actually was, identified from the type names still embedded in the
/// binary asset:
///
/// <list type="bullet">
/// <item><c>camera/Cameras.prefab</c> and <c>camera/Cameras _Mobile.prefab</c> -
/// <c>PostProcessLayer</c> from the removed <c>com.unity.postprocessing</c> (PPv2) package. The
/// type tree still carries <c>temporalAntialiasing</c>, <c>subpixelMorphologicalAntialiasing</c>,
/// <c>fastApproximateAntialiasing</c>, <c>PostProcessDebugLayer</c> and <c>LightMeterMonitor</c>.
/// The same prefabs already carry <c>UniversalAdditionalCameraData</c>, so these are URP-migration
/// leftovers.</item>
/// <item><c>they_live/Sunglasses.prefab</c> - <c>PostProcessVolume</c> from the same package
/// (<c>sharedProfile</c>, <c>isGlobal</c>, <c>blendDistance</c>, <c>weight</c>, <c>priority</c>), on
/// a GameObject literally named <c>PostProcessing</c>.</item>
/// </list>
///
/// PPv2 is not in <c>Packages/manifest.json</c> and no script in the project references
/// <c>PostProcessLayer</c> or <c>PostProcessVolume</c>, so these components are inert. Removing them
/// changes no rendering behaviour - URP has been doing the work since the migration.
///
/// Deliberately NOT handled here:
/// <list type="bullet">
/// <item><c>critical/NavMesh.prefab</c> - the missing script is <c>NavMeshSurface</c>
/// (<c>m_AgentTypeID</c>, <c>m_CollectObjects</c>, <c>m_OverrideVoxelSize</c>, <c>m_NavMeshData</c>).
/// <c>Assets/NavMeshComponents/Scripts/NavMeshSurface.cs</c> still exists, under a different GUID, so
/// the component is recoverable. The prefab is used by <c>level_12_theater</c>. Removing it would
/// discard that level's bake settings and its baked <c>m_NavMeshData</c> reference. Reassign the
/// script in the Inspector instead - dropping NavMeshSurface.cs onto the missing-script slot keeps
/// the serialized values, because the field names still match.</item>
/// <item>The three orphans - <c>menu_start/StartManager.prefab</c> (a Unity 2020.2 copy of the old
/// StartManager, superseded by <c>start_manager_test.prefab</c>),
/// <c>enemy_misc/enemyShotMarker.prefab</c> (its script is <c>BasketBallShotMarkerAuto</c>, which now
/// lives in <c>basketball/Legacy~/</c> and so is excluded from compilation), and
/// <c>auto_players/enemy_executioner_auto.prefab</c>. Nothing references any of them. Deleting the
/// prefabs is the right fix, but that is a deletion and belongs to whoever owns those assets.</item>
/// </list>
/// </summary>
public static class MissingPrefabScriptRepair
{
    /// <summary>
    /// Prefabs whose only missing scripts are confirmed dead PPv2 components.
    /// </summary>
    private static readonly string[] PostProcessingLeftoverPrefabs =
    {
        "Assets/Resources/Prefabs/camera/Cameras.prefab",
        "Assets/Resources/Prefabs/camera/Cameras _Mobile.prefab",
        "Assets/Resources/Prefabs/they_live/Sunglasses.prefab",
    };

    /// <summary>Every prefab currently blocked, for reporting.</summary>
    private static readonly string[] AllBlockedPrefabs =
    {
        "Assets/Resources/Prefabs/camera/Cameras.prefab",
        "Assets/Resources/Prefabs/camera/Cameras _Mobile.prefab",
        "Assets/Resources/Prefabs/they_live/Sunglasses.prefab",
        "Assets/Resources/Prefabs/critical/NavMesh.prefab",
        "Assets/Resources/Prefabs/menu_start/StartManager.prefab",
        "Assets/Resources/Prefabs/enemy_misc/enemyShotMarker.prefab",
        "Assets/Resources/Prefabs/auto_players/enemy_executioner_auto.prefab",
    };

    [MenuItem("Level5/Report Missing Prefab Scripts")]
    public static void ReportMissingScripts()
    {
        foreach (string path in AllBlockedPrefabs)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning("Could not load " + path + ".");
                continue;
            }

            List<string> holders = new List<string>();
            CollectMissingScriptHolders(asset.transform, asset.transform, holders);
            Debug.Log(
                holders.Count == 0
                    ? path + ": no missing scripts."
                    : path + ": " + holders.Count + " missing script(s) on -> " + string.Join(", ", holders.ToArray()));
        }
    }

    /// <summary>
    /// Strips the dead PPv2 components so the prefabs can be reserialized to text. Only touches the
    /// three prefabs whose missing scripts are confirmed to be PostProcessLayer/PostProcessVolume.
    /// </summary>
    [MenuItem("Level5/Remove Dead PostProcessing Components")]
    public static void RemoveDeadPostProcessingComponents()
    {
        int total = 0;
        foreach (string path in PostProcessingLeftoverPrefabs)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                Debug.LogWarning("Could not open " + path + " for editing.");
                continue;
            }

            try
            {
                int removed = RemoveMissingScriptsRecursively(contents.transform);
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    total += removed;
                    Debug.Log("Removed " + removed + " missing PPv2 component(s) from " + path + ".");
                }
                else
                {
                    Debug.Log("No missing scripts left in " + path + ".");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        if (total > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Removed " + total + " dead components. Run Level5/Reserialize Binary Assets again to"
                    + " convert these prefabs to text, then drop them from"
                    + " Level5ProjectValidator.BinaryAssetsBlockedByMissingScripts.");
        }
    }

    private static int RemoveMissingScriptsRecursively(Transform node)
    {
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
        for (int i = 0; i < node.childCount; i++)
        {
            removed += RemoveMissingScriptsRecursively(node.GetChild(i));
        }

        return removed;
    }

    private static void CollectMissingScriptHolders(Transform root, Transform node, List<string> holders)
    {
        Component[] components = node.GetComponents<Component>();
        int missing = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missing++;
            }
        }

        if (missing > 0)
        {
            holders.Add(PathFrom(root, node) + " (" + missing + ")");
        }

        for (int i = 0; i < node.childCount; i++)
        {
            CollectMissingScriptHolders(root, node.GetChild(i), holders);
        }
    }

    private static string PathFrom(Transform root, Transform node)
    {
        string path = node.name;
        Transform current = node.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
