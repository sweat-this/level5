using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 1: migrates <c>OptionManager.prefab</c>'s 103 legacy <see cref="Text"/> components to
/// <see cref="TextMeshProUGUI"/> on a project-owned Neon Pixel-7 SDF font asset, the pilot for the wider
/// menu-text migration (see docs/ui-menu-audit-2026-08-17.md AUD-092 and
/// docs/ui-input-architecture.md Phase 6). Deliberately scoped to the Options screen only.
///
/// Mirrors <see cref="MenuLayoutOwnershipMigration"/>'s shape: <see cref="PrefabUtility"/>/
/// <see cref="SerializedObject"/> only (never hand-edited YAML), one-off menu-item actions plus
/// permanent <see cref="CollectContractErrors"/> infrastructure that stays after the migration lands.
/// </summary>
public static class MenuTextMeshProMigration
{
    private const string PrefabPath = "Assets/Resources/Prefabs/critical/OptionManager.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_options.unity";
    private const string SourceFontPath = "Assets/Fonts/neon_pixel-7.ttf";
    private const string FontAssetFolder = "Assets/Fonts/TMP";
    private const string FontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    // ---------------------------------------------------------------------------------------------
    // Step 1: TMP Essential Resources
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Imports the same "TMP Essential Resources.unitypackage" that
    /// <c>Window &gt; TextMeshPro &gt; Import TMP Essential Resources</c> imports
    /// (<see cref="TMP_PackageUtilities.ImportProjectResourcesMenu"/>), via
    /// <see cref="AssetDatabase.ImportPackage"/> with <c>interactive: false</c> so it is safe to run
    /// from batchmode. Idempotent: a project that already has a <see cref="TMP_Settings"/> asset is left
    /// untouched.
    ///
    /// <see cref="AssetDatabase.ImportPackage"/> is asynchronous - it only finishes over subsequent
    /// Editor update ticks. Called from an interactive Editor that is harmless (the import completes
    /// naturally while the user keeps working); called from a one-shot <c>-executeMethod</c> batchmode
    /// invocation with <c>-quit</c>, the process was observed to exit before the import actually landed
    /// anything on disk, silently producing nothing despite this method's own log line claiming success.
    /// In batchmode this now waits for <see cref="AssetDatabase.importPackageCompleted"/> and exits the
    /// process itself from that callback, and also self-exits on the already-present no-op path - so a
    /// batchmode caller of this method must omit <c>-quit</c> in both cases, since which path is taken
    /// is not known in advance.
    /// </summary>
    [MenuItem("Level5/Import TMP Essential Resources")]
    public static void ImportTmpEssentialResourcesIfNeeded()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
        {
            Debug.Log("MenuTextMeshProMigration: TMP Essential Resources already present; nothing to import.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }

            return;
        }

        string packagePath = TMP_EditorUtility.packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";

        if (Application.isBatchMode)
        {
            AssetDatabase.importPackageCompleted += OnBatchModeEssentialResourcesImportCompleted;
        }

        AssetDatabase.ImportPackage(packagePath, false);
        Debug.Log("MenuTextMeshProMigration: importing TMP Essential Resources from " + packagePath);
    }

    private static void OnBatchModeEssentialResourcesImportCompleted(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnBatchModeEssentialResourcesImportCompleted;
        AssetDatabase.SaveAssets();
        Debug.Log("MenuTextMeshProMigration: TMP Essential Resources import completed; exiting batchmode process.");
        EditorApplication.Exit(0);
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: Neon Pixel-7 SDF font asset
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Creates (or reuses) <see cref="FontAssetPath"/> from <see cref="SourceFontPath"/> using
    /// <see cref="TMP_FontAsset.CreateFontAsset(Font,int,int,GlyphRenderMode,int,int,AtlasPopulationMode,bool)"/>
    /// - the same programmatic entry point the Font Asset Creator window itself is built on - with
    /// <see cref="AtlasPopulationMode.Dynamic"/>. Dynamic mode keeps the source font referenced and
    /// renders glyphs on demand rather than baking a fixed character set, which is what guarantees no
    /// missing glyphs regardless of which strings this font ends up rendering across this and later
    /// AUD-092 phases. Uses TMP's own SDF atlas/material generation exclusively.
    /// </summary>
    [MenuItem("Level5/Create Neon Pixel TMP Font Asset")]
    public static TMP_FontAsset EnsureNeonPixelFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null && existing.atlasPopulationMode == AtlasPopulationMode.Dynamic && existing.sourceFontFile != null)
        {
            return existing;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError("MenuTextMeshProMigration: could not load source font at " + SourceFontPath);
            return null;
        }

        if (!AssetDatabase.IsValidFolder(FontAssetFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(FontAssetFolder).Replace('\\', '/'), Path.GetFileName(FontAssetFolder));
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null)
        {
            Debug.LogError("MenuTextMeshProMigration: TMP_FontAsset.CreateFontAsset returned null for " + SourceFontPath);
            return null;
        }

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

        Debug.Log("MenuTextMeshProMigration: created " + FontAssetPath + " (Dynamic atlas, source " + SourceFontPath + ").");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: resolve the two scene m_Text overrides before any component is touched
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>level_00_options.unity</c> carries two <c>m_Text</c> prefab-instance overrides against
    /// legacy <see cref="Text"/> components ("space, left mouse", "shoot"). Converting those Text
    /// components to TMP without resolving this first would leave a dangling override pointing at a
    /// destroyed component's fileID. This writes each override's effective string into the prefab's own
    /// <see cref="Text.text"/> default (so the prefab becomes the sole source of truth) and removes the
    /// now-redundant scene override, leaving every other modification (root composition, m_IsActive,
    /// AUD-090's resolved layout) untouched. Idempotent: a second run finds nothing left to resolve.
    /// </summary>
    [MenuItem("Level5/Resolve Options Scene Text Overrides")]
    public static void ResolveSceneTextOverrides()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("MenuTextMeshProMigration.ResolveSceneTextOverrides: scene file is missing: " + ScenePath);
            return;
        }

        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyOpen = existing.IsValid() && existing.isLoaded;
        Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject instanceRoot = FindPrefabInstanceRoot(scene, PrefabPath);
            if (instanceRoot == null)
            {
                Debug.LogError(
                    "MenuTextMeshProMigration.ResolveSceneTextOverrides: no OptionManager prefab instance found in "
                        + ScenePath);
                return;
            }

            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
                ?? Array.Empty<PropertyModification>();
            List<PropertyModification> kept = new List<PropertyModification>(modifications.Length);
            int resolvedCount = 0;
            bool prefabDirty = false;

            foreach (PropertyModification modification in modifications)
            {
                if (modification.propertyPath == "m_Text" && modification.target is Text legacyText)
                {
                    SerializedObject serializedObject = new SerializedObject(legacyText);
                    SerializedProperty property = serializedObject.FindProperty("m_Text");
                    if (property != null && property.stringValue != modification.value)
                    {
                        property.stringValue = modification.value;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(legacyText);
                        prefabDirty = true;
                    }

                    resolvedCount++;
                    continue; // drop this now-redundant override
                }

                kept.Add(modification);
            }

            if (resolvedCount == 0)
            {
                Debug.Log("MenuTextMeshProMigration.ResolveSceneTextOverrides: nothing to resolve.");
                return;
            }

            if (prefabDirty)
            {
                AssetDatabase.SaveAssets();
            }

            PrefabUtility.SetPropertyModifications(instanceRoot, kept.ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "MenuTextMeshProMigration.ResolveSceneTextOverrides: resolved " + resolvedCount
                    + " scene text override(s) into the prefab.");
        }
        finally
        {
            if (!alreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Read-only characterization of every legacy <see cref="Text"/> in <see cref="PrefabPath"/>: full
    /// visual property snapshot, which <see cref="Selectable"/>s target it (the one supported reference
    /// consumer), and a scan of every other component's serialized object-reference properties for any
    /// unsupported consumer this migration would need to handle explicitly. Never modifies the asset.
    /// </summary>
    [MenuItem("Level5/Report Options TMP Migration")]
    public static void Report()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            StringBuilder summary = new StringBuilder();
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            summary.AppendLine(
                PrefabPath + " : " + texts.Count + " legacy Text component(s) owned directly by this prefab, "
                    + nestedTexts.Count + " more inside nested prefab instance(s) (out of scope for this migration).");

            HashSet<Object> textSet = new HashSet<Object>(allTexts);

            foreach (Text text in nestedTexts)
            {
                summary.AppendLine(
                    "  SKIPPED (nested prefab instance " + PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject).name
                        + "): " + BuildHierarchyPath(text.gameObject, root));
            }

            foreach (Text text in texts)
            {
                string path = BuildHierarchyPath(text.gameObject, root);
                summary.AppendLine(
                    "  " + path
                        + " text=\"" + Truncate(text.text, 40) + "\""
                        + " font=" + (text.font != null ? text.font.name : "<none>")
                        + " size=" + text.fontSize
                        + " style=" + text.fontStyle
                        + " align=" + text.alignment
                        + " color=" + text.color
                        + " raycastTarget=" + text.raycastTarget
                        + " maskable=" + text.maskable
                        + " richText=" + text.supportRichText
                        + " bestFit=" + text.resizeTextForBestFit
                        + (text.resizeTextForBestFit
                            ? " (min=" + text.resizeTextMinSize + " max=" + text.resizeTextMaxSize + ")"
                            : string.Empty)
                        + " hOverflow=" + text.horizontalOverflow
                        + " vOverflow=" + text.verticalOverflow
                        + " lineSpacing=" + text.lineSpacing
                        + " enabled=" + text.enabled
                        + (text.GetComponent<Outline>() != null ? " [has Outline]" : string.Empty));
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.targetGraphic != null && textSet.Contains(selectable.targetGraphic))
                {
                    summary.AppendLine(
                        "  SUPPORTED targetGraphic: " + BuildHierarchyPath(selectable.gameObject, root)
                            + " (" + selectable.GetType().Name + ") -> "
                            + BuildHierarchyPath(selectable.targetGraphic.gameObject, root));
                }
            }

            List<string> unsupportedConsumers = new List<string>();
            CollectUnsupportedConsumers(root, textSet, unsupportedConsumers);
            foreach (string finding in unsupportedConsumers)
            {
                summary.AppendLine("  UNSUPPORTED CONSUMER: " + finding);
            }

            Debug.Log("MenuTextMeshProMigration.Report complete.\n" + summary);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Scans every component under <paramref name="root"/> other than <see cref="Text"/> and
    /// <see cref="Selectable"/> (whose <c>targetGraphic</c> is the one recognized/supported consumer,
    /// already reported separately) for any serialized object-reference property pointing at one of
    /// <paramref name="textSet"/>. Finding one here means the migration design must explicitly handle it
    /// before the Text it references can be safely destroyed.
    /// </summary>
    private static void CollectUnsupportedConsumers(GameObject root, HashSet<Object> textSet, List<string> findings)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (Component component in transform.GetComponents<Component>())
            {
                if (component == null || component is Text || component is Selectable)
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (property.objectReferenceValue != null && textSet.Contains(property.objectReferenceValue))
                    {
                        findings.Add(
                            BuildHierarchyPath(component.gameObject, root) + " (" + component.GetType().Name
                                + "." + property.propertyPath + ") references a legacy Text component.");
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Migration
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for every legacy <see cref="Text"/> in
    /// <see cref="PrefabPath"/>. No-ops (logged) if none remain. Aborts without saving if any per-Text
    /// step fails or if a <see cref="Selectable"/> is left with a null <c>targetGraphic</c> it did not
    /// have before - <see cref="PrefabUtility.LoadPrefabContents"/> gives every mutation below a
    /// disposable scratch copy, so an abort here truly discards the attempt rather than leaving a
    /// partially-migrated prefab on disk.
    /// </summary>
    [MenuItem("Level5/Migrate Options To TMP")]
    public static void Migrate()
    {
        // Deliberately does not call ImportTmpEssentialResourcesIfNeeded() itself: that import is
        // asynchronous and, in batchmode, self-exits the process once it completes (see that method's
        // doc comment) - calling it from here would kill this method's own batch process mid-migration
        // whenever essentials were already present. Essential Resources are a real prerequisite (the
        // font asset's SDF material shader only exists once they are imported), so this requires them
        // to already be present rather than orchestrating the import inline.
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(
                "MenuTextMeshProMigration.Migrate: TMP Essential Resources are not present. Run"
                    + " Level5/Import TMP Essential Resources first, then re-run this.");
            return;
        }

        TMP_FontAsset font = EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError("MenuTextMeshProMigration.Migrate: could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            if (texts.Count == 0 && root.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                Debug.Log(
                    "MenuTextMeshProMigration.Migrate: no directly-owned legacy Text remains in " + PrefabPath
                        + "; nothing to do (" + nestedTexts.Count
                        + " Text component(s) inside nested prefab instances are intentionally left untouched).");
                return;
            }

            List<string> errors = new List<string>();
            int convertedCount = 0;
            int outlineCompensatedCount = 0;

            foreach (Text text in texts)
            {
                string path = BuildHierarchyPath(text.gameObject, root);
                if (text.resizeTextForBestFit)
                {
                    errors.Add(path + " has Best Fit enabled; this migration does not support autosizing conversion.");
                    continue;
                }

                bool hadEnabledOutline = text.TryGetComponent(out Outline outline) && outline.enabled;
                TextMeshProUGUI tmp = ConvertSingleText(root, text, font);
                if (tmp == null)
                {
                    errors.Add(path + " : conversion failed to add TextMeshProUGUI.");
                    continue;
                }

                convertedCount++;
                if (hadEnabledOutline)
                {
                    outlineCompensatedCount++;
                }
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (IsPartOfNestedPrefabInstance(selectable.gameObject, root))
                {
                    continue; // pre-existing state of a nested prefab instance (e.g. touch_joystick) - not this migration's concern
                }

                if (selectable.targetGraphic == null)
                {
                    errors.Add(
                        BuildHierarchyPath(selectable.gameObject, root) + " : " + selectable.GetType().Name
                            + " has a null targetGraphic after migration.");
                }
            }

            if (errors.Count > 0)
            {
                Debug.LogError(
                    "MenuTextMeshProMigration.Migrate aborted without saving - " + errors.Count + " error(s):\n- "
                        + string.Join("\n- ", errors));
                return;
            }

            PersistLooseUnderlayMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log(
                "MenuTextMeshProMigration.Migrate complete: converted " + convertedCount
                    + " Text component(s), compensated " + outlineCompensatedCount
                    + " legacy Outline effect(s) via TMP underlay material.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// <see cref="ApplyOutlineCompensation"/> assigns a brand-new, never-before-persisted
    /// <see cref="Material"/> clone to a <see cref="TextMeshProUGUI"/>'s <c>fontSharedMaterial</c>.
    /// <see cref="PrefabUtility.SaveAsPrefabAsset"/> was observed to silently drop such a reference
    /// rather than auto-embedding it - the first real migration run saved a prefab where every one of
    /// the 3 outline-compensated texts had quietly reverted to referencing the shared font asset
    /// material, with no trace of the clone or its <c>UNDERLAY_ON</c> keyword anywhere in the file - so
    /// each loose material is made a genuine, separately-persisted project asset (the same
    /// <see cref="AssetDatabase.CreateAsset"/> + reload pattern <see cref="EnsureNeonPixelFontAsset"/>
    /// uses for the font asset itself) before the prefab is saved. A material that is already a
    /// persisted asset (the ~100 unmodified texts still sharing the font asset's own material) is left
    /// alone. Deterministic, overwrite-safe path so a second migration attempt after a partial failure
    /// does not accumulate duplicate " 1"/" 2" assets.
    /// </summary>
    private static void PersistLooseUnderlayMaterials(GameObject root)
    {
        foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Material material = tmp.fontSharedMaterial;
            if (material == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
            {
                continue;
            }

            string path = FontAssetFolder + "/Neon Pixel-7 SDF - " + tmp.gameObject.name + " Underlay.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(material, path);
            tmp.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }

    /// <summary>
    /// Converts a single <paramref name="text"/> on its own GameObject to <see cref="TextMeshProUGUI"/>.
    /// Captures every visual property (and a same-GameObject legacy <see cref="Outline"/>'s effect, see
    /// <see cref="ApplyOutlineCompensation"/>) first, then destroys <paramref name="text"/> - Unity does
    /// not allow two <see cref="Graphic"/>-derived components on one GameObject at once ("A GameObject
    /// can only contain one 'Graphic' component" - observed as <c>AddComponent&lt;TextMeshProUGUI&gt;</c>
    /// silently returning null while the legacy <see cref="Text"/> was still present), so the add has to
    /// happen after the destroy despite every property already being captured beforehand. This still
    /// preserves the GameObject/RectTransform/CanvasRenderer/hierarchy/sibling index/active state, since
    /// none of those depend on the two components ever coexisting. Internal so the converter unit test
    /// can call it directly against a throwaway object without touching the real prefab.
    /// </summary>
    internal static TextMeshProUGUI ConvertSingleText(GameObject scopeRoot, Text text, TMP_FontAsset font)
    {
        if (text == null || font == null)
        {
            return null;
        }

        string content = text.text;
        FontStyles fontStyle = MapFontStyle(text.fontStyle);
        TextAlignmentOptions alignment = MapAlignment(text.alignment);
        Color color = text.color;
        float fontSize = text.fontSize;
        bool raycastTarget = text.raycastTarget;
        bool maskable = text.maskable;
        bool richText = text.supportRichText;
        bool enabledState = text.enabled;
        TextWrappingModes wrapping = text.horizontalOverflow == HorizontalWrapMode.Wrap
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        TextOverflowModes overflow = text.verticalOverflow == VerticalWrapMode.Truncate
            ? TextOverflowModes.Truncate
            : TextOverflowModes.Overflow;
        float lineSpacing = (text.lineSpacing - 1f) * 100f;

        List<Selectable> boundSelectables = new List<Selectable>();
        foreach (Selectable selectable in scopeRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (selectable.targetGraphic == text)
            {
                boundSelectables.Add(selectable);
            }
        }

        GameObject go = text.gameObject;
        Outline outline = go.GetComponent<Outline>();
        bool compensateOutline = outline != null && outline.enabled;
        Color outlineColor = compensateOutline ? outline.effectColor : default;
        Vector2 outlineDistance = compensateOutline ? outline.effectDistance : default;

        Object.DestroyImmediate(text, true);
        if (outline != null)
        {
            Object.DestroyImmediate(outline, true);
        }

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = raycastTarget;
        tmp.maskable = maskable;
        tmp.richText = richText;
        tmp.textWrappingMode = wrapping;
        tmp.overflowMode = overflow;
        tmp.lineSpacing = lineSpacing;
        tmp.enableAutoSizing = false;

        if (compensateOutline)
        {
            ApplyOutlineCompensation(tmp, outlineColor, outlineDistance);
        }

        foreach (Selectable selectable in boundSelectables)
        {
            selectable.targetGraphic = tmp;
        }

        tmp.enabled = enabledState;
        return tmp;
    }

    /// <summary>
    /// <see cref="TextMeshProUGUI"/> never calls <see cref="Graphic.OnPopulateMesh"/> /
    /// <c>IMeshModifier</c> the way legacy <see cref="Text"/> does (confirmed against the installed TMP
    /// source - it overrides only <c>GetModifiedMaterial</c>), so a same-GameObject
    /// <see cref="Outline"/> component - a directional soft drop-shadow here (black, 50% alpha, 1px
    /// offset), found on 3 active + 1 already-disabled label - would silently stop rendering if left in
    /// place. TMP's own SDF material has a directional equivalent for exactly this effect: the Underlay
    /// feature (<c>_UnderlayColor</c>/<c>_UnderlayOffsetX</c>/<c>_UnderlayOffsetY</c>/
    /// <c>_UnderlaySoftness</c>, gated by the <c>UNDERLAY_ON</c> keyword) - unlike <c>_OutlineWidth</c>,
    /// which is a uniform ring, not a directional shadow. Applied as an explicit clone of the font
    /// asset's shared material assigned via <see cref="TMP_Text.fontSharedMaterial"/> (not the
    /// <see cref="TMP_Text.fontMaterial"/> instance-getter round trip - that setter early-returns when
    /// <c>GetEntityId()</c> considers the assigned instance equivalent to the current one, which silently
    /// dropped this exact modification during Migrate's first real run: it saved a prefab with no
    /// embedded material and no <c>UNDERLAY_ON</c> keyword anywhere), so the shared font asset material
    /// used by the other ~100 texts is never touched, and <see cref="PrefabUtility.SaveAsPrefabAsset"/>
    /// embeds this genuinely distinct clone as its own sub-asset.
    /// </summary>
    private static void ApplyOutlineCompensation(TextMeshProUGUI tmp, Color outlineColor, Vector2 outlineDistance)
    {
        Material material = new Material(tmp.fontSharedMaterial);
        material.name = tmp.fontSharedMaterial.name + " (Underlay)";
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, outlineColor);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, Mathf.Clamp(outlineDistance.x, -1f, 1f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, Mathf.Clamp(outlineDistance.y, -1f, 1f));
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        tmp.fontSharedMaterial = material;
    }

    private static FontStyles MapFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions MapAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:
                return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:
                return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:
                return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight:
                return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft:
                return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:
                return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.Center;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectOptionsTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 1 permanent regression guard: zero legacy <see cref="Text"/> remain in
    /// <see cref="PrefabPath"/>, every <see cref="TextMeshProUGUI"/> has a font asset, no
    /// <see cref="Selectable"/> has a null <c>targetGraphic</c>, and no property modification in
    /// <see cref="ScenePath"/> still targets a legacy Text. Do not remove when Phase 1 closes - later
    /// phases add their own screen-specific equivalent rather than replacing this one.
    /// </summary>
    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(PrefabPath + " : could not load OptionManager prefab asset.");
            return errors;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(
                PrefabPath + " : " + ownedLegacyTexts.Count
                    + " legacy Text component(s) directly owned by this prefab remain (expected 0).");
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    PrefabPath + " -> " + BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : TextMeshProUGUI has no font asset.");
            }
        }

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (IsPartOfNestedPrefabInstance(selectable.gameObject, prefabRoot))
            {
                continue; // e.g. touch_joystick - a shared nested prefab instance, out of scope for this contract
            }

            if (selectable.targetGraphic == null)
            {
                errors.Add(
                    PrefabPath + " -> " + BuildHierarchyPath(selectable.gameObject, prefabRoot) + " : "
                        + selectable.GetType().Name + " has a null targetGraphic.");
            }
        }

        if (File.Exists(ScenePath))
        {
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject instanceRoot = FindPrefabInstanceRoot(scene, PrefabPath);
                if (instanceRoot != null)
                {
                    PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
                        ?? Array.Empty<PropertyModification>();
                    foreach (PropertyModification modification in modifications)
                    {
                        if (modification.propertyPath.StartsWith("m_Text", StringComparison.Ordinal)
                            && modification.target is Text)
                        {
                            errors.Add(
                                ScenePath + " : leftover legacy Text scene override on property '"
                                    + modification.propertyPath + "'.");
                        }
                    }
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

    // ---------------------------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// OptionManager.prefab nests one other prefab instance - <c>touch_joystick.prefab</c>, shared by
    /// seven other critical/menu prefabs - which <see cref="PrefabUtility.LoadPrefabContents"/> and
    /// <see cref="AssetDatabase.LoadAssetAtPath{T}"/> both resolve as part of the same hierarchy (its one
    /// <see cref="Text"/> is why a live component walk finds 104, not the 103 this prefab directly
    /// authors). Converting a shared nested prefab's Text from inside one screen's instance would create
    /// a per-instance add/remove-component override on that nested instance rather than a change to
    /// OptionManager's own content, and would leave every other screen using the joystick showing legacy
    /// Text while this one instance alone showed TMP - an unreviewed, out-of-scope divergence. Every
    /// Text/Selectable check in this migration is scoped to what <paramref name="root"/> directly owns
    /// via this predicate.
    /// </summary>
    private static bool IsPartOfNestedPrefabInstance(GameObject go, GameObject root)
    {
        GameObject nearestInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        return nearestInstanceRoot != null && nearestInstanceRoot != root;
    }

    private static void PartitionByNestedPrefabInstance(Text[] all, GameObject root, List<Text> owned, List<Text> nested)
    {
        foreach (Text text in all)
        {
            if (IsPartOfNestedPrefabInstance(text.gameObject, root))
            {
                nested.Add(text);
            }
            else
            {
                owned.Add(text);
            }
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Walks from <paramref name="target"/> up to <paramref name="root"/>, root-to-leaf. Same shape as
    /// <c>MenuLayoutOwnershipMigration.BuildHierarchyPath</c>, kept as an independent copy so the two
    /// one-off migration tools stay decoupled.
    /// </summary>
    private static string BuildHierarchyPath(GameObject target, GameObject root)
    {
        List<string> segments = new List<string>();
        Transform current = target.transform;
        while (current != null)
        {
            segments.Add(current.name);
            if (current.gameObject == root)
            {
                break;
            }

            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments.ToArray());
    }

    /// <summary>
    /// Walks every Transform in the scene (active or not) looking for the outermost prefab instance
    /// root whose nearest source prefab is <paramref name="prefabAssetPath"/>. Same approach as
    /// <c>MenuLayoutOwnershipMigration.FindPrefabInstanceRoot</c>.
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
