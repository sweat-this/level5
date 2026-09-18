using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// Tracks which rows (friend requests, series) currently have a command in flight - send/accept/
    /// decline/cancel a friend request, accept/decline/cancel a challenge, start an attempt - so a
    /// panel can disable that row's buttons and never send a second concurrent command for the same
    /// row while the first is still outstanding.
    /// </summary>
    public sealed class RowCommandState
    {
        private readonly HashSet<Guid> inFlight = new HashSet<Guid>();

        public bool IsInFlight(Guid rowId)
        {
            return inFlight.Contains(rowId);
        }

        /// <summary>Claims the row for a command. Returns false, refusing the claim, if one is
        /// already in flight for this row.</summary>
        public bool TryBegin(Guid rowId)
        {
            return inFlight.Add(rowId);
        }

        public void End(Guid rowId)
        {
            inFlight.Remove(rowId);
        }
    }
}
