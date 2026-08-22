using System.Collections.Generic;

namespace Level5.Core.Progression
{
    /// <summary>
    /// The one authoritative answer to "is this character/level unlocked?", as of the moment it was
    /// built. Character answers are account-scoped (see <c>UnlockSnapshotBuilder</c>'s SQLite/JSON
    /// precedence); level answers currently reflect only authored content
    /// (<c>LevelDefinition.Locked</c>) - there is no durable per-account level entitlement yet, see
    /// docs/persistence-boundaries.md.
    ///
    /// A plain, immutable projection - not a service. Every menu item that needs an unlock answer
    /// reads this same snapshot instead of each independently reaching into SQLite, JSON, or a
    /// singleton, so no two call sites can compute a different answer for the same character or
    /// level. It is built once per refresh (see the adapter that constructs it) and handed to
    /// whichever pure logic needs it - cycling, roster projection, launch validation - as a plain
    /// argument.
    ///
    /// No <c>UnityEngine</c>, database, singleton, or filesystem dependency belongs here.
    /// </summary>
    public sealed class UnlockSnapshot
    {
        private static readonly IReadOnlyDictionary<int, bool> EmptyMap = new Dictionary<int, bool>();

        private readonly IReadOnlyDictionary<int, bool> characters;
        private readonly IReadOnlyDictionary<int, bool> levels;

        public UnlockSnapshot(IReadOnlyDictionary<int, bool> characters, IReadOnlyDictionary<int, bool> levels)
        {
            this.characters = characters ?? EmptyMap;
            this.levels = levels ?? EmptyMap;
        }

        /// <summary>An empty snapshot: every character and level answers as locked.</summary>
        public static readonly UnlockSnapshot Empty = new UnlockSnapshot(null, null);

        /// <summary>
        /// Unlocked only if this account is known to have unlocked it. An id this snapshot has no
        /// entry for answers locked - a deliberate safe default, not "not yet known".
        /// </summary>
        public bool IsCharacterUnlocked(int characterId)
        {
            return characters.TryGetValue(characterId, out bool unlocked) && unlocked;
        }

        public bool IsLevelUnlocked(int levelId)
        {
            return levels.TryGetValue(levelId, out bool unlocked) && unlocked;
        }
    }
}
