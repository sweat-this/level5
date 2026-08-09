using System.Collections.Generic;

namespace Level5.Core.Match
{
    /// <summary>
    /// Every arena this build knows about, looked up by id or scene object name.
    ///
    /// Named for the definition it holds rather than "LevelCatalog" because the legacy
    /// <c>LevelCatalog</c> over <c>LevelPreset</c> still exists in the game assembly while the menu
    /// is migrated; keeping distinct names means both can be in scope in the same file.
    /// </summary>
    public sealed class LevelDefinitionCatalog
    {
        private readonly List<LevelDefinition> definitions = new List<LevelDefinition>();
        private readonly Dictionary<int, LevelDefinition> byId = new Dictionary<int, LevelDefinition>();
        private readonly List<string> problems = new List<string>();

        public LevelDefinitionCatalog(IEnumerable<LevelDefinition> levelDefinitions)
        {
            if (levelDefinitions == null)
            {
                return;
            }

            foreach (LevelDefinition definition in levelDefinitions)
            {
                if (definition == null)
                {
                    problems.Add("the level catalog contains an empty entry");
                    continue;
                }

                if (byId.ContainsKey(definition.LevelId))
                {
                    problems.Add($"duplicate level id {definition.LevelId} ('{definition.DisplayName}')");
                    continue;
                }

                byId.Add(definition.LevelId, definition);
                definitions.Add(definition);
            }
        }

        public IReadOnlyList<LevelDefinition> Definitions => definitions;

        public IReadOnlyList<string> Problems => problems;

        public int Count => definitions.Count;

        public LevelDefinition Find(int levelId)
        {
            return byId.TryGetValue(levelId, out LevelDefinition definition) ? definition : null;
        }

        public LevelDefinition FindByObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            foreach (LevelDefinition definition in definitions)
            {
                if (definition.ObjectName == objectName)
                {
                    return definition;
                }
            }

            return null;
        }

        public static LevelDefinitionCatalog Empty()
        {
            return new LevelDefinitionCatalog(null);
        }
    }
}
