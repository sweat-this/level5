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
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed class Level5ProjectValidator : IPreprocessBuildWithReport
{
    private const string InputActionsPath = "Assets/Scripts/input/PlayerControls.inputactions";

    public int callbackOrder => -1000;

    [InitializeOnLoadMethod]
    private static void ConfigureSourceControlPolicy()
    {
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        if (!string.Equals(VersionControlSettings.mode, "Visible Meta Files", StringComparison.Ordinal))
        {
            VersionControlSettings.mode = "Visible Meta Files";
        }
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
        ValidateDevScenesExcludedFromBuild(errors);
        ValidateNoMissingScriptReferences(errors);
        ValidateSelectableLevels(errors);
        ValidateInputActions(errors);
        ValidateContestModeTimers(errors);
        ValidateDevCodeIsolation(errors);
        // AUD-088: enforced at build time now that the allowlist is empty. Every prefab that
        // could not be reserialized has been repaired or removed, so a binary asset appearing
        // here again is a regression rather than a known gap.
        errors.AddRange(CollectBinarySerializedAssetErrors());
        // AUD-092 Phase 1: the Options screen TMP migration is complete in the same change that adds
        // this check, so - like AUD-088 above - it is enforced immediately rather than deferred.
        errors.AddRange(CollectOptionsTextRenderingContractErrors());
        // AUD-092 Phase 2: same treatment for the Stats screen and its high score row prefab.
        errors.AddRange(CollectStatsTextRenderingContractErrors());
        // AUD-092 Phase 3: same treatment for the Progression screen.
        errors.AddRange(CollectProgressionTextRenderingContractErrors());
        // AUD-092 Phase 3: confirm_update.prefab is SHARED (progression_manager.prefab and
        // DialogueManager.prefab both depend on it), enforced under its own name for that reason.
        errors.AddRange(CollectConfirmationDialogueTextRenderingContractErrors());
        // AUD-092 Phase 4A: the Credits screen's ordinary display/button Text was migrated, while the
        // legacy ReportInputField and its two structural Text dependencies deliberately remain legacy
        // until Phase 4B migrates the InputField itself.
        errors.AddRange(CollectCreditsTextRenderingContractErrors());
        // AUD-092 Phase 5A migrated the account hub/create/login screens' ordinary Text; Phase 5B
        // migrated each screen's legacy InputFields (and their structural textComponent/placeholder
        // Text) to TMP_InputField/TextMeshProUGUI. Zero legacy Text/InputField is now the permanent
        // contract - see CollectAccountTextRenderingContractErrors.
        errors.AddRange(CollectAccountTextRenderingContractErrors());
        // AUD-092 Phase 6A: the Start menu's 27 runtime-mutated legacy Text fields were migrated to
        // TextMeshProUGUI via the new StartMenuTextUiObjects view; the other 14 of StartMenuUiObjects'
        // 41 legacy Text fields (static labels/unbound) deliberately remain legacy Text for Phase 6B.
        errors.AddRange(CollectStartTextRenderingContractErrors());
        // AUD-092 Phase 6C: confirm_tip.prefab - the shared tip dialogue nested directly in
        // level_00_start.unity - is enforced under its own name for the same reason confirm_update is:
        // it is a shared source prefab, not Start-owned. This is also the Start scene's final legacy
        // Text boundary; with it clean, CollectStartTextRenderingContractErrors above now requires zero
        // nested legacy Text as well as zero direct legacy Text.
        errors.AddRange(CollectTipDialogueTextRenderingContractErrors());

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

    private const string DevScriptFolder = "Assets/Scripts/Dev";
    private const string DevGuardSymbol = "#if UNITY_EDITOR || DEVELOPMENT_BUILD";

    /// <summary>
    /// AUD-012: `Assets/Scripts/Dev` holds dev tools, diagnostics, and dead experiments. Production
    /// code may reference them only from inside a
    /// <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> region, so nothing in Dev is reachable from a
    /// release build.
    ///
    /// This is a lint, not a proof - it checks that a referencing file carries the guard symbol at
    /// all, not that the specific reference sits inside it. That is enough to catch the regression
    /// it exists for: someone adding a new, unguarded call into Dev. Three such references existed
    /// when the folder was created (`DevFunctions`, `CharacterProgressParityLogger`, and a
    /// commented-out `AutoPlayerControllerTest` use).
    /// </summary>
    public static List<string> CollectDevIsolationErrors()
    {
        List<string> errors = new List<string>();
        if (!Directory.Exists(DevScriptFolder))
        {
            return errors;
        }

        HashSet<string> devTypeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (string devFile in Directory.GetFiles(DevScriptFolder, "*.cs", SearchOption.AllDirectories))
        {
            devTypeNames.Add(Path.GetFileNameWithoutExtension(devFile));
        }

        if (devTypeNames.Count == 0)
        {
            return errors;
        }

        foreach (string sourceFile in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string normalized = sourceFile.Replace('\\', '/');
            if (normalized.StartsWith(DevScriptFolder, StringComparison.Ordinal) || normalized.Contains("~/"))
            {
                continue;
            }

            string text = File.ReadAllText(sourceFile);
            if (text.Contains(DevGuardSymbol))
            {
                continue;
            }

            foreach (string devType in devTypeNames)
            {
                if (ContainsWord(text, devType))
                {
                    errors.Add(
                        normalized + " references Dev type '" + devType
                        + "' without a '" + DevGuardSymbol + "' guard.");
                }
            }
        }

        return errors;
    }

    private static bool ContainsWord(string text, string word)
    {
        int index = text.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            bool leftClear = index == 0 || !IsIdentifierChar(text[index - 1]);
            int after = index + word.Length;
            bool rightClear = after >= text.Length || !IsIdentifierChar(text[after]);
            if (leftClear && rightClear)
            {
                return true;
            }

            index = text.IndexOf(word, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsIdentifierChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static void ValidateDevCodeIsolation(List<string> errors)
    {
        errors.AddRange(CollectDevIsolationErrors());
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

    public static List<string> CollectEnabledDevSceneErrors()
    {
        List<string> errors = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || string.IsNullOrWhiteSpace(scene.path))
            {
                continue;
            }

            string normalized = scene.path.Replace('\\', '/');
            if (IsDevScenePath(normalized))
            {
                errors.Add("Dev scene must not be enabled in build settings: " + normalized + ".");
            }
        }

        return errors;
    }

    private static void ValidateDevScenesExcludedFromBuild(List<string> errors)
    {
        errors.AddRange(CollectEnabledDevSceneErrors());
    }

    private static bool IsDevScenePath(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        return path.IndexOf("/Dev/", StringComparison.OrdinalIgnoreCase) >= 0
            || fileName.StartsWith("dev_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_dev", StringComparison.OrdinalIgnoreCase)
            || fileName.IndexOf("_dev_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Folders whose assets are third party or imported and are not ours to reserialize.
    /// </summary>
    private static readonly string[] BinaryAssetScanExclusions =
    {
        "Assets/Plugins/",
        "Assets/Standard Assets/",
        "Assets/OmniSARTechnologies/",
        "Assets/TextMesh Pro/",
        "Assets/LevelPlay/",
        "Assets/MobileDependencyResolver/",
        "Assets/Joystick Pack/",
    };

    /// <summary>
    /// AUD-088: the project is on Force Text, and .gitattributes declares
    /// <c>*.prefab text eol=lf merge=unityyamlmerge</c>, but a set of assets - including every menu
    /// screen's UI except the start menu - were still Unity 2020/2021 binary because they were never
    /// reserialized after the switch. Nothing could review, diff or merge a change to them.
    ///
    /// Scenes and prefabs only. Baked data (LightingData, NavMesh, Terrain) is written binary by
    /// Unity whatever the serialization mode says, so it is not ours to enforce.
    ///
    /// This fails once they regress, so the reserialization does not have to be re-done later.
    /// Run <c>Level5/Reserialize Binary Assets</c> to fix any file this reports.
    /// </summary>
    public static List<string> CollectBinarySerializedAssetErrors()
    {
        List<string> errors = new List<string>();
        if (!Directory.Exists("Assets"))
        {
            return errors;
        }

        foreach (string file in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (extension != ".prefab" && extension != ".unity")
            {
                continue;
            }

            string normalized = file.Replace('\\', '/');
            if (IsExcludedFromBinaryAssetScan(normalized) || IsTextSerialized(file))
            {
                continue;
            }

            errors.Add(
                normalized
                    + " is binary-serialized while the project is Force Text. Run"
                    + " Level5/Reserialize Binary Assets.");
        }

        return errors;
    }

    private static bool IsExcludedFromBinaryAssetScan(string normalizedPath)
    {
        foreach (string excluded in BinaryAssetScanExclusions)
        {
            if (normalizedPath.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A text-serialized Unity asset always opens with the YAML directive. Binary assets start with
    /// their own header, so the first bytes are enough to tell them apart without parsing.
    /// </summary>
    private static bool IsTextSerialized(string path)
    {
        try
        {
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] header = new byte[5];
                int read = stream.Read(header, 0, header.Length);
                if (read < header.Length)
                {
                    return true;
                }

                // tolerate a UTF-8 BOM
                int offset = header[0] == 0xEF ? 3 : 0;
                if (offset > 0)
                {
                    stream.Position = 3;
                    read = stream.Read(header, 0, header.Length);
                    if (read < header.Length)
                    {
                        return true;
                    }
                }

                return header[0] == (byte)'%'
                    && header[1] == (byte)'Y'
                    && header[2] == (byte)'A'
                    && header[3] == (byte)'M'
                    && header[4] == (byte)'L';
            }
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Reserializes every asset <see cref="CollectBinarySerializedAssetErrors"/> reports, in place,
    /// preserving GUIDs. This is the editor half of AUD-088 - it cannot run from a headless tool.
    /// </summary>
    [MenuItem("Level5/Reserialize Binary Assets")]
    public static void ReserializeBinaryAssets()
    {
        List<string> errors = CollectBinarySerializedAssetErrors();
        if (errors.Count == 0)
        {
            Debug.Log("No binary-serialized assets found.");
            return;
        }

        List<string> paths = new List<string>(errors.Count);
        foreach (string error in errors)
        {
            int end = error.IndexOf(" is binary-serialized", StringComparison.Ordinal);
            if (end > 0)
            {
                paths.Add(error.Substring(0, end));
            }
        }

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
        AssetDatabase.SaveAssets();
        Debug.Log("Reserialized " + paths.Count + " binary assets to text:\n- " + string.Join("\n- ", paths.ToArray()));
    }

    /// <summary>The one scaling contract every menu canvas follows (AUD-091).</summary>
    public const float MenuCanvasReferenceWidth = 1920f;
    public const float MenuCanvasReferenceHeight = 1080f;
    public const float MenuCanvasMatchWidthOrHeight = 0.5f;

    /// <summary>
    /// AUD-091: the start menu carried three canvases with three different CanvasScaler settings -
    /// 800x400 at scale 0.9, 800x600, and 1920x1080 - all matching on width only. Three co-displayed
    /// layers scaling at three different rates hold together only at the aspect they were authored
    /// on. One contract, asserted here.
    ///
    /// Canvases inside assets that are still binary-serialized cannot be inspected, so they are
    /// reported by <see cref="CollectBinarySerializedAssetErrors"/> instead of silently passing.
    /// </summary>
    public static List<string> CollectMenuCanvasContractErrors()
    {
        List<string> errors = new List<string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled
                || string.IsNullOrWhiteSpace(buildScene.path)
                || !File.Exists(buildScene.path)
                || !IsMenuScenePath(buildScene.path))
            {
                continue;
            }

            Scene existing = SceneManager.GetSceneByPath(buildScene.path);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            Scene scene = alreadyOpen
                ? existing
                : EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (UnityEngine.UI.CanvasScaler scaler in
                        root.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true))
                    {
                        AddCanvasScalerErrors(errors, buildScene.path, scaler);
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

    private static void AddCanvasScalerErrors(
        List<string> errors,
        string scenePath,
        UnityEngine.UI.CanvasScaler scaler)
    {
        string where = scenePath + " -> " + scaler.gameObject.name;

        if (scaler.uiScaleMode != UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            errors.Add(where + " must use ScaleWithScreenSize, not " + scaler.uiScaleMode + ".");
            return;
        }

        if (!Mathf.Approximately(scaler.referenceResolution.x, MenuCanvasReferenceWidth)
            || !Mathf.Approximately(scaler.referenceResolution.y, MenuCanvasReferenceHeight))
        {
            errors.Add(
                where + " reference resolution is " + scaler.referenceResolution
                    + ", expected " + MenuCanvasReferenceWidth + "x" + MenuCanvasReferenceHeight + ".");
        }

        if (scaler.screenMatchMode != UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
        {
            errors.Add(where + " must use ScreenMatchMode.MatchWidthOrHeight.");
        }
        else if (!Mathf.Approximately(scaler.matchWidthOrHeight, MenuCanvasMatchWidthOrHeight))
        {
            errors.Add(
                where + " matchWidthOrHeight is " + scaler.matchWidthOrHeight
                    + ", expected " + MenuCanvasMatchWidthOrHeight + ".");
        }

        if (!Mathf.Approximately(scaler.scaleFactor, 1f))
        {
            errors.Add(where + " scaleFactor is " + scaler.scaleFactor + ", expected 1.");
        }
    }

    /// <summary>Internal so <see cref="MenuCameraRenderingContractMigration"/> shares this definition
    /// rather than re-deriving what counts as a menu scene.</summary>
    internal static bool IsMenuScenePath(string path)
    {
        return Path.GetFileNameWithoutExtension(path)
            .StartsWith("level_00_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The enabled/path/exists/menu-scene build-scene filter shared by
    /// <see cref="CollectMenuCameraContractErrors"/> and <see cref="MenuCameraRenderingContractMigration"/>,
    /// so the two cannot drift on what counts as an in-scope menu scene.
    /// </summary>
    internal static bool IsEnabledMenuBuildScene(EditorBuildSettingsScene buildScene)
    {
        return buildScene.enabled
            && !string.IsNullOrWhiteSpace(buildScene.path)
            && File.Exists(buildScene.path)
            && IsMenuScenePath(buildScene.path);
    }

    /// <summary>
    /// AUD-093 completion: menu Camera.allowHDR/allowMSAA still serialized true even though the
    /// UniversalAdditionalCameraData (UACD) on the same cameras already turns shadows,
    /// post-processing and HDR-display output off. Camera.allowHDR is what actually enables HDR
    /// rendering; UniversalAdditionalCameraData.allowHDROutput is a separate setting that only
    /// controls HDR-capable display output. The active Level5URP asset supports HDR, so a menu
    /// camera with allowHDR=true still renders a full-resolution HDR clear every frame behind an
    /// opaque Screen-Space-Overlay canvas that never needs it.
    ///
    /// Canvases inside assets that are still binary-serialized cannot be inspected, so as with
    /// <see cref="CollectMenuCanvasContractErrors"/> those are reported by
    /// <see cref="CollectBinarySerializedAssetErrors"/> instead of silently passing.
    /// </summary>
    public static List<string> CollectMenuCameraContractErrors()
    {
        List<string> errors = new List<string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!IsEnabledMenuBuildScene(buildScene))
            {
                continue;
            }

            Scene existing = SceneManager.GetSceneByPath(buildScene.path);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            Scene scene = alreadyOpen
                ? existing
                : EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        AddMenuCameraContractErrors(errors, buildScene.path, camera);
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

    [MenuItem("Level5/Validate Menu Camera Rendering Contract")]
    public static void ValidateMenuCameraContractFromMenu()
    {
        List<string> errors = CollectMenuCameraContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Menu camera rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Menu camera rendering contract validated.");
    }

    private static void AddMenuCameraContractErrors(List<string> errors, string scenePath, Camera camera)
    {
        string where = scenePath + " -> " + camera.gameObject.name;

        if (camera.allowHDR)
        {
            errors.Add(where + " has Camera.allowHDR enabled; menu cameras must set it false.");
        }

        if (camera.allowMSAA)
        {
            errors.Add(where + " has Camera.allowMSAA enabled; menu cameras must set it false.");
        }

        // Camera.GetUniversalAdditionalCameraData() silently AddComponent<>()s one when missing,
        // which a validator must never do as a side effect of reading state. TryGetComponent keeps
        // this check actually read-only.
        if (!camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
        {
            errors.Add(where + " has no UniversalAdditionalCameraData.");
            return;
        }

        if (cameraData.renderShadows)
        {
            errors.Add(
                where + " has UniversalAdditionalCameraData.renderShadows enabled; menu cameras must set it false.");
        }

        if (cameraData.renderPostProcessing)
        {
            errors.Add(
                where
                    + " has UniversalAdditionalCameraData.renderPostProcessing enabled; menu cameras must set it false.");
        }

        if (cameraData.allowHDROutput)
        {
            errors.Add(
                where + " has UniversalAdditionalCameraData.allowHDROutput enabled; menu cameras must set it false.");
        }
    }

    public static List<string> CollectMissingScriptReferenceErrors()
    {
        List<string> errors = new List<string>();
        AddMissingScriptReferenceErrors(errors, "Assets");
        AddMissingScriptReferenceErrors(errors, "ProjectSettings");
        return errors;
    }

    private static void ValidateNoMissingScriptReferences(List<string> errors)
    {
        errors.AddRange(CollectMissingScriptReferenceErrors());
    }

    private static void AddMissingScriptReferenceErrors(List<string> errors, string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (extension != ".asset" && extension != ".prefab" && extension != ".unity")
            {
                continue;
            }

            string normalized = file.Replace('\\', '/');
            string[] lines = File.ReadAllLines(file);

            // A YAML document is delimited by "--- !u!...". A missing user script always
            // serializes as "m_Script: {fileID: 0}" with no other identity for the component.
            // Unity's own built-in editor singletons (e.g. ProjectAuditorSettings) legitimately
            // serialize the same fileID: 0, but carry a populated m_EditorClassIdentifier instead
            // of a script GUID - that is not a missing reference, so a document is only flagged
            // when no such identifier is present alongside it.
            int documentStart = 0;
            for (int i = 0; i <= lines.Length; i++)
            {
                bool isBoundary = i == lines.Length || lines[i].StartsWith("--- ", StringComparison.Ordinal);
                if (!isBoundary)
                {
                    continue;
                }

                AddDocumentMissingScriptErrors(errors, normalized, lines, documentStart, i);
                documentStart = i;
            }
        }
    }

    private static void AddDocumentMissingScriptErrors(
        List<string> errors, string normalizedFile, string[] lines, int start, int end)
    {
        int missingScriptLine = -1;
        bool hasEditorClassIdentifier = false;

        for (int i = start; i < end; i++)
        {
            string line = lines[i];
            if (missingScriptLine < 0 && line.IndexOf("m_Script: {fileID: 0}", StringComparison.Ordinal) >= 0)
            {
                missingScriptLine = i + 1;
            }

            int identifierIndex = line.IndexOf("m_EditorClassIdentifier:", StringComparison.Ordinal);
            if (identifierIndex >= 0
                && !string.IsNullOrWhiteSpace(line.Substring(identifierIndex + "m_EditorClassIdentifier:".Length)))
            {
                hasEditorClassIdentifier = true;
            }
        }

        if (missingScriptLine >= 0 && !hasEditorClassIdentifier)
        {
            errors.Add(normalizedFile + ":" + missingScriptLine + " has a missing MonoBehaviour script reference.");
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

    /// <summary>
    /// A contest mode is timed by definition, so its prefab must set CustomTimer. Leaving it at 0
    /// used to hand Timer a zero-length clock (AUD-034); GameRules now falls back to the default
    /// match length instead, but a contest mode silently running at the default rather than its
    /// intended length is still a data bug worth failing the build over.
    /// </summary>
    private static void ValidateContestModeTimers(List<string> errors)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            StartScreenModeSelected mode = prefab != null ? prefab.GetComponent<StartScreenModeSelected>() : null;
            if (mode == null)
            {
                continue;
            }

            bool isContestMode = mode.GameModeThreePointContest
                || mode.GameModeFourPointContest
                || mode.GameModeSevenPointContest
                || mode.GameModeAllPointContest;

            if (isContestMode && mode.CustomTimer <= 0f)
            {
                errors.Add("Contest mode prefab " + path + " leaves CustomTimer at 0; it must set its match length.");
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
                // scenes with none of these managers have nothing to satisfy
                if (!SceneContainsComponent<GameRules>(scene))
                {
                    continue;
                }

                HashSet<string> objectNames = CollectObjectNames(scene);

                AddMissingObjectErrors(errors, buildScene.path, "GameRules", GameRules.RequiredSceneObjectNames, objectNames);
                AddMissingObjectErrors(
                    errors,
                    buildScene.path,
                    "MatchHudPresenter",
                    MatchHudPresenter.RequiredHudObjectNames,
                    objectNames);
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

    [MenuItem("Level5/Validate Menu UiObjects Contract")]
    public static void ValidateMenuUiObjectsFromMenu()
    {
        List<string> errors = CollectMenuUiObjectContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Menu UI reference validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Menu UI references validated.");
    }

    /// <summary>
    /// Every menu manager's serialized <c>*UiObjects</c>/<see cref="MenuFooterUiObjects"/> view must
    /// carry the references that manager's own <c>ValidateMenuUi</c> considers required. This
    /// replaced <c>GameObject.Find(name)</c> fallbacks with serialized references (AUD-103/AUD-104),
    /// so a rename no longer breaks anything - the fileID reference survives it - but a forgotten or
    /// mis-wired field still needs to fail the build the same way the old name-list contract did.
    ///
    /// Delegating to each manager's own <c>ValidateMenuUi</c> (rather than re-deriving the required
    /// field set here) means there is exactly one place that knows which references a given screen
    /// needs, and it is the same code path the manager runs at <c>Start</c>/<c>Awake</c>.
    /// </summary>
    public static List<string> CollectMenuUiObjectContractErrors()
    {
        List<string> errors = new List<string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path) || !File.Exists(buildScene.path))
            {
                continue;
            }

            Scene existing = SceneManager.GetSceneByPath(buildScene.path);
            bool alreadyOpen = existing.IsValid() && existing.isLoaded;
            Scene scene = alreadyOpen
                ? existing
                : EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);
            try
            {
                AddMenuUiContractErrors<OptionsManager>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
                AddMenuUiContractErrors<CreditsManager>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
                AddMenuUiContractErrors<StatsManager>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
                AddMenuUiContractErrors<ProgressionManager>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
                AddMenuUiContractErrors<AccountManager>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
                AddMenuUiContractErrors<StartManager>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
                AddMenuUiContractErrors<Pause>(errors, buildScene.path, scene, (m, missing) => m.ValidateMenuUi(missing));
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

    private static void AddMenuUiContractErrors<T>(
        List<string> errors,
        string scenePath,
        Scene scene,
        Func<T, List<string>, bool> validate)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T manager in root.GetComponentsInChildren<T>(true))
            {
                List<string> missing = new List<string>();
                if (!validate(manager, missing))
                {
                    errors.Add(
                        scenePath + " -> " + typeof(T).Name + " on '" + manager.gameObject.name
                            + "' is missing: " + string.Join(", ", missing.ToArray()));
                }
            }
        }
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

    [MenuItem("Level5/Validate Menu Layout Ownership Contract")]
    public static void ValidateMenuLayoutOwnershipFromMenu()
    {
        List<string> errors = CollectMenuLayoutOverrideContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Menu layout ownership validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Menu layout ownership validated.");
    }

    /// <summary>
    /// AUD-090: <c>OptionManager</c>, <c>StatsManager</c>, <c>progressionScreen</c> and
    /// <c>creditsManager</c> used to carry roughly a hundred combined prefab-instance overrides on
    /// child RectTransform anchor/position/size/pivot properties in their scenes - most numerically
    /// redundant with the prefab, a handful genuinely divergent. <c>MenuLayoutOwnershipMigration</c>
    /// removed the redundant ones and deliberately resolved every divergence (into the prefab where the
    /// scene held the real layout, or reverted where the prefab already did), so the prefab is now the
    /// sole owner of internal child layout on these four screens. Root placement/order and semantic
    /// state (active flags, text, wiring) remain legitimately scene-owned and are not checked here.
    ///
    /// Delegates entirely to <see cref="MenuLayoutOwnershipMigration.CollectForbiddenChildLayoutOverrides"/>
    /// so there is exactly one place that classifies a modification as child layout versus everything
    /// else this contract allows.
    /// </summary>
    public static List<string> CollectMenuLayoutOverrideContractErrors()
    {
        return MenuLayoutOwnershipMigration.CollectForbiddenChildLayoutOverrides();
    }

    [MenuItem("Level5/Validate Options Text Rendering Contract")]
    public static void ValidateOptionsTextRenderingContractFromMenu()
    {
        List<string> errors = CollectOptionsTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Options text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Options text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 1: OptionManager.prefab's 103 legacy Text components were migrated to
    /// TextMeshProUGUI on a project-owned Neon Pixel-7 SDF font asset. Delegates entirely to
    /// <see cref="MenuTextMeshProMigration.CollectContractErrors"/> so there is exactly one place
    /// classifying this screen's text-rendering contract, matching the
    /// <see cref="CollectMenuLayoutOverrideContractErrors"/> precedent.
    /// </summary>
    public static List<string> CollectOptionsTextRenderingContractErrors()
    {
        return MenuTextMeshProMigration.CollectContractErrors();
    }

    [MenuItem("Level5/Validate Stats Text Rendering Contract")]
    public static void ValidateStatsTextRenderingContractFromMenu()
    {
        List<string> errors = CollectStatsTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Stats text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Stats text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 2: StatsManager.prefab's directly-owned legacy Text components and
    /// highScoreRow.prefab's six columns were migrated to TextMeshProUGUI on the same project-owned
    /// Neon Pixel-7 SDF font asset Options used. Delegates entirely to
    /// <see cref="StatsTextMeshProMigration.CollectContractErrors"/>, matching the
    /// <see cref="CollectOptionsTextRenderingContractErrors"/> precedent.
    /// </summary>
    public static List<string> CollectStatsTextRenderingContractErrors()
    {
        return StatsTextMeshProMigration.CollectContractErrors();
    }

    [MenuItem("Level5/Validate Progression Text Rendering Contract")]
    public static void ValidateProgressionTextRenderingContractFromMenu()
    {
        List<string> errors = CollectProgressionTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Progression text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Progression text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 3: progressionScreen.prefab's directly-owned legacy Text components were migrated
    /// to TextMeshProUGUI on the same project-owned Neon Pixel-7 SDF font asset Options/Stats used.
    /// Delegates entirely to <see cref="ProgressionTextMeshProMigration.CollectContractErrors"/>,
    /// matching the <see cref="CollectOptionsTextRenderingContractErrors"/> precedent.
    /// </summary>
    public static List<string> CollectProgressionTextRenderingContractErrors()
    {
        return ProgressionTextMeshProMigration.CollectContractErrors();
    }

    [MenuItem("Level5/Validate Confirmation Dialogue Text Rendering Contract")]
    public static void ValidateConfirmationDialogueTextRenderingContractFromMenu()
    {
        List<string> errors = CollectConfirmationDialogueTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Confirmation dialogue text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Confirmation dialogue text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 3: confirm_update.prefab's two directly-owned legacy Text components
    /// (confirm_button, cancel_button) were migrated to TextMeshProUGUI on the same project-owned Neon
    /// Pixel-7 SDF font asset Options/Stats/Progression used. Named separately from
    /// <see cref="CollectProgressionTextRenderingContractErrors"/> - not folded into it - because
    /// confirm_update.prefab is SHARED: it is nested inside progression_manager.prefab AND held by
    /// DialogueManager.prefab as a runtime Instantiate() template for Start/Account flows, so this
    /// contract must hold regardless of Progression-specific state. Delegates entirely to
    /// <see cref="ProgressionTextMeshProMigration.CollectConfirmationDialogueContractErrors"/>.
    /// </summary>
    public static List<string> CollectConfirmationDialogueTextRenderingContractErrors()
    {
        return ProgressionTextMeshProMigration.CollectConfirmationDialogueContractErrors();
    }

    [MenuItem("Level5/Validate Credits Text Rendering Contract")]
    public static void ValidateCreditsTextRenderingContractFromMenu()
    {
        List<string> errors = CollectCreditsTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Credits text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Credits text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 4A: creditsManager.prefab's 21 ordinary display/button legacy Text components were
    /// migrated to TextMeshProUGUI on the same project-owned Neon Pixel-7 SDF font asset the other menu
    /// screens used, while the legacy <c>ReportInputField</c> InputField and its two structural Text
    /// dependencies deliberately remain legacy Text until Phase 4B. Delegates entirely to
    /// <see cref="CreditsTextMeshProMigration.CollectContractErrors"/>, matching the
    /// <see cref="CollectProgressionTextRenderingContractErrors"/> precedent.
    /// </summary>
    public static List<string> CollectCreditsTextRenderingContractErrors()
    {
        return CreditsTextMeshProMigration.CollectContractErrors();
    }

    [MenuItem("Level5/Validate Account Text Rendering Contract")]
    public static void ValidateAccountTextRenderingContractFromMenu()
    {
        List<string> errors = CollectAccountTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Account text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Account text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 5A migrated the account hub/create/login screens' ordinary directly scene-owned
    /// legacy Text to TextMeshProUGUI on the shared Neon Pixel-7 SDF font asset. AUD-092 Phase 5B
    /// migrated each screen's legacy InputField components themselves (and their structural
    /// textComponent/placeholder Text dependencies) to TMP_InputField/TextMeshProUGUI, including the
    /// password field's ContentType.Password fix. <c>level_00_account_loginLocal</c> has no legacy
    /// Text/InputField and is asserted unchanged. Delegates entirely to
    /// <see cref="AccountTextMeshProMigration"/>, matching the
    /// <see cref="CollectCreditsTextRenderingContractErrors"/> precedent.
    /// </summary>
    public static List<string> CollectAccountTextRenderingContractErrors()
    {
        List<string> errors = new List<string>();
        errors.AddRange(AccountTextMeshProMigration.CollectHubContractErrors());
        errors.AddRange(AccountTextMeshProMigration.CollectCreateNewContractErrors());
        errors.AddRange(AccountTextMeshProMigration.CollectLoginExistingContractErrors());
        errors.AddRange(AccountTextMeshProMigration.CollectLoginLocalContractErrors());
        return errors;
    }

    [MenuItem("Level5/Validate Start Text Rendering Contract")]
    public static void ValidateStartTextRenderingContractFromMenu()
    {
        List<string> errors = CollectStartTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Start text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Start text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 6A: the Start menu's (<c>level_00_start</c>) 27 runtime-mutated legacy Text fields
    /// - the subset of StartMenuUiObjects' 41 legacy Text fields StartManager/PlayerSelectView/
    /// CpuSlotBinding actually write <c>.text</c> into - were migrated to TextMeshProUGUI on the shared
    /// Neon Pixel-7 SDF font asset via the new StartMenuTextUiObjects view. The other 14 fields (static
    /// labels/unbound) deliberately remain legacy Text for Phase 6B. Delegates entirely to
    /// <see cref="StartMenuTextMeshProMigration.CollectContractErrors"/>, matching the
    /// <see cref="CollectAccountTextRenderingContractErrors"/> precedent.
    /// </summary>
    public static List<string> CollectStartTextRenderingContractErrors()
    {
        return StartMenuTextMeshProMigration.CollectContractErrors();
    }

    [MenuItem("Level5/Validate Tip Dialogue Text Rendering Contract")]
    public static void ValidateTipDialogueTextRenderingContractFromMenu()
    {
        List<string> errors = CollectTipDialogueTextRenderingContractErrors();
        if (errors.Count > 0)
        {
            Debug.LogError("Tip dialogue text rendering contract validation failed:\n- " + string.Join("\n- ", errors.ToArray()));
            return;
        }

        Debug.Log("Tip dialogue text rendering contract validated.");
    }

    /// <summary>
    /// AUD-092 Phase 6C: confirm_tip.prefab's four directly-owned legacy Text components (header, tip
    /// body, next-button label, close-button label) were migrated to TextMeshProUGUI on the shared Neon
    /// Pixel-7 SDF font asset, and its owning StartScreenTipDialogueManager now resolves UI only through
    /// the new TipDialogueUiObjects typed view - no legacy Text/Button fields, no GameObject.Find. Named
    /// separately from <see cref="CollectStartTextRenderingContractErrors"/> - not folded into it -
    /// because confirm_tip.prefab is a SHARED source prefab nested into level_00_start.unity, matching
    /// the <see cref="CollectConfirmationDialogueTextRenderingContractErrors"/> precedent for
    /// confirm_update.prefab. Delegates entirely to
    /// <see cref="TipDialogueTextMeshProMigration.CollectContractErrors"/>.
    ///
    /// DialogueManager.confirmationDialogTip (the field that used to make this prefab look like it also
    /// backed the account-flow confirmation dialog) was found to be a dangling reference - its
    /// serialized fileID does not correspond to any object in this prefab - and DialogueManager itself
    /// only ever exists in level_00_account_loginLocal.unity, never level_00_start.unity, so its tip
    /// branch could never actually run. That field, the TipDialogue constant, and the tip-specific
    /// GameObject.Find/Instantiate branch were removed from DialogueManager entirely; its ordinary
    /// ConfirmDialogue path is unchanged.
    /// </summary>
    public static List<string> CollectTipDialogueTextRenderingContractErrors()
    {
        return TipDialogueTextMeshProMigration.CollectContractErrors();
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
