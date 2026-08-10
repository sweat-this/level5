namespace Level5.Core.Match
{
    /// <summary>
    /// The one wrap-around index helper. Bounded cycling (levels, modes, players, CPU slots) all
    /// need "step by N and wrap", and it had drifted into three separate copies
    /// (<c>StartMenuSelectionState</c>, <c>GameModeCompatibility</c>, and the player-selection
    /// core) with slightly different zero/negative-count guards. One copy means a fix to the
    /// wrap rule cannot miss a sibling.
    /// </summary>
    public static class IndexMath
    {
        /// <summary>Wraps <paramref name="value"/> into [0, count). Returns 0 when count is not positive.</summary>
        public static int Wrap(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
