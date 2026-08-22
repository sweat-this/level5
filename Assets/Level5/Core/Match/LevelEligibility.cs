using Level5.Core.Progression;

namespace Level5.Core.Match
{
    /// <summary>
    /// Whether a level can actually be chosen right now: authored content, mode compatibility and
    /// account unlock state, composed - never collapsed into one mutable boolean.
    ///
    /// <see cref="GameModeCompatibility"/> deliberately does not know about unlock state (its own
    /// doc comment says so: "Unlock/account availability is not part of this method"), because mode
    /// compatibility is a property of authored content and account unlock is a property of the
    /// player. This is the one place the three gates - <see cref="LevelDefinition.Selectable"/>,
    /// <see cref="GameModeCompatibility.CanPlay"/>, and <see cref="UnlockSnapshot.IsLevelUnlocked"/>
    /// - are combined, so cycling and launch validation ask the same question instead of each
    /// re-deriving it.
    ///
    /// <paramref name="unlock"/> is accepted as null throughout: callers that have not been migrated
    /// to pass a snapshot yet get exactly the old mode/arena-only behavior, unchanged.
    /// </summary>
    public static class LevelEligibility
    {
        /// <summary>Authored-content gate: does this level exist and offer itself for selection at all.</summary>
        public static bool IsSelectableContent(LevelDefinition level)
        {
            return level != null && level.Selectable;
        }

        /// <summary>Account gate: has this account unlocked the level. Null snapshot means "not gated".</summary>
        public static bool IsUnlockedForAccount(LevelDefinition level, UnlockSnapshot unlock)
        {
            return level != null && (unlock == null || unlock.IsLevelUnlocked(level.LevelId));
        }

        /// <summary>Content- and account-gated, independent of any particular mode.</summary>
        public static bool IsAvailable(LevelDefinition level, UnlockSnapshot unlock)
        {
            return IsSelectableContent(level) && IsUnlockedForAccount(level, unlock);
        }

        /// <summary>The full selection question: content, mode and account gates together.</summary>
        public static bool CanSelect(
            LevelDefinition level,
            GameModeDefinition mode,
            GameModeCompatibility compatibility,
            UnlockSnapshot unlock)
        {
            if (compatibility == null)
            {
                return false;
            }

            return IsAvailable(level, unlock) && compatibility.CanPlay(mode, level);
        }

        /// <summary>
        /// The next eligible level index for a mode, stepping by <paramref name="step"/> from
        /// <paramref name="startIndex"/> and wrapping - the same bounded, single-pass walk
        /// <see cref="GameModeCompatibility.NextCompatibleLevelIndex"/> uses, extended to also skip
        /// levels that are not selectable or not unlocked. Terminates and returns
        /// <paramref name="startIndex"/> when nothing is eligible, so the menu holds still instead
        /// of looping.
        /// </summary>
        public static int NextEligibleLevelIndex(
            GameModeCompatibility compatibility,
            GameModeDefinition mode,
            int startIndex,
            int step,
            UnlockSnapshot unlock)
        {
            if (compatibility == null)
            {
                return startIndex;
            }

            int count = compatibility.Levels.Count;
            if (mode == null || count == 0 || step == 0)
            {
                return startIndex;
            }

            for (int offset = 1; offset <= count; offset++)
            {
                int index = IndexMath.Wrap(startIndex + (offset * step), count);
                if (CanSelect(compatibility.Levels.Definitions[index], mode, compatibility, unlock))
                {
                    return index;
                }
            }

            return startIndex;
        }

        /// <summary>
        /// The eligible level index to hold after a mode change: the current one if it is still
        /// eligible, otherwise the nearest eligible one in the given direction.
        /// </summary>
        public static int EligibleLevelIndexFor(
            GameModeCompatibility compatibility,
            GameModeDefinition mode,
            int currentIndex,
            int step,
            UnlockSnapshot unlock)
        {
            if (compatibility == null || compatibility.Levels.Count == 0)
            {
                return currentIndex;
            }

            int wrapped = IndexMath.Wrap(currentIndex, compatibility.Levels.Count);
            if (CanSelect(compatibility.Levels.Definitions[wrapped], mode, compatibility, unlock))
            {
                return wrapped;
            }

            return NextEligibleLevelIndex(compatibility, mode, wrapped, step, unlock);
        }
    }
}
