using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>api/v2/friends/*. All calls require an access token.</summary>
    public sealed class FriendsApiClient : AuthenticatedApiClientBase, IFriendsApiClient
    {
        public FriendsApiClient(IApiTransport transport, BackendV2SessionManager sessionManager)
            : base(transport, sessionManager)
        {
        }

        public IEnumerator List(Action<ApiResponse<List<FriendSummaryDto>>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Get, "api/v2/friends", null, completed);
        }

        public IEnumerator Remove(Guid playerId, Action<ApiResponse<ApiVoid>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Delete, "api/v2/friends/" + playerId, null, completed);
        }

        public IEnumerator SendRequest(Guid toPlayerId, Action<ApiResponse<FriendRequestResponseDto>> completed)
        {
            return SendAuthorized(
                ApiHttpMethod.Post, "api/v2/friends/requests", new SendFriendRequestDto(toPlayerId), completed);
        }

        public IEnumerator ListIncoming(Action<ApiResponse<List<FriendRequestResponseDto>>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Get, "api/v2/friends/requests/incoming", null, completed);
        }

        public IEnumerator ListOutgoing(Action<ApiResponse<List<FriendRequestResponseDto>>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Get, "api/v2/friends/requests/outgoing", null, completed);
        }

        public IEnumerator Accept(Guid requestId, Action<ApiResponse<ApiVoid>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Post, $"api/v2/friends/requests/{requestId}/accept", null, completed);
        }

        public IEnumerator Decline(Guid requestId, Action<ApiResponse<ApiVoid>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Post, $"api/v2/friends/requests/{requestId}/decline", null, completed);
        }

        public IEnumerator Cancel(Guid requestId, Action<ApiResponse<ApiVoid>> completed)
        {
            return SendAuthorized(ApiHttpMethod.Post, $"api/v2/friends/requests/{requestId}/cancel", null, completed);
        }
    }
}
