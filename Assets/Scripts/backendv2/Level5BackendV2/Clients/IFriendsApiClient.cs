using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    public interface IFriendsApiClient
    {
        IEnumerator List(Action<ApiResponse<List<FriendSummaryDto>>> completed);

        IEnumerator Remove(Guid playerId, Action<ApiResponse<ApiVoid>> completed);

        IEnumerator SendRequest(Guid toPlayerId, Action<ApiResponse<FriendRequestResponseDto>> completed);

        IEnumerator ListIncoming(Action<ApiResponse<List<FriendRequestResponseDto>>> completed);

        IEnumerator ListOutgoing(Action<ApiResponse<List<FriendRequestResponseDto>>> completed);

        IEnumerator Accept(Guid requestId, Action<ApiResponse<ApiVoid>> completed);

        IEnumerator Decline(Guid requestId, Action<ApiResponse<ApiVoid>> completed);

        IEnumerator Cancel(Guid requestId, Action<ApiResponse<ApiVoid>> completed);
    }
}
