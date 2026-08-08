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
/// Exports Unity-serialized authored data that cannot be reliably recovered from binary prefab and
/// scene files through repository text search alone.
///
/// The exporter is read-only with respect to game assets. It writes deterministic JSON under
/// docs/generated and restores the editor's original scene setup after scene inspection.
/// </summary>
public static class Level5DocumentationExporter
{
    public const int SchemaVersion = 2;
    public const string OutputRelativePath = "docs/generated/level5-authored-game-data.json";

    private const string CharacterSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/player_selected_objects";
    private const string DefaultCharacterRoot =
        "Assets/Resources/Prefabs/menu_start/default_shooter_profiles";
    private const string LevelSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/level_selected_objects";
    private const string ModeSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/mode_selected_objects";
    private const string SupportSelectionRoot =
        "Assets/Resources/Prefabs/menu_start/cheerleader_selected_object";
    private const string EnemyRoot = "Assets/Resources/Prefabs/enemies";
    private const string BodyGuardRoot = "Assets/Resources/Prefabs/bodyguards";
    private const string NavMeshVehicleRoot = "Assets/Resources/Prefabs/vehicles-navmesh";
    private const string NonNavMeshVehicleRoot = "Assets/Resources/Prefabs/vehicles-no-navmesh";
    private const string ResourcesPrefabRoot = "Assets/Resources/Prefabs";
    private const string SceneRoot = "Assets/Scenes";

    private static readonly string[] RequiredRoots =
    {
        CharacterSelectionRoot,
        DefaultCharacterRoot,
        LevelSelectionRoot,
        ModeSelectionRoot,
        SupportSelectionRoot,
        EnemyRoot,
        BodyGuardRoot,
        NavMeshVehicleRoot,
        NonNavMeshVehicleRoot,
        ResourcesPrefabRoot,
        SceneRoot
    };

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

    /// <summary>Entry point suitable for Unity -executeMethod automation.</summary>
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

    /// <summary>Builds and writes the authored-data export. Returns the absolute output path.</summary>
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
            characterSelections = ExportPrefabs(CharacterSelectionRoot, typeof(CharacterProfile)),
            defaultCharacterProfiles = ExportPrefabs(DefaultCharacterRoot, typeof(CharacterProfile)),
            levelSelections = ExportPrefabs(LevelSelectionRoot, typeof(LevelSelected)),
            modeSelections = ExportPrefabs(ModeSelectionRoot, typeof(StartScreenModeSelected)),
            supportSelections = ExportPrefabs(SupportSelectionRoot, typeof(CheerleaderProfile)),
            enemies = ExportPrefabs(EnemyRoot, typeof(EnemyController), typeof(EnemyHealth)),
            bodyGuards = ExportPrefabs(
                BodyGuardRoot,
                typeof(BodyGuardController),
                typeof(BodyGuardHealth)),
            navMeshVehicles = ExportPrefabs(NavMeshVehicleRoot, typeof(VehicleController)),
            nonNavMeshVehicles = ExportPrefabs(NonNavMeshVehicleRoot),
            racingProfiles = ExportRacingProfilePrefabs(),
            scenes = ExportScenes()
        };

        foreach (string root in RequiredRoots)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                export.findings.Add("Missing export root: " + root);
            }
        }

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
        File.WriteAllText(absolutePath, JsonUtility.ToJson(export, true) + Environment.NewLine);
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
                AssetRecord missing = new AssetRecord
                {
                    sourcePath = path,
                    assetName = Path.GetFileNameWithoutExtension(path)
                };
                missing.findings.Add("Prefab could not be loaded by AssetDatabase.");
                records.Add(missing);
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

                record.components.Sort(CompareComponents);
            }

            record.findings.Sort(StringComparer.Ordinal);
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

            record.components.Sort(CompareComponents);
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
            RestoreOriginalSceneSetup(originalSetup);
        }

        records.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.sourcePath, right.sourcePath));
        return records;
    }

    private static void RestoreOriginalSceneSetup(SceneSetup[] originalSetup)
    {
        // Unity requires RestoreSceneManagerSetup to contain at least one loaded/active scene.
        // The editor normally satisfies that contract. If a headless/editor context reports an
        // empty setup, do not leave the final authored scene open after inspection: return to a
        // neutral untitled empty scene instead.
        if (originalSetup != null && originalSetup.Length > 0)
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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

    private static int CompareSceneComponents(SceneComponentRecord left, SceneComponentRecord right)
    {
        int hierarchy = StringComparer.Ordinal.Compare(left.hierarchyPath, right.hierarchyPath);
        if (hierarchy != 0)
        {
            return hierarchy;
        }

        return CompareComponents(left.component, right.component);
    }

    private static int CompareComponents(ComponentRecord left, ComponentRecord right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int hierarchy = StringComparer.Ordinal.Compare(left.hierarchyPath, right.hierarchyPath);
        if (hierarchy != 0)
        {
            return hierarchy;
        }

        int componentType = StringComparer.Ordinal.Compare(left.componentType, right.componentType);
        if (componentType != 0)
        {
            return componentType;
        }

        return left.componentIndex.CompareTo(right.componentIndex);
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

        records.Sort(CompareComponents);
        return records;
    }

    private static ComponentRecord ExportComponent(Component component)
    {
        ComponentRecord record = new ComponentRecord
        {
            hierarchyPath = GetHierarchyPath(component.transform),
            componentType = component.GetType().FullName ?? component.GetType().Name,
            componentIndex = GetComponentTypeIndex(component)
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
            case SerializedPropertyType.Quaternion:
                return FormatQuaternion(property.quaternionValue);
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
        string[] names = property.enumNames;
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

        if (value is Component component)
        {
            return HierarchyObjectReference(component.gameObject, component);
        }

        if (value is GameObject gameObject)
        {
            return HierarchyObjectReference(gameObject, null);
        }

        string assetPath = AssetDatabase.GetAssetPath(value);
        if (!string.IsNullOrEmpty(assetPath))
        {
            return assetPath;
        }

        return value.name ?? value.GetType().Name;
    }

    private static string HierarchyObjectReference(GameObject gameObject, Component component)
    {
        if (gameObject == null)
        {
            return string.Empty;
        }

        string hierarchyPath = GetHierarchyPath(gameObject.transform);
        string assetPath = GetContainerAssetPath(gameObject.transform);

        // A prefab root GameObject is already uniquely identified by its asset path. Keep that
        // common external-prefab reference concise. Child GameObjects and all Component references
        // need the extended identity so the exact authored target survives export.
        if (!string.IsNullOrEmpty(assetPath)
            && component == null
            && gameObject.transform.parent == null)
        {
            return assetPath;
        }

        string reference = string.IsNullOrEmpty(assetPath)
            ? "scene:" + hierarchyPath
            : "asset:" + assetPath + "#" + hierarchyPath;

        if (component == null)
        {
            return reference;
        }

        string componentType = component.GetType().FullName ?? component.GetType().Name;
        return reference
            + "@"
            + componentType
            + "["
            + GetComponentTypeIndex(component).ToString(CultureInfo.InvariantCulture)
            + "]";
    }

    private static string GetContainerAssetPath(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(current.gameObject);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            current = current.parent;
        }

        return string.Empty;
    }

    private static int GetComponentTypeIndex(Component component)
    {
        if (component == null || component.gameObject == null)
        {
            return -1;
        }

        Component[] matching = component.gameObject.GetComponents(component.GetType());
        for (int i = 0; i < matching.Length; i++)
        {
            if (ReferenceEquals(matching[i], component))
            {
                return i;
            }
        }

        return -1;
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
            string escapedName = Uri.EscapeDataString(current.name ?? string.Empty);
            parts.Add(
                escapedName
                + "["
                + current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture)
                + "]");
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
            if (!string.IsNullOrEmpty(path)
                && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
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
            if (!string.IsNullOrEmpty(path)
                && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
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

    private static string FormatQuaternion(Quaternion value) => string.Format(
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

    private static string FormatBounds(Bounds value) =>
        "center=" + FormatVector3(value.center) + ";size=" + FormatVector3(value.size);

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
        "position=" + FormatVector3Int(value.position) + ";size=" + FormatVector3Int(value.size);

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
        public string hierarchyPath;
        public string componentType;
        public int componentIndex;
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
