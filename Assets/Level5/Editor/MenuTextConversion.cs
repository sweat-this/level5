using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092: low-level Text -&gt; TextMeshProUGUI conversion mechanics shared by every per-screen menu
/// TMP migration (<see cref="MenuTextMeshProMigration"/> for Options, <see cref="StatsTextMeshProMigration"/>
/// for Stats). Extracted from the Options Phase 1 implementation verbatim - screen-specific orchestration
/// (which prefab/scene, which fields map to which UI view, the permanent per-screen contract) stays in
/// each screen's own migration class; only the mechanics proven safe by Phase 1 live here.
/// </summary>
internal static class MenuTextConversion
{
    private const string SourceFontPath = "Assets/Fonts/neon_pixel-7.ttf";
    private const string FontAssetFolder = "Assets/Fonts/TMP";
    private const string FontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    /// <summary>
    /// Creates (or reuses) the single project-wide Neon Pixel-7 SDF font asset every menu screen's TMP
    /// migration must share - see <see cref="MenuTextMeshProMigration.EnsureNeonPixelFontAsset"/>'s
    /// original doc comment for why <see cref="AtlasPopulationMode.Dynamic"/> is required.
    /// </summary>
    internal static TMP_FontAsset EnsureNeonPixelFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null && existing.atlasPopulationMode == AtlasPopulationMode.Dynamic && existing.sourceFontFile != null)
        {
            return existing;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError("MenuTextConversion: could not load source font at " + SourceFontPath);
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
            Debug.LogError("MenuTextConversion: TMP_FontAsset.CreateFontAsset returned null for " + SourceFontPath);
            return null;
        }

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

        Debug.Log("MenuTextConversion: created " + FontAssetPath + " (Dynamic atlas, source " + SourceFontPath + ").");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    /// <summary>
    /// Converts a single <paramref name="text"/> on its own GameObject to <see cref="TextMeshProUGUI"/>,
    /// preserving every visual property, same-GameObject legacy <see cref="Outline"/> effect (via TMP
    /// Underlay, see <see cref="ApplyOutlineCompensation"/>), and any <see cref="Selectable"/> whose
    /// <c>targetGraphic</c> pointed at it. See the original Phase 1 implementation for why the destroy
    /// must happen before the add (two <see cref="Graphic"/>-derived components cannot coexist).
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
    /// Mid-level orchestration shared by every screen migration's "convert everything, wire the named
    /// fields" shape (<see cref="StatsTextMeshProMigration.MigrateStatsManager"/> and
    /// <see cref="StatsTextMeshProMigration.MigrateHighScoreRow"/> - Options' single-prefab migration
    /// predates this and stays inline, matching its simpler one-target-object shape). Converts every
    /// Text in <paramref name="ownedTexts"/> via <see cref="ConvertSingleText"/>, resolves
    /// <paramref name="namedFields"/> by GameObject name against that same list (captured by the caller
    /// before conversion, so identity survives the Text -&gt; TextMeshProUGUI swap), and writes the
    /// resulting TextMeshProUGUI components into <paramref name="target"/>'s matching serialized fields.
    /// Returns false with <paramref name="errors"/> populated - and <paramref name="target"/> left
    /// unmodified - on any failure; the caller is expected to abort without saving.
    /// </summary>
    internal static bool ConvertOwnedTextsAndWireNamedFields(
        GameObject root,
        List<Text> ownedTexts,
        TMP_FontAsset font,
        (string GameObjectName, string FieldName)[] namedFields,
        Object target,
        bool checkSelectableTargetGraphics,
        out int convertedCount,
        out int outlineCompensatedCount,
        List<string> errors)
    {
        convertedCount = 0;
        outlineCompensatedCount = 0;

        // Capture named field targets by GameObject identity before any Text is destroyed - the
        // GameObject survives the Text -> TextMeshProUGUI swap, the Text component itself does not.
        Dictionary<string, GameObject> namedTargets = new Dictionary<string, GameObject>();
        foreach ((string gameObjectName, string fieldName) in namedFields)
        {
            GameObject found = null;
            foreach (Text text in ownedTexts)
            {
                if (text.gameObject.name == gameObjectName)
                {
                    found = text.gameObject;
                    break;
                }
            }

            if (found == null)
            {
                errors.Add(
                    "could not find an owned Text GameObject named '" + gameObjectName + "' for "
                        + target.GetType().Name + "." + fieldName + ".");
                return false;
            }

            namedTargets[fieldName] = found;
        }

        foreach (Text text in ownedTexts)
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

        if (checkSelectableTargetGraphics)
        {
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (IsPartOfNestedPrefabInstance(selectable.gameObject, root))
                {
                    continue;
                }

                if (selectable.targetGraphic == null)
                {
                    errors.Add(
                        BuildHierarchyPath(selectable.gameObject, root) + " : " + selectable.GetType().Name
                            + " has a null targetGraphic after migration.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        SerializedObject targetSerialized = new SerializedObject(target);
        foreach (KeyValuePair<string, GameObject> mapping in namedTargets)
        {
            TextMeshProUGUI tmp = mapping.Value.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                errors.Add(mapping.Value.name + " has no TextMeshProUGUI after conversion.");
                return false;
            }

            SerializedProperty property = targetSerialized.FindProperty(mapping.Key);
            if (property == null)
            {
                errors.Add(target.GetType().Name + " has no field named '" + mapping.Key + "'.");
                return false;
            }

            property.objectReferenceValue = tmp;
        }

        targetSerialized.ApplyModifiedProperties();
        return true;
    }

    /// <summary>
    /// See the Phase 1 original for why this uses TMP's Underlay feature (not <c>_OutlineWidth</c>, which
    /// is a uniform ring rather than a directional shadow) via an explicit <c>fontSharedMaterial</c> clone
    /// (not the <c>fontMaterial</c> instance-getter round trip, which silently dropped this modification).
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

    /// <summary>
    /// See <see cref="MenuTextMeshProMigration.PersistLooseUnderlayMaterials"/>'s original doc comment:
    /// <see cref="PrefabUtility.SaveAsPrefabAsset"/> silently drops a loose (never-persisted) material
    /// reference rather than embedding it, so every outline-compensated clone must become its own real
    /// project asset before the prefab is saved. Deterministic, overwrite-safe path.
    ///
    /// AUD-092 Phase 3: the path is qualified by <paramref name="root"/>'s own name (the prefab's root
    /// GameObject, e.g. "OptionManager"/"progressionScreen") as well as the converted GameObject's name.
    /// Two different screens can legitimately share a footer object name (Progression's "stats_menu"
    /// footer button collided with Options' pre-existing "Neon Pixel-7 SDF - stats_menu Underlay.mat"
    /// this way, found when migrating Progression) - without the screen qualifier, this method's
    /// find-or-delete-and-recreate at that path either overwrites the other screen's material out from
    /// under it, or - as observed here - the two screens end up silently sharing one material instance,
    /// so editing either screen's outline later would move both. Does not touch materials any earlier
    /// migration already created under the old unqualified name; those stay where they are since their
    /// TMP components' <c>fontSharedMaterial</c> already points at a persisted asset and this method
    /// only ever acts on a still-loose (never-persisted) material.
    ///
    /// AUD-092 Phase 4A: within a single call (i.e. within one screen), a loose material that is
    /// value-identical (shader, Underlay keyword, color, X/Y offset, softness) to one already persisted
    /// earlier in this same call reuses that persisted asset instead of creating another one - found
    /// migrating Credits, whose four footer buttons (press_start/stats_menu/options/quit_game) all clone
    /// the exact same outline compensation and would otherwise get four separate, permanently-identical
    /// material assets that could never batch together in the same Canvas. Comparing by value (not by
    /// GameObject name) means only genuinely different outline styles ever end up as separate assets.
    /// Deliberately scoped to one call: two DIFFERENT screens with matching values still each get their
    /// own asset (via the existing root-name qualifier above), because sharing across screens is exactly
    /// the accidental-coupling bug this method's screen qualifier was added to prevent - editing one
    /// screen's outline later must never move another screen's. Sharing within one screen is safe
    /// because the values only match here because the buttons are meant to look identical; editing one
    /// later to look different naturally gives it its own loose material again on the next migration run.
    /// </summary>
    internal static void PersistLooseUnderlayMaterials(GameObject root)
    {
        Dictionary<string, Material> persistedByValue = new Dictionary<string, Material>();
        foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Material material = tmp.fontSharedMaterial;
            if (material == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
            {
                continue;
            }

            string valueKey = BuildUnderlayMaterialValueKey(material);
            if (persistedByValue.TryGetValue(valueKey, out Material alreadyPersisted))
            {
                tmp.fontSharedMaterial = alreadyPersisted;
                continue;
            }

            string path = FontAssetFolder + "/Neon Pixel-7 SDF - " + root.name + " - " + tmp.gameObject.name + " Underlay.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(material, path);
            Material persisted = AssetDatabase.LoadAssetAtPath<Material>(path);
            tmp.fontSharedMaterial = persisted;
            persistedByValue[valueKey] = persisted;
        }
    }

    /// <summary>
    /// The subset of a TMP underlay-compensation material's state that determines its visual result -
    /// see <see cref="PersistLooseUnderlayMaterials"/>. Two loose materials with the same key are
    /// visually indistinguishable and safe to collapse onto one persisted asset.
    /// </summary>
    private static string BuildUnderlayMaterialValueKey(Material material)
    {
        return material.shader.name
            + "|" + material.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay)
            + "|" + material.GetColor(ShaderUtilities.ID_UnderlayColor)
            + "|" + material.GetFloat(ShaderUtilities.ID_UnderlayOffsetX)
            + "|" + material.GetFloat(ShaderUtilities.ID_UnderlayOffsetY)
            + "|" + material.GetFloat(ShaderUtilities.ID_UnderlaySoftness);
    }

    /// <summary>
    /// AUD-092 Phase 3: the single-prefab "convert every owned Text, abort-without-saving on any failure"
    /// shape shared verbatim by <see cref="MenuTextMeshProMigration.Migrate"/> (Options),
    /// <see cref="ProgressionTextMeshProMigration.Migrate"/>, and
    /// <see cref="ProgressionTextMeshProMigration.MigrateConfirmDialogue"/> - extracted here once those
    /// three call sites had converged on identical bodies differing only by prefab path and log prefix
    /// (see each caller's own doc comment for why it stays a separate <c>[MenuItem]</c> entry point rather
    /// than being merged into one). Returns true on success (including the "nothing to do" no-op path)
    /// and false on any abort; callers only need to expose their own <c>[MenuItem]</c> wrapper.
    ///
    /// AUD-092 Phase 4A: <paramref name="resolveProtectedTexts"/> lets a caller (see
    /// <c>CreditsTextMeshProMigration</c>) exempt a fixed set of directly-owned Text components from
    /// conversion - the legacy <c>UnityEngine.UI.InputField</c>'s <c>textComponent</c>/<c>placeholder</c>,
    /// which must remain legacy Text until Phase 4B migrates the InputField itself. Invoked against the
    /// freshly-loaded scratch <paramref name="root"/> (a Text's identity does not survive across separate
    /// <see cref="PrefabUtility.LoadPrefabContents"/> calls, so it must be re-resolved here rather than
    /// passed in from the caller's own inspection). Its second parameter is the SAME <c>errors</c> list
    /// this method later reports its per-Text failures through - a resolver that cannot find its expected
    /// shape should add a description of the problem to that list and return null, rather than logging
    /// independently, so a boundary-resolution failure is reported through the exact same single
    /// aggregated "aborted without saving - N error(s)" message as every other kind of failure below,
    /// instead of a separate ad hoc message. Every existing caller passes <c>null</c> (protect nothing)
    /// and is unaffected. Idempotency is judged by the count of remaining ELIGIBLE (non-protected) Text,
    /// not the total legacy Text count, so a prefab that intentionally keeps protected Text forever still
    /// reports "nothing to do" on a second run.
    /// </summary>
    internal static bool MigratePrefabTexts(
        string prefabPath, string logPrefix, Func<GameObject, List<string>, HashSet<Text>> resolveProtectedTexts = null)
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(
                logPrefix + ": TMP Essential Resources are not present. Run Level5/Import TMP Essential"
                    + " Resources first, then re-run this.");
            return false;
        }

        TMP_FontAsset font = EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError(logPrefix + ": could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            List<string> errors = new List<string>();
            HashSet<Text> protectedTexts = null;
            // AUD-092 Phase 4B: only ask the resolver to find something to protect when there is at
            // least one directly-owned Text left to consider protecting. Once Credits' InputField
            // itself is later migrated to TMP_InputField, zero legacy Text remain at all - re-running
            // this Phase 4A entry point at that point must still report "nothing to do", not fail
            // because ResolveProtectedInputFieldTexts can no longer find the (by then nonexistent)
            // legacy InputField it used to protect Text for.
            if (resolveProtectedTexts != null && texts.Count > 0)
            {
                protectedTexts = resolveProtectedTexts(root, errors);
                if (protectedTexts == null)
                {
                    if (errors.Count == 0)
                    {
                        errors.Add("could not resolve the protected Text component set.");
                    }

                    Debug.LogError(
                        logPrefix + " aborted without saving - " + errors.Count + " error(s):\n- " + string.Join("\n- ", errors));
                    return false;
                }
            }

            List<Text> eligibleTexts = protectedTexts == null
                ? texts
                : texts.FindAll(text => !protectedTexts.Contains(text));

            if (eligibleTexts.Count == 0 && root.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                Debug.Log(
                    logPrefix + ": no directly-owned eligible legacy Text remains in " + prefabPath + "; nothing to do ("
                        + nestedTexts.Count + " Text component(s) inside nested prefab instances, "
                        + (protectedTexts?.Count ?? 0) + " protected Text component(s), intentionally left untouched).");
                return true;
            }

            int convertedCount = 0;
            int outlineCompensatedCount = 0;

            foreach (Text text in eligibleTexts)
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
                    logPrefix + " aborted without saving - " + errors.Count + " error(s):\n- " + string.Join("\n- ", errors));
                return false;
            }

            PersistLooseUnderlayMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log(
                logPrefix + " complete: converted " + convertedCount + " Text component(s), compensated "
                    + outlineCompensatedCount + " legacy Outline effect(s) via TMP underlay material.");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    internal static FontStyles MapFontStyle(FontStyle style)
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

    internal static TextAlignmentOptions MapAlignment(TextAnchor anchor)
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

    /// <summary>
    /// True when <paramref name="go"/> belongs to a nested prefab instance rather than being directly
    /// owned by <paramref name="root"/> - see the Phase 1 original (<c>touch_joystick.prefab</c>, shared
    /// by every critical/menu prefab) for why this matters: converting a shared nested instance's Text
    /// from inside one screen would create a per-instance override and desync every other screen sharing
    /// it.
    /// </summary>
    internal static bool IsPartOfNestedPrefabInstance(GameObject go, GameObject root)
    {
        GameObject nearestInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        return nearestInstanceRoot != null && nearestInstanceRoot != root;
    }

    internal static void PartitionByNestedPrefabInstance(Text[] all, GameObject root, List<Text> owned, List<Text> nested)
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

    internal static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, maxLength) + "...";
    }

    /// <summary>Walks from <paramref name="target"/> up to <paramref name="root"/>, root-to-leaf.</summary>
    internal static string BuildHierarchyPath(GameObject target, GameObject root)
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
    /// AUD-092 Phase 3: single-pass index of every Transform under <paramref name="root"/> (active or
    /// not) by <see cref="GameObject.name"/>, for callers that need to resolve more than one known child
    /// name (e.g. <see cref="ProgressionTextMeshProMigration.CollectConfirmationDialogueContractErrors"/>
    /// resolving <c>confirm_button</c>/<c>cancel_button</c>) without re-walking the hierarchy once per
    /// name. Last-wins on a duplicate name, matching <see cref="GameObject.Find"/>'s own ambiguity.
    /// </summary>
    internal static Dictionary<string, Transform> IndexChildrenByName(GameObject root)
    {
        Dictionary<string, Transform> index = new Dictionary<string, Transform>();
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            index[candidate.name] = candidate;
        }

        return index;
    }

    /// <summary>
    /// AUD-092 Phase 3: forces <paramref name="behaviour"/>'s own private <c>Awake()</c> to run
    /// immediately via reflection. Verified empirically (a throwaway probe test, not committed) that
    /// <see cref="Object.Instantiate(Object)"/> does NOT invoke <c>Awake()</c> for a freshly instantiated
    /// MonoBehaviour in this project's batchmode EditMode test harness - not synchronously, and not even
    /// after yielding a full frame via <c>[UnityTest]</c>. This is not a transient timing race a
    /// <c>yield return null</c> fixes; the engine genuinely never dispatches it here. Any Editor/test code
    /// that instantiates a prefab and depends on Awake-driven initialization (button discovery, listener
    /// wiring, etc. - see <c>ConfirmDialogueTextMeshProMigrationTests</c> for the pattern this was
    /// written against) must call this rather than assume the engine will do it. Safe to call even if the
    /// engine does eventually invoke the real Awake elsewhere too, provided the target's Awake is itself
    /// idempotent (early-outs on already-assigned fields, overwrites rather than accumulates state).
    /// </summary>
    internal static void InvokeAwake(MonoBehaviour behaviour)
    {
        MethodInfo awake = behaviour.GetType().GetMethod(
            "Awake", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (awake == null)
        {
            Debug.LogError("MenuTextConversion.InvokeAwake: " + behaviour.GetType().Name + " has no private instance Awake() method to invoke.");
            return;
        }

        awake.Invoke(behaviour, null);
    }

    /// <summary>
    /// Walks every Transform in <paramref name="scene"/> (active or not) looking for the outermost
    /// prefab instance root whose nearest source prefab is <paramref name="prefabAssetPath"/>.
    /// </summary>
    internal static GameObject FindPrefabInstanceRoot(Scene scene, string prefabAssetPath)
    {
        return FindPrefabInstanceRoot(scene.GetRootGameObjects(), prefabAssetPath);
    }

    /// <summary>
    /// AUD-092 Phase 3: same search as the <see cref="Scene"/> overload above, but for a nested prefab
    /// instance living inside another prefab asset (e.g. <c>confirm_update.prefab</c> nested inside
    /// <c>progression_manager.prefab</c>) rather than inside a scene. <paramref name="outerRoot"/> is
    /// expected to come from <see cref="PrefabUtility.LoadPrefabContents"/>, matching every other mutator
    /// in this file.
    /// </summary>
    internal static GameObject FindPrefabInstanceRoot(GameObject outerRoot, string prefabAssetPath)
    {
        return FindPrefabInstanceRoot(new[] { outerRoot }, prefabAssetPath);
    }

    private static GameObject FindPrefabInstanceRoot(IEnumerable<GameObject> roots, string prefabAssetPath)
    {
        foreach (GameObject root in roots)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
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

    /// <summary>
    /// Scans every component under <paramref name="root"/> other than <see cref="Text"/> and
    /// <see cref="Selectable"/> for a serialized object-reference property pointing at one of
    /// <paramref name="textSet"/> - an unsupported consumer the migration would need to handle explicitly
    /// before that Text can be safely destroyed.
    /// </summary>
    internal static void CollectUnsupportedConsumers(GameObject root, HashSet<Object> textSet, List<string> findings)
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

    /// <summary>
    /// Resolves every <c>m_Text</c> property modification in <paramref name="scenePath"/>'s instance of
    /// <paramref name="prefabAssetPath"/> into the prefab's own <see cref="Text.text"/> default and drops
    /// the now-redundant override - see <see cref="MenuTextMeshProMigration.ResolveSceneTextOverrides"/>'s
    /// original doc comment for why this must run before any Text component is destroyed. Returns the
    /// number of overrides resolved, or -1 if the scene/prefab instance could not be found.
    /// </summary>
    internal static int ResolveSceneTextOverrides(string scenePath, string prefabAssetPath)
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogError("MenuTextConversion.ResolveSceneTextOverrides: scene file is missing: " + scenePath);
            return -1;
        }

        Scene existing = SceneManager.GetSceneByPath(scenePath);
        bool alreadyOpen = existing.IsValid() && existing.isLoaded;
        Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            GameObject instanceRoot = FindPrefabInstanceRoot(scene, prefabAssetPath);
            if (instanceRoot == null)
            {
                Debug.LogError(
                    "MenuTextConversion.ResolveSceneTextOverrides: no prefab instance of " + prefabAssetPath
                        + " found in " + scenePath);
                return -1;
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
                return 0;
            }

            if (prefabDirty)
            {
                AssetDatabase.SaveAssets();
            }

            PrefabUtility.SetPropertyModifications(instanceRoot, kept.ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return resolvedCount;
        }
        finally
        {
            if (!alreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    /// <summary>
    /// Permanent regression guard shared by every screen's contract: no leftover <c>m_Text*</c> property
    /// modification in <paramref name="scenePath"/> still targets a legacy Text component.
    /// </summary>
    internal static void CollectDanglingSceneTextOverrides(string scenePath, string prefabAssetPath, List<string> errors)
    {
        if (!File.Exists(scenePath))
        {
            return;
        }

        Scene existing = SceneManager.GetSceneByPath(scenePath);
        bool alreadyOpen = existing.IsValid() && existing.isLoaded;
        Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            GameObject instanceRoot = FindPrefabInstanceRoot(scene, prefabAssetPath);
            if (instanceRoot == null)
            {
                return;
            }

            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
                ?? Array.Empty<PropertyModification>();
            foreach (PropertyModification modification in modifications)
            {
                if (modification.propertyPath.StartsWith("m_Text", StringComparison.Ordinal)
                    && modification.target is Text)
                {
                    errors.Add(
                        scenePath + " : leftover legacy Text scene override on property '"
                            + modification.propertyPath + "'.");
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

    /// <summary>
    /// AUD-092 Phase 3: permanent regression guard for a shared prefab (e.g. <c>confirm_update.prefab</c>)
    /// nested as a nested prefab instance inside another prefab asset rather than a scene - mirrors
    /// <see cref="CollectDanglingSceneTextOverrides"/> but reads the nested instance's property
    /// modifications via <see cref="PrefabUtility.LoadPrefabContents"/> instead of opening a scene.
    /// </summary>
    internal static void CollectDanglingPrefabTextOverrides(string outerPrefabPath, string nestedPrefabAssetPath, List<string> errors)
    {
        if (!File.Exists(outerPrefabPath))
        {
            return;
        }

        GameObject outerRoot = PrefabUtility.LoadPrefabContents(outerPrefabPath);
        try
        {
            GameObject instanceRoot = FindPrefabInstanceRoot(outerRoot, nestedPrefabAssetPath);
            if (instanceRoot == null)
            {
                return;
            }

            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot)
                ?? Array.Empty<PropertyModification>();
            foreach (PropertyModification modification in modifications)
            {
                if (modification.target is Text
                    && (modification.propertyPath.StartsWith("m_Text", StringComparison.Ordinal)
                        || modification.propertyPath.StartsWith("m_FontData", StringComparison.Ordinal)))
                {
                    errors.Add(
                        outerPrefabPath + " : leftover legacy Text prefab override on property '"
                            + modification.propertyPath + "' targeting nested instance of " + nestedPrefabAssetPath + ".");
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(outerRoot);
        }
    }
}
