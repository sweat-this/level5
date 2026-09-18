using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// Loading/empty/error/pagination view state shared by every list panel: the friends list, both
    /// friend-request lists, and all four series tabs (Incoming/Outgoing/Active/Completed).
    ///
    /// Holds only view state, never a decision Backend V2 owns - it caches the last fetched DTOs
    /// verbatim and an opaque cursor forwarded exactly as received, never parsed or reconstructed.
    /// Friend/friend-request lists never set a cursor (their client methods return a plain list); the
    /// series list methods do.
    /// </summary>
    public sealed class ListViewState<T>
    {
        public IReadOnlyList<T> Items { get; private set; } = Array.Empty<T>();

        public bool IsLoading { get; private set; }

        public string ErrorMessage { get; private set; }

        public string NextCursor { get; private set; }

        public bool HasMore => !string.IsNullOrEmpty(NextCursor);

        public bool IsEmpty => !IsLoading && ErrorMessage == null && Items.Count == 0;

        public void BeginLoad()
        {
            IsLoading = true;
            ErrorMessage = null;
        }

        /// <summary>A fresh page replaces the list entirely - a refresh. Use <see cref="AppendPage"/>
        /// for "load more".</summary>
        public void ReplaceWith(IReadOnlyList<T> items, string nextCursor = null)
        {
            Items = items ?? Array.Empty<T>();
            NextCursor = nextCursor;
            IsLoading = false;
            ErrorMessage = null;
        }

        public void AppendPage(IReadOnlyList<T> items, string nextCursor)
        {
            List<T> combined = new List<T>(Items);
            if (items != null)
            {
                combined.AddRange(items);
            }

            Items = combined;
            NextCursor = nextCursor;
            IsLoading = false;
            ErrorMessage = null;
        }

        public void Fail(string errorMessage)
        {
            IsLoading = false;
            ErrorMessage = string.IsNullOrEmpty(errorMessage) ? "something went wrong" : errorMessage;
        }
    }
}
