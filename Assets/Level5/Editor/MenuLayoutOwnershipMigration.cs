using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-090: <c>level_00_options</c>/<c>OptionManager.prefab</c>,
/// <c>level_00_stats</c>/<c>StatsManager.prefab</c>,
/// <c>level_00_progression</c>/<c>progressionScreen.prefab</c> and
/// <c>level_00_credits</c>/<c>creditsManager.prefab</c> each carried dozens of
/// <see cref="PrefabUtility.GetPropertyModifications"/> entries on their prefab instance. Most of
/// them were stale: the scene's serialized override value was numerically identical to the current
/// source-prefab value, so the override did nothing except shadow a future prefab layout change. A
/// minority were genuine divergences - the scene really placed a control somewhere the prefab did not.
///
/// <see cref="Report"/>, <see cref="Normalize"/> and <see cref="ResolveDeliberateDivergences"/> are the
/// one-off AUD-090 actions and are safe to keep in the repository afterward purely as a record of how
/// the migration was performed, matching the convention <c>MenuUiObjectsWiring</c>/
/// <c>MenuSceneCleanup</c> already establish elsewhere in this folder.
///
/// <see cref="Classify"/> and <see cref="CollectForbiddenChildLayoutOverrides"/> are not one-off,
/// however: they are permanent infrastructure backing
/// <see cref="Level5ProjectValidator.CollectMenuLayoutOverrideContractErrors"/> and the
/// <c>PrefabDrivenMenuScreensDoNotOverridePrefabOwnedChildLayout</c> regression test. Do not remove
/// them when AUD-090 closes.
///
/// <see cref="PrefabUtility.GetPropertyModifications"/> returns each <c>PropertyModification.target</c>
/// as a reference into the loaded source Prefab Asset itself (not the scene instance), so classifying
/// and re-reading a modification's current prefab value never requires separately loading or walking
/// the prefab asset - the target *is* the prefab's live sub-object, and it stays valid across a scene
/// open/close because it belongs to an asset, not the scene. Removing a modification is done by
/// filtering <see cref="PrefabUtility.GetPropertyModifications"/>'s array and writing it back with
/// <see cref="PrefabUtility.SetPropertyModifications"/>, never by hand-editing YAML.
///
/// Idempotent: <see cref="Normalize"/> only ever removes redundant child-layout modifications and
/// <see cref="ResolveDeliberateDivergences"/> only ever resolves the documented divergences, so a scene
/// with none left is opened, found unchanged, and not saved.
/// </summary>
public static class MenuLayoutOwnershipMigration
{
    private readonly struct ScreenTarget
    {
        public readonly string ScenePath;
        public readonly string PrefabPath;

        public ScreenTarget(string scenePath, string prefabPath)
        {
            ScenePath = scenePath;
            PrefabPath = prefabPath;
        }
    }

    private static readonly ScreenTarget[] Targets =
    {
        new ScreenTarget(
            "Assets/Scenes/level_00_options.unity",
            "Assets/Resources/Prefabs/critical/OptionManager.prefab"),
        new ScreenTarget(
            "Assets/Scenes/level_00_stats.unity",
            "Assets/Resources/Prefabs/menu_stats/StatsManager.prefab"),
        new ScreenTarget(
            "Assets/Scenes/level_00_progression.unity",
            "Assets/Resources/Prefabs/menu_progression/progressionScreen.prefab"),
        new ScreenTarget(
            "Assets/Scenes/level_00_credits.unity",
            "Assets/Resources/Prefabs/menu_credits/creditsManager.prefab"),
    };

    /// <summary>The only properties AUD-090 treats as internal child layout authoring.</summary>
    private static readonly string[] ChildLayoutProperties =
    {
        "m_AnchorMin.x", "m_AnchorMin.y",
        "m_AnchorMax.x", "m_AnchorMax.y",
        "m_AnchoredPosition.x", "m_AnchoredPosition.y",
        "m_SizeDelta.x", "m_SizeDelta.y",
        "m_Pivot.x", "m_Pivot.y",
    };

    private const float FloatTolerance = 0.0001f;

    /// <summary>
    /// A (scene, hierarchy-path-prefix) pair naming a documented AUD-090 deliberate resolution, plus
    /// the exact number of child-layout properties it was measured to match when the resolution was
    /// authored. If a future rename/restructure of the named object makes the prefix stop matching, the
    /// actual count silently drops to 0 with no compiler or runtime signal - <see cref="ResolveTarget"/>
    /// compares against <see cref="ExpectedMatchCount"/> and warns rather than letting that go unnoticed.
    /// </summary>
    internal readonly struct HierarchyPrefixTarget
    {
        public readonly string ScenePath;
        public readonly string HierarchyPathPrefix;
        public readonly int ExpectedMatchCount;

        public HierarchyPrefixTarget(string scenePath, string hierarchyPathPrefix, int expectedMatchCount)
        {
            ScenePath = scenePath;
            HierarchyPathPrefix = hierarchyPathPrefix;
            ExpectedMatchCount = expectedMatchCount;
        }
    }

    /// <summary>
    /// AUD-090 deliberate resolution, case B (accidental drift - prefab is correct). Every child
    /// under <c>keyboardMouse_keys</c> is zeroed (anchor 0/0, anchoredPosition 0,0) in the scene,
    /// while the prefab holds a structured per-row vertical list - anchor pinned at 1 (top), a
    /// constant x=450, and a distinct y per row - matching the sibling keyboardOnly_keys/gamepad_keys
    /// /touch_keys panels. Zeroing an entire subtree uniformly collapses every row onto the same
    /// point; that is stale drift, not an authored layout. <c>OptionsManager</c> shows this panel
    /// purely via <c>SetActiveIfNotNull(keyboardMouseObject, ...)</c> (OptionsManager.cs:268) with no
    /// repositioning, so the scene's zeroed state would stack every keyboard+mouse control row on top
    /// of itself the moment that control scheme is selected. The prefab value is correct; these scene
    /// overrides are reverted even though they are not numerically redundant.
    /// </summary>
    private static readonly HierarchyPrefixTarget[] AccidentalDriftRevertPrefixes =
    {
        new HierarchyPrefixTarget(
            "Assets/Scenes/level_00_options.unity",
            "OptionManager/Canvas/columns/map/keyboardMouse_keys",
            expectedMatchCount: 60),
    };

    /// <summary>
    /// AUD-090 deliberate resolution, case A (the scene value is the intended reusable layout).
    /// Both <c>nft_airdrop</c> objects are active (<c>m_IsActive: 1</c>) and clickable -
    /// <c>CreditsManager.OpenNftAirdrop()</c> opens <c>webLinkNftAirdrop</c> - but the prefab
    /// collapses each to a zero-size point at its anchor origin (anchor 0/0, sizeDelta 0, position
    /// 0,0): a real, wired element authored with no layout at all. The scene already carries the
    /// sized, positioned values that render today. The prefab never received them because it was
    /// still binary-serialized (AUD-088) when this layout was authored, so the fix could only land in
    /// the scene. The scene value is correct and is pushed into the prefab; the now-redundant scene
    /// override is then removed by the same pass that removes ordinary redundant overrides.
    /// </summary>
    private static readonly HierarchyPrefixTarget[] ScenePushToPrefabPrefixes =
    {
        new HierarchyPrefixTarget(
            "Assets/Scenes/level_00_credits.unity",
            "creditsManager/Canvas/columns/column/nft_airdrop",
            expectedMatchCount: 5),
        new HierarchyPrefixTarget(
            "Assets/Scenes/level_00_credits.unity",
            "creditsManager/Canvas/columns/column.1/nft_airdrop",
            expectedMatchCount: 4),
    };

    internal enum Category
    {
        ChildLayout,
        RootComposition,
        Semantic,
        Unknown,
    }

    internal readonly struct Classified
    {
        public readonly PropertyModification Modification;
        public readonly Category Category;
        public readonly bool Redundant;
        public readonly string HierarchyPath;

        public Classified(PropertyModification modification, Category category, bool redundant, string hierarchyPath)
        {
            Modification = modification;
            Category = category;
            Redundant = redundant;
            HierarchyPath = hierarchyPath;
        }
    }

    /// <summary>Result of resolving a <see cref="ScreenTarget"/> to an open scene and its prefab
    /// instance. <see cref="Error"/> is null on success.</summary>
    private readonly struct ResolvedTarget
    {
        public readonly Scene Scene;
        public readonly GameObject InstanceRoot;
        public readonly GameObject PrefabAssetRoot;
        public readonly PropertyModification[] Modifications;
        public readonly string Error;

        private ResolvedTarget(
            Scene scene, GameObject instanceRoot, GameObject prefabAssetRoot,
            PropertyModification[] modifications, string error)
        {
            Scene = scene;
            InstanceRoot = instanceRoot;
            PrefabAssetRoot = prefabAssetRoot;
            Modifications = modifications;
            Error = error;
        }

        public bool Success => Error == null;

        public static ResolvedTarget Failure(string error)
        {
            return new ResolvedTarget(default, null, null, Array.Empty<PropertyModification>(), error);
        }

        public static ResolvedTarget Ok(
            Scene scene, GameObject instanceRoot, GameObject prefabAssetRoot, PropertyModification[] modifications)
        {
            return new ResolvedTarget(scene, instanceRoot, prefabAssetRoot, modifications, null);
        }
    }

    [MenuItem("Level5/Report Menu Layout Overrides")]
    public static void Report()
    {
        RunOverAllTargets(reportOnly: true);
    }

    /// <summary>
    /// Read-only contract check backing
    /// <see cref="Level5ProjectValidator.CollectMenuLayoutOverrideContractErrors"/>: after
    /// <see cref="Normalize"/> and <see cref="ResolveDeliberateDivergences"/> have run, none of the
    /// four prefab-driven menu instances should carry a child RectTransform layout override at all -
    /// redundant ones are removed automatically, and every genuine divergence found during AUD-090 was
    /// deliberately resolved into either the prefab or the scene. A child-layout override reappearing
    /// here means new drift, not a case this validator is meant to silently allow.
    ///
    /// A missing scene file or a missing/unresolvable prefab instance is also reported as an error
    /// here (unlike <see cref="Report"/>/<see cref="Normalize"/>, which merely log it) - this method
    /// backs a regression test, and silently returning zero errors when a target screen has gone
    /// missing entirely would be a worse failure than a false positive.
    ///
    /// Opens scenes additively and leaves already-open scenes alone, matching the pattern the other
    /// <c>Collect*ContractErrors</c> methods on <see cref="Level5ProjectValidator"/> use, so this is
    /// safe to call from the edit-mode test suite without disturbing the user's open scenes.
    /// </summary>
    public static List<string> CollectForbiddenChildLayoutOverrides()
    {
        List<string> errors = new List<string>();

        foreach (ScreenTarget target in Targets)
        {
            if (!System.IO.File.Exists(target.ScenePath))
            {
                errors.Add(target.ScenePath + " : scene file is missing.");
                continue;
            }

            Scene existing = SceneManager.GetSceneByPath(target.ScenePath);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            Scene scene = alreadyOpen
                ? existing
                : EditorSceneManager.OpenScene(target.ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject instanceRoot = FindPrefabInstanceRoot(scene, target.PrefabPath);
                if (instanceRoot == null)
                {
                    errors.Add(
                        target.ScenePath + " -> " + target.PrefabPath
                            + " : no matching prefab instance found in scene.");
                    continue;
                }

                GameObject prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath);
                if (prefabAssetRoot == null)
                {
                    errors.Add(target.ScenePath + " -> " + target.PrefabPath + " : could not load source prefab asset.");
                    continue;
                }

                PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
                    ?? Array.Empty<PropertyModification>();
                Dictionary<Object, SerializedObject> cache = new Dictionary<Object, SerializedObject>();

                foreach (PropertyModification modification in modifications)
                {
                    Classified item = Classify(modification, prefabAssetRoot, cache);
                    if (item.Category != Category.ChildLayout)
                    {
                        continue;
                    }

                    errors.Add(
                        target.ScenePath + " -> " + target.PrefabPath + " : " + item.HierarchyPath
                            + " overrides prefab-owned child layout property '" + modification.propertyPath
                            + "' (scene=" + modification.value + ", prefab="
                            + ReadCurrentPrefabValue(modification, cache) + "). Resolve with"
                            + " Level5/Normalize Menu Layout Overrides or Level5/Resolve Menu Layout Divergences.");
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

    [MenuItem("Level5/Normalize Menu Layout Overrides")]
    public static void Normalize()
    {
        RunOverAllTargets(reportOnly: false);
    }

    /// <summary>
    /// Applies the two AUD-090 deliberate-divergence resolutions documented on
    /// <see cref="AccidentalDriftRevertPrefixes"/> and <see cref="ScenePushToPrefabPrefixes"/>: pushes
    /// the scene's value into the prefab where the scene is authoritative, then removes both that
    /// (now-redundant) override and the documented accidental-drift override in the same pass that
    /// also removes ordinary redundant overrides. Idempotent for the same reason <see cref="Normalize"/>
    /// is: once a resolution has landed, the modification it targeted no longer exists to re-resolve.
    /// </summary>
    [MenuItem("Level5/Resolve Menu Layout Divergences")]
    public static void ResolveDeliberateDivergences()
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        StringBuilder summary = new StringBuilder();
        bool anyPrefabTouched = false;

        try
        {
            foreach (ScreenTarget target in Targets)
            {
                ResolveTarget(target, summary, ref anyPrefabTouched);
            }
        }
        finally
        {
            // Runs even if a later target throws after an earlier target already pushed a prefab
            // edit (EditorUtility.SetDirty'd but not yet saved) - otherwise a mid-run failure could
            // leave that edit unsaved in memory only, lost the moment the process exits.
            if (anyPrefabTouched)
            {
                AssetDatabase.SaveAssets();
            }

            if (originalSetup != null && originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        Debug.Log("MenuLayoutOwnershipMigration.ResolveDeliberateDivergences complete.\n" + summary);
    }

    private static void ResolveTarget(ScreenTarget target, StringBuilder summary, ref bool anyPrefabTouched)
    {
        ResolvedTarget resolved = OpenSingleAndResolveTarget(target);
        if (!resolved.Success)
        {
            summary.AppendLine(target.ScenePath + " -> " + target.PrefabPath + " : " + resolved.Error + ".");
            return;
        }

        Dictionary<Object, SerializedObject> cache = new Dictionary<Object, SerializedObject>();
        List<PropertyModification> kept = new List<PropertyModification>(resolved.Modifications.Length);
        List<string> divergentHierarchyPaths = new List<string>();
        int pushedCount = 0;
        int forcedRevertCount = 0;
        int redundantCount = 0;

        foreach (PropertyModification modification in resolved.Modifications)
        {
            Classified item = Classify(modification, resolved.PrefabAssetRoot, cache);
            if (item.Category != Category.ChildLayout)
            {
                kept.Add(modification);
                continue;
            }

            if (item.Redundant)
            {
                redundantCount++;
                continue;
            }

            divergentHierarchyPaths.Add(item.HierarchyPath);

            if (MatchesPrefix(target.ScenePath, item.HierarchyPath, ScenePushToPrefabPrefixes))
            {
                if (PushSceneValueToPrefab(modification, cache))
                {
                    pushedCount++;
                    anyPrefabTouched = true;
                    continue;
                }

                // Push failed (unreadable/non-float property) - keep the scene override rather than
                // silently discarding the only place this value was authored.
                Debug.LogError(
                    "MenuLayoutOwnershipMigration: could not push " + item.HierarchyPath + " "
                        + modification.propertyPath + " to the prefab; scene override kept.");
                kept.Add(modification);
                continue;
            }

            if (MatchesPrefix(target.ScenePath, item.HierarchyPath, AccidentalDriftRevertPrefixes))
            {
                forcedRevertCount++;
                continue;
            }

            kept.Add(modification);
        }

        // Once a documented resolution has landed, its modifications are gone from the scene for
        // good - a scene with zero remaining divergent items is the correct steady state, not a sign
        // the prefix stopped matching. Only compare counts while there is still something divergent to
        // check them against, so this catches a genuine partial/renamed match instead of firing on
        // every run forever after the first successful resolution.
        if (divergentHierarchyPaths.Count > 0)
        {
            WarnOnUnexpectedMatchCounts(target.ScenePath, divergentHierarchyPaths, ScenePushToPrefabPrefixes);
            WarnOnUnexpectedMatchCounts(target.ScenePath, divergentHierarchyPaths, AccidentalDriftRevertPrefixes);
        }

        int removedCount = resolved.Modifications.Length - kept.Count;
        if (removedCount == 0)
        {
            summary.AppendLine(target.ScenePath + " -> no deliberate resolutions applied (nothing left to resolve).");
            return;
        }

        PrefabUtility.SetPropertyModifications(resolved.InstanceRoot, kept.ToArray());
        EditorSceneManager.MarkSceneDirty(resolved.Scene);
        EditorSceneManager.SaveScene(resolved.Scene);
        summary.AppendLine(
            target.ScenePath + " -> pushed " + pushedCount + " value(s) to prefab, force-reverted "
                + forcedRevertCount + " accidental-drift override(s), removed " + redundantCount
                + " newly/already-redundant override(s).");
    }

    /// <summary>
    /// Compares how many divergent child-layout modifications actually matched each documented prefix
    /// targeting <paramref name="scenePath"/> against that prefix's <see cref="HierarchyPrefixTarget.
    /// ExpectedMatchCount"/>, and warns on a mismatch. A rename of the object a prefix targets makes
    /// the prefix stop matching anything with no other symptom - <see cref="ResolveDeliberateDivergences"/>
    /// would just log "no deliberate resolutions applied," which reads identically to the success case
    /// of there being nothing left to resolve.
    /// </summary>
    private static void WarnOnUnexpectedMatchCounts(
        string scenePath, List<string> divergentHierarchyPaths, HierarchyPrefixTarget[] entries)
    {
        foreach (HierarchyPrefixTarget entry in entries)
        {
            if (!string.Equals(entry.ScenePath, scenePath, StringComparison.Ordinal))
            {
                continue;
            }

            int actual = 0;
            foreach (string hierarchyPath in divergentHierarchyPaths)
            {
                if (MatchesSinglePrefix(hierarchyPath, entry.HierarchyPathPrefix))
                {
                    actual++;
                }
            }

            if (actual != entry.ExpectedMatchCount)
            {
                Debug.LogWarning(
                    "MenuLayoutOwnershipMigration: documented resolution '" + entry.HierarchyPathPrefix
                        + "' in " + scenePath + " matched " + actual + " divergent propert"
                        + (actual == 1 ? "y" : "ies") + ", expected " + entry.ExpectedMatchCount
                        + ". The target object may have been renamed or restructured since this"
                        + " resolution was authored - verify it is still doing what it claims to.");
            }
        }
    }

    internal static bool MatchesPrefix(string scenePath, string hierarchyPath, HierarchyPrefixTarget[] entries)
    {
        foreach (HierarchyPrefixTarget entry in entries)
        {
            if (string.Equals(entry.ScenePath, scenePath, StringComparison.Ordinal)
                && MatchesSinglePrefix(hierarchyPath, entry.HierarchyPathPrefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSinglePrefix(string hierarchyPath, string prefix)
    {
        return hierarchyPath == prefix || hierarchyPath.StartsWith(prefix + "/", StringComparison.Ordinal);
    }

    private static bool PushSceneValueToPrefab(PropertyModification modification, Dictionary<Object, SerializedObject> cache)
    {
        if (!float.TryParse(
                modification.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float sceneValue))
        {
            return false;
        }

        SerializedObject serializedObject = GetOrCreateSerializedObject(modification.target, cache);
        if (serializedObject == null)
        {
            return false;
        }

        SerializedProperty property = serializedObject.FindProperty(modification.propertyPath);
        if (property == null || property.propertyType != SerializedPropertyType.Float)
        {
            return false;
        }

        property.floatValue = sceneValue;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(modification.target);
        return true;
    }

    private static void RunOverAllTargets(bool reportOnly)
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        StringBuilder summary = new StringBuilder();
        int changedSceneCount = 0;

        try
        {
            foreach (ScreenTarget target in Targets)
            {
                ProcessTarget(target, reportOnly, summary, out bool changed);
                if (changed)
                {
                    changedSceneCount++;
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

        string verb = reportOnly ? "Report" : "Normalize";
        Debug.Log(
            "MenuLayoutOwnershipMigration." + verb + " complete."
                + (reportOnly ? string.Empty : " Changed " + changedSceneCount + " scene(s).")
                + "\n" + summary);
    }

    private static void ProcessTarget(ScreenTarget target, bool reportOnly, StringBuilder summary, out bool changed)
    {
        changed = false;

        ResolvedTarget resolved = OpenSingleAndResolveTarget(target);
        if (!resolved.Success)
        {
            summary.AppendLine(target.ScenePath + " -> " + target.PrefabPath + " : " + resolved.Error + ".");
            return;
        }

        Dictionary<Object, SerializedObject> cache = new Dictionary<Object, SerializedObject>();
        List<Classified> classified = new List<Classified>(resolved.Modifications.Length);
        foreach (PropertyModification modification in resolved.Modifications)
        {
            classified.Add(Classify(modification, resolved.PrefabAssetRoot, cache));
        }

        int redundantChildLayout = 0;
        int divergentChildLayout = 0;
        int rootComposition = 0;
        int semantic = 0;
        int unknown = 0;
        List<Classified> divergentDetails = new List<Classified>();
        List<Classified> unknownDetails = new List<Classified>();

        foreach (Classified item in classified)
        {
            switch (item.Category)
            {
                case Category.ChildLayout:
                    if (item.Redundant)
                    {
                        redundantChildLayout++;
                    }
                    else
                    {
                        divergentChildLayout++;
                        divergentDetails.Add(item);
                    }

                    break;
                case Category.RootComposition:
                    rootComposition++;
                    break;
                case Category.Semantic:
                    semantic++;
                    break;
                default:
                    unknown++;
                    unknownDetails.Add(item);
                    break;
            }
        }

        summary.AppendLine(target.ScenePath + " -> " + target.PrefabPath);
        summary.AppendLine(
            "  total=" + resolved.Modifications.Length
                + " redundantChildLayout=" + redundantChildLayout
                + " divergentChildLayout=" + divergentChildLayout
                + " rootComposition=" + rootComposition
                + " semantic=" + semantic
                + " unknown=" + unknown);

        foreach (Classified item in divergentDetails)
        {
            summary.AppendLine(
                "  DIVERGENT " + item.HierarchyPath + " " + item.Modification.propertyPath
                    + " scene=" + item.Modification.value + " prefab="
                    + ReadCurrentPrefabValue(item.Modification, cache));
        }

        foreach (Classified item in unknownDetails)
        {
            summary.AppendLine(
                "  UNKNOWN " + item.HierarchyPath + " " + item.Modification.propertyPath
                    + " value=" + item.Modification.value
                    + " target=" + (item.Modification.target != null ? item.Modification.target.GetType().Name : "null"));
        }

        if (reportOnly || redundantChildLayout == 0)
        {
            return;
        }

        List<PropertyModification> kept = new List<PropertyModification>(resolved.Modifications.Length - redundantChildLayout);
        for (int i = 0; i < resolved.Modifications.Length; i++)
        {
            if (classified[i].Category == Category.ChildLayout && classified[i].Redundant)
            {
                continue;
            }

            kept.Add(resolved.Modifications[i]);
        }

        PrefabUtility.SetPropertyModifications(resolved.InstanceRoot, kept.ToArray());
        EditorSceneManager.MarkSceneDirty(resolved.Scene);
        EditorSceneManager.SaveScene(resolved.Scene);
        changed = true;
        summary.AppendLine("  removed " + redundantChildLayout + " redundant child-layout override(s), scene saved.");
    }

    /// <summary>
    /// Opens <paramref name="target"/>'s scene in Single mode and resolves its prefab instance, shared
    /// by <see cref="ProcessTarget"/> and <see cref="ResolveTarget"/> so scene-opening, instance
    /// resolution and the accompanying safety checks live in exactly one place instead of two
    /// independently-maintained copies.
    ///
    /// Refuses to open when any currently loaded scene has unsaved changes, since
    /// <see cref="OpenSceneMode.Single"/> unloads every loaded scene - without this guard, running
    /// this from an interactive session with unrelated unsaved edits open would silently discard them.
    /// </summary>
    private static ResolvedTarget OpenSingleAndResolveTarget(ScreenTarget target)
    {
        if (!System.IO.File.Exists(target.ScenePath))
        {
            return ResolvedTarget.Failure("missing scene file");
        }

        if (AnyLoadedSceneIsDirty())
        {
            return ResolvedTarget.Failure(
                "a currently loaded scene has unsaved changes; save or close it first - opening in"
                    + " Single mode would otherwise discard it");
        }

        Scene scene = EditorSceneManager.OpenScene(target.ScenePath, OpenSceneMode.Single);
        GameObject instanceRoot = FindPrefabInstanceRoot(scene, target.PrefabPath);
        if (instanceRoot == null)
        {
            return ResolvedTarget.Failure("no matching prefab instance found in scene");
        }

        GameObject prefabAssetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabPath);
        if (prefabAssetRoot == null)
        {
            return ResolvedTarget.Failure("could not load source prefab asset");
        }

        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
            ?? Array.Empty<PropertyModification>();
        return ResolvedTarget.Ok(scene, instanceRoot, prefabAssetRoot, modifications);
    }

    private static bool AnyLoadedSceneIsDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                return true;
            }
        }

        return false;
    }

    internal static Classified Classify(
        PropertyModification modification, GameObject prefabAssetRoot, Dictionary<Object, SerializedObject> cache)
    {
        GameObject targetGameObject = GetTargetGameObject(modification.target);
        string hierarchyPath = targetGameObject != null
            ? BuildHierarchyPath(targetGameObject, prefabAssetRoot)
            : "<unresolved target>";

        bool isRoot = targetGameObject != null && targetGameObject == prefabAssetRoot;
        if (isRoot)
        {
            return new Classified(modification, Category.RootComposition, false, hierarchyPath);
        }

        bool isChildLayoutProperty = Array.IndexOf(ChildLayoutProperties, modification.propertyPath) >= 0;
        bool targetIsRectTransform = modification.target is RectTransform;

        if (isChildLayoutProperty && targetIsRectTransform)
        {
            bool redundant = TryReadCurrentFloat(modification.target, modification.propertyPath, cache, out float currentValue)
                && float.TryParse(modification.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float sceneValue)
                && Mathf.Abs(currentValue - sceneValue) <= FloatTolerance;

            return new Classified(modification, Category.ChildLayout, redundant, hierarchyPath);
        }

        if (IsRecognizedSemanticProperty(modification))
        {
            return new Classified(modification, Category.Semantic, false, hierarchyPath);
        }

        // m_RootOrder/m_LocalPosition/m_LocalRotation/m_LocalEulerAnglesHint/m_LocalScale are root
        // composition only when they are actually on the prefab's root object - the isRoot branch
        // above already returned for that case, so reaching here means a non-root Transform, which
        // this contract has no safe bucket for. Falling through to Unknown (rather than folding it
        // into RootComposition, which CollectForbiddenChildLayoutOverrides never inspects) keeps a
        // genuine non-root Transform override visible in Report() instead of silently exempting it.
        return new Classified(modification, Category.Unknown, false, hierarchyPath);
    }

    private static bool IsRecognizedSemanticProperty(PropertyModification modification)
    {
        string path = modification.propertyPath;
        if (path == "m_IsActive" || path == "m_Name" || path == "m_Enabled")
        {
            return true;
        }

        if (modification.target is UnityEngine.UI.Text && path.StartsWith("m_Text", StringComparison.Ordinal))
        {
            return true;
        }

        if (modification.target is UnityEngine.UI.Graphic
            && (path.StartsWith("m_Color", StringComparison.Ordinal) || path == "m_Material" || path == "m_Sprite"))
        {
            return true;
        }

        if (modification.target is UnityEngine.UI.Selectable
            && (path == "m_Interactable" || path.StartsWith("m_Colors", StringComparison.Ordinal)))
        {
            return true;
        }

        if (path.StartsWith("m_OnClick", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static GameObject GetTargetGameObject(Object target)
    {
        if (target is GameObject gameObject)
        {
            return gameObject;
        }

        if (target is Component component)
        {
            return component.gameObject;
        }

        return null;
    }

    /// <summary>
    /// Walks from <paramref name="target"/> up to <paramref name="prefabAssetRoot"/>, building the
    /// path root-to-leaf in a single pass (append + reverse) rather than repeatedly inserting at index
    /// 0, which is O(n) per call and O(n^2) overall for an n-deep hierarchy.
    ///
    /// Warns if the walk runs out of parents without ever reaching <paramref name="prefabAssetRoot"/>:
    /// that should never happen for a modification whose target genuinely belongs to this prefab asset,
    /// so hitting it means the returned path is incomplete and callers matching against it - the
    /// AUD-090 deliberate-resolution prefixes in particular - may silently fail to match.
    /// </summary>
    private static string BuildHierarchyPath(GameObject target, GameObject prefabAssetRoot)
    {
        List<string> segments = new List<string>();
        Transform current = target.transform;
        bool reachedRoot = false;
        while (current != null)
        {
            segments.Add(current.name);
            if (current.gameObject == prefabAssetRoot)
            {
                reachedRoot = true;
                break;
            }

            current = current.parent;
        }

        if (!reachedRoot)
        {
            Debug.LogWarning(
                "MenuLayoutOwnershipMigration: hierarchy path for '" + target.name
                    + "' did not resolve back to prefab root '" + prefabAssetRoot.name
                    + "' - the resulting path may be incomplete.");
        }

        segments.Reverse();
        return string.Join("/", segments.ToArray());
    }

    private static SerializedObject GetOrCreateSerializedObject(Object target, Dictionary<Object, SerializedObject> cache)
    {
        if (target == null)
        {
            return null;
        }

        if (cache.TryGetValue(target, out SerializedObject serializedObject) && serializedObject.targetObject != null)
        {
            return serializedObject;
        }

        serializedObject = new SerializedObject(target);
        cache[target] = serializedObject;
        return serializedObject;
    }

    /// <summary>
    /// Reads <paramref name="propertyPath"/>'s current float value directly off <paramref name="target"/>
    /// (a live sub-object of the source prefab asset, per <see cref="PrefabUtility.GetPropertyModifications"/>'s
    /// contract - never the scene instance, so this never observes a LayoutGroup's runtime-driven
    /// RectTransform state). <paramref name="cache"/> keeps one <see cref="SerializedObject"/> per
    /// target for the duration of a single target-scene pass, since a RectTransform typically has
    /// several of its ten child-layout properties overridden at once and would otherwise be wrapped
    /// freshly for each one.
    /// </summary>
    private static bool TryReadCurrentFloat(
        Object target, string propertyPath, Dictionary<Object, SerializedObject> cache, out float value)
    {
        value = 0f;
        SerializedObject serializedObject = GetOrCreateSerializedObject(target, cache);
        if (serializedObject == null)
        {
            return false;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property == null || property.propertyType != SerializedPropertyType.Float)
        {
            return false;
        }

        value = property.floatValue;
        return true;
    }

    private static string ReadCurrentPrefabValue(PropertyModification modification, Dictionary<Object, SerializedObject> cache)
    {
        return TryReadCurrentFloat(modification.target, modification.propertyPath, cache, out float value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : "<unreadable>";
    }

    /// <summary>
    /// Walks every Transform in the scene (active or not) looking for the outermost prefab instance
    /// root whose nearest source prefab is <paramref name="prefabAssetPath"/>. Does not assume the
    /// instance is itself a scene root GameObject - it is not, in any of the four target scenes.
    /// </summary>
    private static GameObject FindPrefabInstanceRoot(Scene scene, string prefabAssetPath)
    {
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObject candidateObject = candidate.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(candidateObject))
                {
                    continue;
                }

                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidateObject);
                if (string.Equals(path, prefabAssetPath, StringComparison.Ordinal))
                {
                    return candidateObject;
                }
            }
        }

        return null;
    }
}
