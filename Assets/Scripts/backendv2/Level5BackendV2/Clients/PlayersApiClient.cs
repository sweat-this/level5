using System;
using System.Collections;
using UnityEngine.Networking;

namespace Level5.BackendV2
{
    /// <summary>api/v2/players/*. All calls require an access token.</summary>
    public sealed class PlayersApiClient : AuthenticatedApiClientBase, IPlayersApiClient
    {
        public PlayersApiClient(IApiTransport transport, BackendV2SessionManager sessionManager)
            : base(transport, sessionManager)
        {
        }

        public IEnumerator GetByTag(string tag, Action<ApiResponse<PlayerProfileResponseDto>> completed)
        {
            string path = "api/v2/players/by-tag/" + UnityWebRequest.EscapeURL(tag ?? string.Empty);
            return SendAuthorized(ApiHttpMethod.Get, path, null, completed);
        }

        public IEnumerator GetMe(Action<ApiResponse<Guid>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Get, "api/v2/players/me", null, completed);
        }

        public IEnumerator GetMyProfile(Action<ApiResponse<PlayerProfileResponseDto>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Get, "api/v2/players/me/profile", null, completed);
        }

        public IEnumerator UpdateMe(string displayName, Action<ApiResponse<PlayerProfileResponseDto>> completed)
        {
            return SendAuthorized(
                ApiHttpMethod.Patch, "api/v2/players/me", new UpdatePlayerProfileRequestDto(displayName), completed);
        }
    }
}
