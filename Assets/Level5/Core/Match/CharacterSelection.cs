using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// The character a roster slot plays as, reduced to the values the match actually needs.
    ///
    /// Deliberately not a reference to the <c>CharacterProfile</c> MonoBehaviour: those live on
    /// menu prefabs that are destroyed by the scene load the launch triggers.
    /// </summary>
    public sealed class CharacterSelection
    {
        public static readonly CharacterSelection None = new CharacterSelection(0, string.Empty, string.Empty, false, false);

        public CharacterSelection(
            int characterId,
            string objectName,
            string displayName,
            bool isShooter,
            bool isFighter)
        {
            CharacterId = characterId;
            ObjectName = objectName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            IsShooter = isShooter;
            IsFighter = isFighter;
        }

        public int CharacterId { get; }

        /// <summary>Resources prefab name, e.g. "drblood".</summary>
        public string ObjectName { get; }

        public string DisplayName { get; }

        public bool IsShooter { get; }

        public bool IsFighter { get; }

        public bool IsEmpty => CharacterId == 0 && string.IsNullOrEmpty(ObjectName);

        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? ObjectName : DisplayName;
        }
    }

    /// <summary>The cheerleader/friend selection and the shooting bonuses it contributes.</summary>
    public sealed class CheerleaderSelection
    {
        public static readonly CheerleaderSelection None = new CheerleaderSelection(0, string.Empty, string.Empty);

        public CheerleaderSelection(
            int cheerleaderId,
            string objectName,
            string displayName,
            int bonusThreeAccuracy = 0,
            int bonusFourAccuracy = 0,
            int bonusSevenAccuracy = 0,
            int bonusRelease = 0,
            int bonusRange = 0,
            int bonusLuck = 0,
            int bonusClutch = 0)
        {
            CheerleaderId = cheerleaderId;
            ObjectName = objectName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BonusThreeAccuracy = bonusThreeAccuracy;
            BonusFourAccuracy = bonusFourAccuracy;
            BonusSevenAccuracy = bonusSevenAccuracy;
            BonusRelease = bonusRelease;
            BonusRange = bonusRange;
            BonusLuck = bonusLuck;
            BonusClutch = bonusClutch;
        }

        public int CheerleaderId { get; }

        public string ObjectName { get; }

        public string DisplayName { get; }

        public int BonusThreeAccuracy { get; }

        public int BonusFourAccuracy { get; }

        public int BonusSevenAccuracy { get; }

        public int BonusRelease { get; }

        public int BonusRange { get; }

        public int BonusLuck { get; }

        public int BonusClutch { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName) ? ObjectName : DisplayName;
        }
    }

    /// <summary>Difficulty, with the numbers the menu and save data already use.</summary>
    public enum MatchDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hardcore = 2
    }

    /// <summary>The sniper variant a player can switch on. Legacy stores this as three booleans.</summary>
    public enum SniperMode
    {
        None = 0,
        Bullet = 1,
        MachineGun = 2,
        Laser = 3
    }

    /// <summary>Conversions for the legacy numeric difficulty.</summary>
    public static class MatchDifficulties
    {
        public static MatchDifficulty FromInt(int value)
        {
            return Enum.IsDefined(typeof(MatchDifficulty), value)
                ? (MatchDifficulty)value
                : MatchDifficulty.Normal;
        }

        public static int ToInt(MatchDifficulty difficulty)
        {
            return (int)difficulty;
        }
    }
}
