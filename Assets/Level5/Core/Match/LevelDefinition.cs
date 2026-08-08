using UnityEngine;

namespace Level5.Core.Match
{
    /// <summary>
    /// Authored data for one arena.
    ///
    /// Replaces the scattered per-level booleans that used to be copied into global state
    /// (<c>levelRequiresTimeOfDay</c>, <c>levelRequiresWeather</c>, <c>levelHasSevenPointers</c>,
    /// <c>customCamera</c>) with capability flags plus the few values that are not capabilities.
    ///
    /// Like <see cref="GameModeDefinition"/> this is authored data only: never write match state here.
    /// </summary>
    [CreateAssetMenu(menuName = "Level 5/Match/Level Definition", fileName = "LevelDefinition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable numeric id, matching the authored LevelSelected prefab.")]
        [SerializeField] private int levelId;
        [SerializeField] private string displayName;
        [SerializeField] private string info;
        [Tooltip("Scene name prefix, e.g. 'level_01_scrapyard' without the descriptor suffix.")]
        [SerializeField] private string objectName;
        [Tooltip("Scene name suffix appended after an underscore to build the scene name.")]
        [SerializeField] private string sceneDescriptor;

        [Header("Capabilities")]
        [SerializeField] private ArenaCapability capabilities = ArenaCapability.None;

        [Header("Presentation")]
        [SerializeField] private bool customCamera;
        [SerializeField] private bool selectable = true;
        [SerializeField] private bool locked;

        public int LevelId => levelId;

        public string DisplayName => displayName;

        public string Info => info;

        public string ObjectName => objectName;

        public string SceneDescriptor => sceneDescriptor;

        /// <summary>
        /// The scene this level loads. Mirrors the legacy
        /// <c>LevelObjectName + "_" + LevelDescription</c> concatenation exactly.
        /// </summary>
        public string SceneName => string.IsNullOrEmpty(sceneDescriptor)
            ? objectName
            : objectName + "_" + sceneDescriptor;

        public ArenaCapability Capabilities => capabilities;

        public bool CustomCamera => customCamera;

        public bool Selectable => selectable;

        public bool Locked => locked;

        public bool Supports(ArenaCapability capability)
        {
            return (capabilities & capability) == capability;
        }

        // ---- derived legacy views -------------------------------------------------------------

        public bool IsShootingLevel => Supports(ArenaCapability.Basketball);

        public bool IsFightingLevel => Supports(ArenaCapability.Combat);

        public bool IsBattleRoyalLevel => Supports(ArenaCapability.BattleRoyal);

        public bool IsCageMatchLevel => Supports(ArenaCapability.Cage);

        public bool HasSevenPointers => Supports(ArenaCapability.SevenPointLine);

        public bool HasTraffic => Supports(ArenaCapability.Traffic);

        public bool RequiresTimeOfDay => Supports(ArenaCapability.TimeOfDay);

        public bool HasWeather => Supports(ArenaCapability.Weather);

        public static LevelDefinition Create(LevelDefinitionData data)
        {
            LevelDefinition definition = CreateInstance<LevelDefinition>();
            definition.Apply(data);
            definition.name = string.IsNullOrEmpty(data.ObjectName)
                ? "level_" + data.LevelId
                : data.ObjectName;
            return definition;
        }

        /// <summary>Overwrites every authored field. Editor migration only - never call at runtime.</summary>
        public void Apply(LevelDefinitionData data)
        {
            levelId = data.LevelId;
            displayName = data.DisplayName;
            info = data.Info;
            objectName = data.ObjectName;
            sceneDescriptor = data.SceneDescriptor;
            capabilities = data.Capabilities;
            customCamera = data.CustomCamera;
            selectable = data.Selectable;
            locked = data.Locked;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(displayName) ? "level " + levelId : displayName;
        }
    }

    /// <summary>Plain carrier for the authored level fields.</summary>
    public struct LevelDefinitionData
    {
        public int LevelId;
        public string DisplayName;
        public string Info;
        public string ObjectName;
        public string SceneDescriptor;
        public ArenaCapability Capabilities;
        public bool CustomCamera;
        public bool Selectable;
        public bool Locked;

        public static LevelDefinitionData Default(int levelId)
        {
            return new LevelDefinitionData
            {
                LevelId = levelId,
                DisplayName = string.Empty,
                Info = string.Empty,
                ObjectName = string.Empty,
                SceneDescriptor = string.Empty,
                Capabilities = ArenaCapability.Basketball | ArenaCapability.Multiplayer,
                CustomCamera = false,
                Selectable = true,
                Locked = false
            };
        }
    }
}
