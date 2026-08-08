using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// Typed game-mode identity.
    ///
    /// The numeric values are the values already stored in save data, high score rows and the
    /// backend. They are contract, not implementation detail - never renumber an existing member.
    /// New modes take the next unused number.
    ///
    /// The legacy <c>Modes</c> constants remain during migration; a test asserts the two agree.
    /// </summary>
    public enum GameModeId
    {
        /// <summary>No mode selected. Legacy code writes 0 into gameModeSelectedId before a launch.</summary>
        None = 0,
        TotalPoints = 1,
        Total3Pointers = 2,
        Total4Pointers = 3,
        Total7Pointers = 4,
        TotalDistance = 6,
        SpotUp3s = 7,
        SpotUp4s = 8,
        SpotUpAll = 9,
        ConsecutiveShots = 14,
        InThePocket = 15,
        ThreePointContest = 16,
        FourPointContest = 17,
        AllPointContest = 18,
        PointsByDistance = 19,
        BashUpSomeNerds = 20,
        BattleRoyal = 21,
        CageMatch = 22,
        VersusCpu = 23,
        SevenPointContest = 24,
        SpotUp7s = 25,
        BeatThaComputahs = 26,
        Lockdown = 27,
        Arcade = 98,
        FreePlay = 99
    }

    /// <summary>Conversions between the typed identity and the raw numbers legacy code still uses.</summary>
    public static class GameModeIds
    {
        /// <summary>Every declared mode, in declaration order.</summary>
        public static GameModeId[] All()
        {
            return (GameModeId[])Enum.GetValues(typeof(GameModeId));
        }

        public static bool IsKnown(int modeId)
        {
            return Enum.IsDefined(typeof(GameModeId), modeId);
        }

        /// <summary>
        /// Maps a stored number to typed identity. Unknown numbers map to <see cref="GameModeId.None"/>
        /// rather than throwing: old saves and hand-edited rows do contain ids this build never shipped,
        /// and refusing to read them would lock a player out of their own history.
        /// </summary>
        public static GameModeId FromInt(int modeId)
        {
            return IsKnown(modeId) ? (GameModeId)modeId : GameModeId.None;
        }

        public static int ToInt(GameModeId modeId)
        {
            return (int)modeId;
        }
    }
}
