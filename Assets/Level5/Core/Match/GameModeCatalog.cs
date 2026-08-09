using System.Collections.Generic;

namespace Level5.Core.Match
{
    /// <summary>
    /// Every mode this build knows about, looked up by typed id.
    ///
    /// Construction rejects duplicate ids: two definitions claiming the same mode is the failure
    /// that silently makes half the game read one set of rules and half the other.
    /// </summary>
    public sealed class GameModeCatalog
    {
        private readonly List<GameModeDefinition> definitions = new List<GameModeDefinition>();
        private readonly Dictionary<int, GameModeDefinition> byId = new Dictionary<int, GameModeDefinition>();
        private readonly List<string> problems = new List<string>();

        public GameModeCatalog(IEnumerable<GameModeDefinition> modeDefinitions)
        {
            if (modeDefinitions == null)
            {
                return;
            }

            foreach (GameModeDefinition definition in modeDefinitions)
            {
                if (definition == null)
                {
                    problems.Add("the mode catalog contains an empty entry");
                    continue;
                }

                if (byId.ContainsKey(definition.RawModeId))
                {
                    problems.Add($"duplicate game mode id {definition.RawModeId} ('{definition.DisplayName}')");
                    continue;
                }

                byId.Add(definition.RawModeId, definition);
                definitions.Add(definition);
            }
        }

        public IReadOnlyList<GameModeDefinition> Definitions => definitions;

        /// <summary>Problems found while building the catalog. Empty means the catalog is sound.</summary>
        public IReadOnlyList<string> Problems => problems;

        public int Count => definitions.Count;

        public GameModeDefinition Find(GameModeId modeId)
        {
            return Find(GameModeIds.ToInt(modeId));
        }

        public GameModeDefinition Find(int modeId)
        {
            return byId.TryGetValue(modeId, out GameModeDefinition definition) ? definition : null;
        }

        public GameModeDefinition FindByObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            foreach (GameModeDefinition definition in definitions)
            {
                if (definition.ObjectName == objectName)
                {
                    return definition;
                }
            }

            return null;
        }

        public static GameModeCatalog Empty()
        {
            return new GameModeCatalog(null);
        }
    }
}
