using Level5.Core.Match;

namespace Level5.Core.PlayerSelection
{
    /// <summary>
    /// One draft slot in the roster being assembled at player select.
    ///
    /// Replaces the legacy pattern of a bare index plus a magic "0 means none" character. A slot
    /// is either inactive (no character chosen) or active with a stable character id - there is no
    /// third state and no fake character record.
    /// </summary>
    public sealed class PlayerSelectionSlot
    {
        private PlayerSelectionSlot(PlayerControlType controlType, int? characterId)
        {
            ControlType = controlType;
            CharacterId = characterId;
        }

        public PlayerControlType ControlType { get; }

        public int? CharacterId { get; }

        public bool IsActive => CharacterId.HasValue;

        public static PlayerSelectionSlot Inactive(PlayerControlType controlType)
        {
            return new PlayerSelectionSlot(controlType, null);
        }

        public static PlayerSelectionSlot Active(PlayerControlType controlType, int characterId)
        {
            return new PlayerSelectionSlot(controlType, characterId);
        }

        public override string ToString()
        {
            return IsActive ? $"{ControlType} #{CharacterId}" : $"{ControlType} (empty)";
        }
    }
}
