using System.Collections.Generic;
using Level5.Core.Match;

namespace Level5.Core.PlayerSelection
{
    /// <summary>
    /// A read-only selection projection: what player-selection logic needs to know about a
    /// character, and nothing else.
    ///
    /// Deliberately not another authored character model. <c>CharacterPreset</c> and the live
    /// loaded <c>CharacterProfile</c> data remain the sources of truth; this is a snapshot built
    /// from them by the adapter layer and discarded/rebuilt whenever that data refreshes.
    ///
    /// No <c>UnityEngine</c>, database, singleton, or scene dependency belongs here.
    /// </summary>
    public sealed class CharacterSelectOption
    {
        public CharacterSelectOption(
            int characterId,
            string displayName,
            string objectName,
            bool isShooter,
            bool isFighter,
            bool isUnlocked,
            CharacterSelectStats stats)
        {
            CharacterId = characterId;
            DisplayName = displayName ?? string.Empty;
            ObjectName = objectName ?? string.Empty;
            IsShooter = isShooter;
            IsFighter = isFighter;
            IsUnlocked = isUnlocked;
            Stats = stats ?? CharacterSelectStats.Empty;
        }

        public int CharacterId { get; }

        public string DisplayName { get; }

        public string ObjectName { get; }

        public bool IsShooter { get; }

        public bool IsFighter { get; }

        public bool IsUnlocked { get; }

        public CharacterSelectStats Stats { get; }

        /// <summary>
        /// The match-facing representation of this option. Roster/compatibility code already
        /// speaks <see cref="CharacterSelection"/>; this is the one conversion point instead of a
        /// second copy of the same fields.
        /// </summary>
        public CharacterSelection ToSelection(string objectNameOverride = null)
        {
            return new CharacterSelection(
                CharacterId,
                string.IsNullOrEmpty(objectNameOverride) ? ObjectName : objectNameOverride,
                DisplayName,
                IsShooter,
                IsFighter);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? ObjectName : DisplayName;
        }
    }

    /// <summary>
    /// The numeric display snapshot the start menu and progression screen show for a character.
    /// Computed once by the catalog adapter, not recalculated (or written back) on every render.
    /// </summary>
    public sealed class CharacterSelectStats
    {
        public static readonly CharacterSelectStats Empty = new CharacterSelectStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        public CharacterSelectStats(
            int level,
            int experience,
            int experienceToNextLevel,
            int pointsAvailable,
            float accuracy3Pt,
            float accuracy4Pt,
            float accuracy7Pt,
            float release,
            float range,
            float speedPercent,
            float jumpPercent,
            int luck,
            int? effectiveClutch = null)
        {
            Level = level;
            Experience = experience;
            ExperienceToNextLevel = experienceToNextLevel;
            PointsAvailable = pointsAvailable;
            Accuracy3Pt = accuracy3Pt;
            Accuracy4Pt = accuracy4Pt;
            Accuracy7Pt = accuracy7Pt;
            Release = release;
            Range = range;
            SpeedPercent = speedPercent;
            JumpPercent = jumpPercent;
            Luck = luck;

            // The current effective clutch rule, preserved from the old render-time mutation.
            EffectiveClutch = effectiveClutch ?? CharacterLevel.EffectiveClutchFromLevel(level);
        }

        public int Level { get; }

        public int Experience { get; }

        public int ExperienceToNextLevel { get; }

        public int PointsAvailable { get; }

        public float Accuracy3Pt { get; }

        public float Accuracy4Pt { get; }

        public float Accuracy7Pt { get; }

        public float Release { get; }

        public float Range { get; }

        public float SpeedPercent { get; }

        public float JumpPercent { get; }

        public int Luck { get; }

        public int EffectiveClutch { get; }
    }

    public static class CharacterSelectOptions
    {
        public static CharacterSelectOption Find(IReadOnlyList<CharacterSelectOption> options, int characterId)
        {
            if (options == null)
            {
                return null;
            }

            for (int i = 0; i < options.Count; i++)
            {
                CharacterSelectOption option = options[i];
                if (option != null && option.CharacterId == characterId)
                {
                    return option;
                }
            }

            return null;
        }

        public static int IndexOf(IReadOnlyList<CharacterSelectOption> options, int characterId)
        {
            if (options == null)
            {
                return -1;
            }

            for (int i = 0; i < options.Count; i++)
            {
                CharacterSelectOption option = options[i];
                if (option != null && option.CharacterId == characterId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
