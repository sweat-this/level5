using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// api/v2/leaderboards/{modeId} - read-only, authenticated. The server owns which modes have a
    /// leaderboard, the ranking metric/direction, row ordering and cursor validity; this client never
    /// duplicates that policy, only forwards the caller's filters and opaque cursor and returns
    /// whatever the server sent back.
    /// </summary>
    public sealed class LeaderboardsApiClient : AuthenticatedApiClientBase, ILeaderboardsApiClient
    {
        public LeaderboardsApiClient(IApiTransport transport, BackendV2SessionManager sessionManager)
            : base(transport, sessionManager)
        {
        }

        public IEnumerator GetPage(
            int modeId,
            int limit,
            string cursor,
            bool? hardcore,
            bool? traffic,
            bool? enemies,
            bool? sniper,
            Action<ApiResponse<LeaderboardPageDto>> completed)
        {
            Dictionary<string, string> query = new Dictionary<string, string> { ["limit"] = limit.ToString() };
            if (!string.IsNullOrEmpty(cursor))
            {
                query["cursor"] = cursor;
            }

            AddIfNotNull(query, "hardcore", hardcore);
            AddIfNotNull(query, "traffic", traffic);
            AddIfNotNull(query, "enemies", enemies);
            AddIfNotNull(query, "sniper", sniper);

            return SendAuthorized(ApiHttpMethod.Get, "api/v2/leaderboards/" + modeId, null, completed, query);
        }

        private static void AddIfNotNull(Dictionary<string, string> query, string key, bool? value)
        {
            if (value.HasValue)
            {
                query[key] = value.Value ? "true" : "false";
            }
        }
    }
}
