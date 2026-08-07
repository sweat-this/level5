using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Exports the Unity-serialized authored data that Codex cannot reliably recover from binary
/// prefab/scene files through repository text search alone.
///
/// The exporter is intentionally read-only with respect to game assets. It writes one deterministic
/// JSON document under docs/generated and restores the editor's original scene setup after scanning.
/// </summary>
public static class Level5DocumentationExporter
{
    public const int SchemaVersion = 1;
    public const string OutputRelativePath = "docs/generated/level5-authored-game-data.json";

    private static readonly string CharacterSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/player_selected_objects";
    private static readonly string DefaultCharacterRoot =
        "Assets/Resources/Prefabs/menu_start/default_shooter_profiles";
    private static readonly string LevelSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/level_selected_objects";
    private static readonly string ModeSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/mode_selected_objects";
    private static readonly string SupportSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/cheerleader_selected_object";
    private static readonly string EnemyRoot = "Assets/Resources/Prefabs/enemies";
    private static readonly string BodyGuardRoot = "Assets/Resources/Prefabs/bodyguards";
    private static readonly string NavMeshVehicleRoot = "Assets/Resources/Prefabs/vehicles-navmesh";
    private static readonly string NonNavMeshVehicleRoot = "Assets/Resources/Prefabs/vehicles-no-navmesh";
    private static readonly string ResourcesPrefabRoot = "Assets/Resources/Prefabs";
    private static readonly string SceneRoot = "Assets/Scenes";

    [MenuItem("Level 5/Documentation/Export Authored Game Data")]
    public static void ExportFromMenu()
    {
        try
        {
            string path = Export();
            Debug.Log("Level 5 documentation data exported to: " + path);
            EditorUtility.DisplayDialog(
                "Level 5 Documentation Export",
                "Export complete:\n" + path,
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Level 5 Documentation Export Failed",
                exception.Message,
                "OK");
        }
    }

    /// <summary>
    /// Entry point suitable for Unity -executeMethod automation.
    /// </summary>
    public static void ExportFromCommandLine()
    {
        try
        {
            string path = Export();
            Debug.Log("Level 5 documentation data exported to: " + path);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }

            throw;
        }
    }

    /// <summary>
    /// Builds and writes the authored-data export. Returns the absolute output path.
    /// </summary>
    public static string Export()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Level 5 documentation export cannot run while entering or running Play Mode.");
        }

        EnsureNoDirtyOpenScenes();

        DocumentationExport export = new DocumentationExport
        {
            schemaVersion = SchemaVersion,
            unityVersion = Application.unityVersion,
            outputContract = OutputRelativePath,
            characterSelections = ExportPrefabs(
                CharacterSelectionRoot,
                typeof(CharacterProfile)),
            defaultCharacterProfiles = ExportPrefabs(
                DefaultCharacterRoot,
                typeof(CharacterProfile)),
            levelSelections = ExportPrefabs(
                LevelSelectionRoot,
                typeof(LevelSelected)),
            modeSelections = ExportPrefabs(
                ModeSelectionRoot,
                typeof(StartScreenModeSelected)),
            supportSelections = ExportPrefabs(
                SupportSelectionRoot,
                typeof(CheerleaderProfile)),
            enemies = ExportPrefabs(
                EnemyRoot,
                typeof(EnemyController),
                typeof(EnemyHealth)),
            bodyGuards = ExportPrefabs(
                BodyGuardRoot,
                typeof(BodyGuardController),
                typeof(BodyGuardHealth)),
            navMeshVehicles = ExportPrefabs(
                NavMeshVehicleRoot,
                typeof(VehicleController)),
            nonNavMeshVehicles = ExportPrefabs(
                NonNavMeshVehicleRoot),
            racingProfiles = ExportRacingProfilePrefabs(),
            scenes = ExportScenes()
        };

        export.findings.Sort(StringComparer.Ordinal);

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Could not resolve the Unity project root.");
        }

        string absolutePath = Path.Combine(
            projectRoot,
            OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(absolutePath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("Could not resolve the documentation export directory.");
        }

        Directory.CreateDirectory(directory);
        string json = JsonUtility.ToJson(export, true) + Environment.NewLine;
        File.WriteAllText(absolutePath, json);
        return absolutePath;
    }

    private static List<AssetRecord> ExportPrefabs(string root, params Type[] componentTypes)
    {
        List<AssetRecord> records = new List<AssetRecord>();
        foreach (string path in FindPrefabPaths(root))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                records.Add(new AssetRecord
                {
                    sourcePath = path,
                    assetName = Path.GetFileNameWithoutExtension(path),
                    finding = "Prefab could not be loaded by AssetDatabase."
                });
                continue;
            }

            AssetRecord record = new AssetRecord
            {
                sourcePath = path,
                assetName = prefab.name
            };

            if (componentTypes == null || componentTypes.Length == 0)
            {
                record.components = ExportAllMonoBehaviours(prefab);
            }
            else
            {
                foreach (Type componentType in componentTypes)
                {
                    Component component = prefab.GetComponentInChildren(componentType, true);
                    if (component == null)
                    {
                        record.findings.Add("Missing expected component: " + componentType.Name);
                        continue;
                    }

                    record.components.Add(ExportComponent(component));
                }
            }

            records.Add(record);
        }

        records.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.sourcePath, right.sourcePath));
        return records;
    }

    private static List<AssetRecord> ExportRacingProfilePrefabs()
    {
        List<AssetRecord> records = new List<AssetRecord>();
        foreach (string path in FindPrefabPaths(ResourcesPrefabRoot))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            RacingVehicleProfile profile = prefab.GetComponentInChildren<RacingVehicleProfile>(true);
            if (profile == null)
            {
                continue;
            }

            AssetRecord record = new AssetRecord
            {
                sourcePath = path,
                assetName = prefab.name
            };
            record.components.Add(ExportComponent(profile));

            RacingVehicleController controller = prefab.GetComponentInChildren<RacingVehicleController>(true);
            if (controller != null)
            {
                record.components.Add(ExportComponent(controller));
            }

            records.Add(record);
        }

        records.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.sourcePath, right.sourcePath));
        return records;
    }

    private static List<SceneRecord> ExportScenes()
    {
        List<SceneRecord> records = new List<SceneRecord>();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string scenePath in FindScenePaths())
            {
                SceneRecord record = new SceneRecord
                {
                    sourcePath = scenePath,
                    sceneName = Path.GetFileNameWithoutExtension(scenePath)
                };

                try
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    record.components.AddRange(ExportSceneComponents<EnemySpawner>());
                    record.components.AddRange(ExportSceneComponents<TrafficManager>());
                    record.components.AddRange(ExportSceneComponents<RacingVehicleProfile>());
                    record.components.Sort(CompareSceneComponents);

                    if (!scene.IsValid())
                    {
                        record.findings.Add("EditorSceneManager returned an invalid scene handle.");
                    }
                }
                catch (Exception exception)
                {
                    record.findings.Add(
                        "Scene inspection failed: "
                        + exception.GetType().Name
                        + ": "
                        + exception.Message);
                }

                record.findings.Sort(StringComparer.Ordinal);
                records.Add(record);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        records.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.sourcePath, right.sourcePath));
        return records;
    }

    private static List<SceneComponentRecord> ExportSceneComponents<T>() where T : Component
    {
        List<SceneComponentRecord> records = new List<SceneComponentRecord>();
        T[] components = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (T component in components)
        {
            if (component == null || !component.gameObject.scene.IsValid())
            {
                continue;
            }

            records.Add(new SceneComponentRecord
            {
                hierarchyPath = GetHierarchyPath(component.transform),
                component = ExportComponent(component)
            });
        }

        records.Sort(CompareSceneComponents);
        return records;
    }

    private static int CompareSceneComponents(
        SceneComponentRecord left,
        SceneComponentRecord right)
    {
        int hierarchy = StringComparer.Ordinal.Compare(left.hierarchyPath, right.hierarchyPath);
        if (hierarchy != 0)
        {
            return hierarchy;
        }

        return StringComparer.Ordinal.Compare(
            left.component != null ? left.component.componentType : string.Empty,
            right.component != null ? right.component.componentType : string.Empty);
    }

    private static List<ComponentRecord> ExportAllMonoBehaviours(GameObject prefab)
    {
        List<ComponentRecord> records = new List<ComponentRecord>();
        MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components)
        {
            if (component != null)
            {
                records.Add(ExportComponent(component));
            }
        }

        records.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.componentType, right.componentType));
        return records;
    }

    private static ComponentRecord ExportComponent(Component component)
    {
        ComponentRecord record = new ComponentRecord
        {
            componentType = component.GetType().FullName ?? component.GetType().Name
        };

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = true;
            if (property.propertyPath == "m_Script")
            {
                continue;
            }

            record.properties.Add(new SerializedFieldRecord
            {
                propertyPath = property.propertyPath,
                propertyType = property.propertyType.ToString(),
                value = SerializedValue(property)
            });
        }

        record.properties.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.propertyPath, right.propertyPath));
        return record;
    }

    private static string SerializedValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean:
                return property.boolValue ? "true" : "false";
            case SerializedPropertyType.Float:
                return property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.String:
                return property.stringValue ?? string.Empty;
            case SerializedPropertyType.Color:
                return FormatColor(property.colorValue);
            case SerializedPropertyType.ObjectReference:
                return ObjectReference(property.objectReferenceValue);
            case SerializedPropertyType.Enum:
                return EnumValue(property);
            case SerializedPropertyType.Vector2:
                return FormatVector2(property.vector2Value);
            case SerializedPropertyType.Vector3:
                return FormatVector3(property.vector3Value);
            case SerializedPropertyType.Vector4:
                return FormatVector4(property.vector4Value);
            case SerializedPropertyType.Rect:
                return FormatRect(property.rectValue);
            case SerializedPropertyType.Bounds:
                return FormatBounds(property.boundsValue);
            case SerializedPropertyType.Vector2Int:
                return FormatVector2Int(property.vector2IntValue);
            case SerializedPropertyType.Vector3Int:
                return FormatVector3Int(property.vector3IntValue);
            case SerializedPropertyType.RectInt:
                return FormatRectInt(property.rectIntValue);
            case SerializedPropertyType.BoundsInt:
                return FormatBoundsInt(property.boundsIntValue);
            case SerializedPropertyType.ManagedReference:
                return property.managedReferenceFullTypename ?? string.Empty;
            case SerializedPropertyType.Generic:
                return string.Empty;
            default:
                return "<" + property.propertyType + ">";
        }
    }

    private static string EnumValue(SerializedProperty property)
    {
        int index = property.enumValueIndex;
        string[] names = property.enumDisplayNames;
        if (names != null && index >= 0 && index < names.Length)
        {
            return names[index];
        }

        return index.ToString(CultureInfo.InvariantCulture);
    }

    private static string ObjectReference(Object value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        string assetPath = AssetDatabase.GetAssetPath(value);
        if (!string.IsNullOrEmpty(assetPath))
        {
            return assetPath;
        }

        if (value is Component component)
        {
            return "scene:" + GetHierarchyPath(component.transform);
        }

        if (value is GameObject gameObject)
        {
            return "scene:" + GetHierarchyPath(gameObject.transform);
        }

        return value.name ?? value.GetType().Name;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static List<string> FindPrefabPaths(string root)
    {
        List<string> paths = new List<string>();
        if (!AssetDatabase.IsValidFolder(root))
        {
            return paths;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static List<string> FindScenePaths()
    {
        List<string> paths = new List<string>();
        if (!AssetDatabase.IsValidFolder(SceneRoot))
        {
            return paths;
        }

        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { SceneRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static void EnsureNoDirtyOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isDirty)
            {
                throw new InvalidOperationException(
                    "Save or discard modified scene changes before exporting documentation data. Dirty scene: "
                    + scene.path);
            }
        }
    }

    private static string FormatColor(Color value) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R},{3:R}",
        value.r,
        value.g,
        value.b,
        value.a);

    private static string FormatVector2(Vector2 value) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:R},{1:R}",
        value.x,
        value.y);

    private static string FormatVector3(Vector3 value) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R}",
        value.x,
        value.y,
        value.z);

    private static string FormatVector4(Vector4 value) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R},{3:R}",
        value.x,
        value.y,
        value.z,
        value.w);

    private static string FormatRect(Rect value) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R},{3:R}",
        value.x,
        value.y,
        value.width,
        value.height);

    private static string FormatBounds(Bounds value) => string.Format(
        CultureInfo.InvariantCulture,
        "center={0};size={1}",
        FormatVector3(value.center),
        FormatVector3(value.size));

    private static string FormatVector2Int(Vector2Int value) =>
        value.x.ToString(CultureInfo.InvariantCulture)
        + ","
        + value.y.ToString(CultureInfo.InvariantCulture);

    private static string FormatVector3Int(Vector3Int value) =>
        value.x.ToString(CultureInfo.InvariantCulture)
        + ","
        + value.y.ToString(CultureInfo.InvariantCulture)
        + ","
        + value.z.ToString(CultureInfo.InvariantCulture);

    private static string FormatRectInt(RectInt value) => string.Format(
        CultureInfo.InvariantCulture,
        "{0},{1},{2},{3}",
        value.x,
        value.y,
        value.width,
        value.height);

    private static string FormatBoundsInt(BoundsInt value) =>
        "position="
        + FormatVector3Int(value.position)
        + ";size="
        + FormatVector3Int(value.size);

    [Serializable]
    public sealed class DocumentationExport
    {
        public int schemaVersion;
        public string unityVersion;
        public string outputContract;
        public List<AssetRecord> characterSelections = new List<AssetRecord>();
        public List<AssetRecord> defaultCharacterProfiles = new List<AssetRecord>();
        public List<AssetRecord> levelSelections = new List<AssetRecord>();
        public List<AssetRecord> modeSelections = new List<AssetRecord>();
        public List<AssetRecord> supportSelections = new List<AssetRecord>();
        public List<AssetRecord> enemies = new List<AssetRecord>();
        public List<AssetRecord> bodyGuards = new List<AssetRecord>();
        public List<AssetRecord> navMeshVehicles = new List<AssetRecord>();
        public List<AssetRecord> nonNavMeshVehicles = new List<AssetRecord>();
        public List<AssetRecord> racingProfiles = new List<AssetRecord>();
        public List<SceneRecord> scenes = new List<SceneRecord>();
        public List<string> findings = new List<string>();
    }

    [Serializable]
    public sealed class AssetRecord
    {
        public string sourcePath;
        public string assetName;
        public string finding;
        public List<ComponentRecord> components = new List<ComponentRecord>();
        public List<string> findings = new List<string>();
    }

    [Serializable]
    public sealed class SceneRecord
    {
        public string sourcePath;
        public string sceneName;
        public List<SceneComponentRecord> components = new List<SceneComponentRecord>();
        public List<string> findings = new List<string>();
    }

    [Serializable]
    public sealed class SceneComponentRecord
    {
        public string hierarchyPath;
        public ComponentRecord component;
    }

    [Serializable]
    public sealed class ComponentRecord
    {
        public string componentType;
        public List<SerializedFieldRecord> properties = new List<SerializedFieldRecord>();
    }

    [Serializable]
    public sealed class SerializedFieldRecord
    {
        public string propertyPath;
        public string propertyType;
        public string value;
    }
}
