using System;
using System.Collections;

namespace Level5.BackendV2
{
    public interface ILeaderboardsApiClient
    {
        /// <param name="cursor">The opaque cursor from a previous page's <c>NextCursor</c>, or null
        /// for the first page. Forwarded verbatim - never parsed or constructed by the caller.</param>
        /// <param name="hardcore">Null omits the query parameter (no filtering on this modifier);
        /// a value filters on it exactly, including an explicit false.</param>
        IEnumerator GetPage(
            int modeId,
            int limit,
            string cursor,
            bool? hardcore,
            bool? traffic,
            bool? enemies,
            bool? sniper,
            Action<ApiResponse<LeaderboardPageDto>> completed);
    }
}
