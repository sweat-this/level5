using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Level5.Core.Match;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools for the authored-data half of the match overhaul.
///
/// Two jobs, both of which have to happen inside Unity because the authored data lives in a binary
/// prefab:
///
/// - write <see cref="GameModeDefinition"/> / <see cref="LevelDefinition"/> assets from the legacy
///   start-menu components, so the authored values are migrated rather than retyped;
/// - export the mode characterization matrix, so what each mode actually is can be reviewed in a
///   diff instead of by opening prefabs one at a time.
///
/// The migration is safe to re-run: it updates existing assets in place, keeping their GUIDs, so
/// anything already referencing one keeps its reference.
/// </summary>
public static class MatchDefinitionMigration
{
    // The same two folders LoadManager reads at runtime.
    private const string ModePrefabResourcesPath = "Prefabs/menu_start/mode_selected_objects";
    private const string LevelPrefabResourcesPath = "Prefabs/menu_start/level_selected_objects";
    private const string ModeAssetFolder = "Assets/Resources/Match/Modes";
    private const string LevelAssetFolder = "Assets/Resources/Match/Levels";
    private const string CharacterizationPath = "docs/generated/level5-game-mode-characterization.md";

    [MenuItem("Level 5/Match/Migrate Authored Mode and Level Definitions")]
    public static void MigrateDefinitions()
    {
        if (!TryLoadAuthoredSources(out List<StartScreenModeSelected> modes, out List<LevelSelected> levels, out string error))
        {
            EditorUtility.DisplayDialog("Match definition migration", error, "OK");
            return;
        }

        EnsureFolder(ModeAssetFolder);
        EnsureFolder(LevelAssetFolder);

        List<string> anomalies = new List<string>();
        int modesWritten = 0;
        foreach (StartScreenModeSelected source in modes)
        {
            GameModeDefinitionFactory.Conversion conversion = GameModeDefinitionFactory.Convert(source);
            anomalies.AddRange(conversion.Anomalies);
            if (conversion.Definition == null)
            {
                continue;
            }

            WriteOrUpdate(
                ModeAssetFolder,
                AssetName("mode", conversion.Definition.RawModeId, conversion.Definition.ObjectName),
                conversion.Definition,
                (existing, fresh) => existing.Apply(ToData(fresh)));
            modesWritten++;
        }

        int levelsWritten = 0;
        foreach (LevelSelected source in levels)
        {
            LevelDefinition definition = LevelDefinitionFactory.Create(source);
            if (definition == null)
            {
                continue;
            }

            WriteOrUpdate(
                LevelAssetFolder,
                AssetName("level", definition.LevelId, definition.ObjectName),
                definition,
                (existing, fresh) => existing.Apply(ToData(fresh)));
            levelsWritten++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        foreach (string anomaly in anomalies)
        {
            Debug.LogWarning("Match definition migration: " + anomaly);
        }

        string summary = $"Wrote {modesWritten} mode and {levelsWritten} level definitions."
            + (anomalies.Count > 0
                ? $"\n\n{anomalies.Count} authored value(s) could not be represented exactly. See the console."
                : "\n\nNo anomalies.");
        EditorUtility.DisplayDialog("Match definition migration", summary, "OK");
    }

    /// <summary>
    /// Writes the mode characterization matrix: for every authored mode, what it resolves to under
    /// the new model, plus the legacy booleans it came from. This is the artefact the plan asks for
    /// before any legacy field is deleted - it is what a later change is checked against.
    /// </summary>
    [MenuItem("Level 5/Match/Export Mode Characterization Matrix")]
    public static void ExportCharacterizationMatrix()
    {
        EditorUtility.DisplayDialog("Mode characterization", WriteCharacterizationMatrix(), "OK");
    }

    /// <summary>
    /// Batch-mode entry point for the export, so the matrix can be refreshed by CI or a script:
    ///
    /// <code>
    /// Unity.exe -batchmode -quit -projectPath &lt;path&gt;     ///   -executeMethod MatchDefinitionMigration.ExportCharacterizationFromCommandLine
    /// </code>
    /// </summary>
    public static void ExportCharacterizationFromCommandLine()
    {
        Debug.Log(WriteCharacterizationMatrix());
    }

    private static string WriteCharacterizationMatrix()
    {
        if (!TryLoadAuthoredSources(out List<StartScreenModeSelected> modes, out List<LevelSelected> levels, out string error))
        {
            return error;
        }

        StringBuilder markdown = new StringBuilder();
        markdown.AppendLine("# Level 5 game mode characterization");
        markdown.AppendLine();
        markdown.AppendLine("Generated by `Level 5 > Match > Export Mode Characterization Matrix`.");
        markdown.AppendLine("Do not edit by hand - re-export it.");
        markdown.AppendLine();
        markdown.AppendLine("Each row is what one authored mode resolves to under the match configuration model.");
        markdown.AppendLine("A change to any of these values is a gameplay change, not a refactor.");
        markdown.AppendLine();
        markdown.AppendLine("| id | mode | objective | clock | timer | combat | shot rule | markers | ball | money | streak | survive | cpu shooters | enemies only | arcade | roster |");
        markdown.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

        List<string> anomalies = new List<string>();
        foreach (StartScreenModeSelected source in modes)
        {
            GameModeDefinitionFactory.Conversion conversion = GameModeDefinitionFactory.Convert(source);
            anomalies.AddRange(conversion.Anomalies);
            GameModeDefinition mode = conversion.Definition;
            if (mode == null)
            {
                continue;
            }

            markdown.AppendLine(string.Join(" | ", new[]
            {
                "| " + mode.RawModeId,
                mode.DisplayName,
                mode.Objective.ToString(),
                mode.ClockMode.ToString(),
                mode.CustomTimerSeconds > 0f ? mode.CustomTimerSeconds.ToString("0.##") : "default",
                mode.CombatMode.ToString(),
                mode.ShotRule.ToString(),
                mode.ShotMarkers.ToString(),
                Yes(mode.RequiresBasketball),
                Yes(mode.RequiresMoneyBall),
                Yes(mode.RequiresConsecutiveShots),
                Yes(mode.RequiresPlayerSurvive),
                Yes(mode.AllowsCpuShooters),
                Yes(mode.EnemiesOnly),
                Yes(mode.ArcadeMode),
                $"{mode.MinPlayers}-{mode.MaxPlayers}" + (mode.AddsImplicitDefender ? " +defender" : string.Empty) + " |"
            }));
        }

        markdown.AppendLine();
        markdown.AppendLine("## Arena capabilities");
        markdown.AppendLine();
        markdown.AppendLine("| id | level | scene | capabilities |");
        markdown.AppendLine("| --- | --- | --- | --- |");
        foreach (LevelSelected source in levels)
        {
            LevelDefinition level = LevelDefinitionFactory.Create(source);
            if (level == null)
            {
                continue;
            }

            markdown.AppendLine($"| {level.LevelId} | {level.DisplayName} | {level.SceneName} | {level.Capabilities} |");
        }

        markdown.AppendLine();
        markdown.AppendLine("## Anomalies");
        markdown.AppendLine();
        if (anomalies.Count == 0)
        {
            markdown.AppendLine("None. Every authored mode maps onto the rule dimensions exactly.");
        }
        else
        {
            markdown.AppendLine("Authored states the rule dimensions cannot represent exactly. These are recorded, not fixed:");
            markdown.AppendLine();
            foreach (string anomaly in anomalies)
            {
                markdown.AppendLine("- " + anomaly);
            }
        }

        string path = Path.Combine(Directory.GetCurrentDirectory(), CharacterizationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, markdown.ToString());
        AssetDatabase.Refresh();

        return $"Wrote {CharacterizationPath}: {modes.Count} modes, {levels.Count} levels, {anomalies.Count} anomalies.";
    }

    /// <summary>
    /// Loads the authored mode and level components the same way the game does at runtime:
    /// <c>Resources.LoadAll</c> over the two selection-prefab folders that
    /// <see cref="LoadManager"/> reads.
    ///
    /// Loading them the same way matters for more than convenience - the menu indices are positions
    /// in this list, so a set gathered any other way could be in a different order and the catalog
    /// would not line up with what the menu is showing.
    ///
    /// Levels are filtered by <c>IsSelectable</c>, again matching <see cref="LoadManager"/>: an
    /// unselectable level is not in the menu and should not be in the catalog.
    ///
    /// Public so the parity tests resolve the authored data exactly the way the migration does.
    /// </summary>
    public static bool TryLoadAuthoredSources(
        out List<StartScreenModeSelected> modes,
        out List<LevelSelected> levels,
        out string error)
    {
        modes = new List<StartScreenModeSelected>();
        levels = new List<LevelSelected>();
        error = null;

        foreach (GameObject prefab in Resources.LoadAll<GameObject>(ModePrefabResourcesPath))
        {
            if (prefab != null && prefab.TryGetComponent(out StartScreenModeSelected mode))
            {
                modes.Add(mode);
            }
        }

        foreach (GameObject prefab in Resources.LoadAll<GameObject>(LevelPrefabResourcesPath))
        {
            if (prefab != null && prefab.TryGetComponent(out LevelSelected level) && level.IsSelectable)
            {
                levels.Add(level);
            }
        }

        if (modes.Count == 0)
        {
            error = $"No StartScreenModeSelected prefabs under Resources/{ModePrefabResourcesPath}.";
            return false;
        }

        if (levels.Count == 0)
        {
            error = $"No selectable LevelSelected prefabs under Resources/{LevelPrefabResourcesPath}.";
            return false;
        }

        return true;
    }

    private static void WriteOrUpdate<T>(string folder, string assetName, T fresh, Action<T, T> apply)
        where T : ScriptableObject
    {
        string path = $"{folder}/{assetName}.asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            // Update in place so the asset keeps its GUID and every reference to it survives.
            apply(existing, fresh);
            EditorUtility.SetDirty(existing);
            return;
        }

        AssetDatabase.CreateAsset(fresh, path);
    }

    private static string AssetName(string prefix, int id, string objectName)
    {
        string suffix = string.IsNullOrEmpty(objectName) ? string.Empty : "_" + Sanitize(objectName);
        return $"{prefix}_{id:00}{suffix}";
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
        }

        return builder.ToString();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private static GameModeDefinitionData ToData(GameModeDefinition definition)
    {
        return new GameModeDefinitionData
        {
            ModeId = definition.RawModeId,
            DisplayName = definition.DisplayName,
            ObjectName = definition.ObjectName,
            Description = definition.Description,
            HighScoreField = definition.HighScoreField,
            Objective = definition.Objective,
            ClockMode = definition.ClockMode,
            CustomTimerSeconds = definition.CustomTimerSeconds,
            CombatMode = definition.CombatMode,
            ShotRule = definition.ShotRule,
            ShotMarkers = definition.ShotMarkers,
            RequiresBasketball = definition.RequiresBasketball,
            RequiresMoneyBall = definition.RequiresMoneyBall,
            RequiresConsecutiveShots = definition.RequiresConsecutiveShots,
            RequiresPlayerSurvive = definition.RequiresPlayerSurvive,
            AllowsCpuShooters = definition.AllowsCpuShooters,
            EnemiesOnly = definition.EnemiesOnly,
            ArcadeMode = definition.ArcadeMode,
            MinPlayers = definition.MinPlayers,
            MaxPlayers = definition.MaxPlayers,
            RequiresCpuOpponent = definition.RequiresCpuOpponent,
            AddsImplicitDefender = definition.AddsImplicitDefender,
            RequiredArenaCapabilities = definition.RequiredArenaCapabilities,
            ForbiddenArenaCapabilities = definition.ForbiddenArenaCapabilities
        };
    }

    private static LevelDefinitionData ToData(LevelDefinition definition)
    {
        return new LevelDefinitionData
        {
            LevelId = definition.LevelId,
            DisplayName = definition.DisplayName,
            Info = definition.Info,
            ObjectName = definition.ObjectName,
            SceneDescriptor = definition.SceneDescriptor,
            Capabilities = definition.Capabilities,
            CustomCamera = definition.CustomCamera,
            Selectable = definition.Selectable,
            Locked = definition.Locked
        };
    }

    private static string Yes(bool value)
    {
        return value ? "yes" : "no";
    }
}
