namespace Level5.Core.Match
{
    /// <summary>
    /// Who drives a roster slot.
    ///
    /// The remote and replay members exist so the roster shape does not have to change when those
    /// arrive; nothing in this overhaul implements them, and the builder rejects them today.
    /// </summary>
    public enum PlayerControlType
    {
        /// <summary>A person at this machine, holding one of the local input slots.</summary>
        LocalHuman,

        /// <summary>Driven by the game's AI.</summary>
        Cpu,

        /// <summary>A person on another machine. Not implemented.</summary>
        RemoteHuman,

        /// <summary>Played back from a recording. Not implemented.</summary>
        ReplayGhost
    }
}
