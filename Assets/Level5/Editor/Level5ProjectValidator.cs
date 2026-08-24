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

    private static bool IsMenuScenePath(string path)
    {
        return Path.GetFileNameWithoutExtension(path)
            .StartsWith("level_00_", StringComparison.OrdinalIgnoreCase);
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
                if (!SceneContainsComponent<GameRules>(scene)
                    && !SceneContainsComponent<ProgressionManager>(scene))
                {
                    continue;
                }

                HashSet<string> objectNames = CollectObjectNames(scene);

                if (SceneContainsComponent<GameRules>(scene))
                {
                    AddMissingObjectErrors(errors, buildScene.path, "GameRules", GameRules.RequiredSceneObjectNames, objectNames);
                    AddMissingObjectErrors(
                        errors,
                        buildScene.path,
                        "MatchHudPresenter",
                        MatchHudPresenter.RequiredHudObjectNames,
                        objectNames);
                }

                // AUD-047: the progression menu still resolves its Text/Image references this way -
                // see docs/ui-input-architecture.md. The button names this array used to also carry
                // (playerSelectButtonName, progression3/4/7AccuracyName, ...) moved to
                // ProgressionUiObjects's serialized references, asserted by
                // CollectMenuUiObjectContractErrors instead (AUD-103).
                if (SceneContainsComponent<ProgressionManager>(scene))
                {
                    AddMissingObjectErrors(
                        errors,
                        buildScene.path,
                        "ProgressionManager",
                        ProgressionManager.RequiredProgressionObjectNames,
                        objectNames);
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
