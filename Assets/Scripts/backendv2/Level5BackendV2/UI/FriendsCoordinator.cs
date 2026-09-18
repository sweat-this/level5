using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// Orchestrates the Friends panel's three lists (friends, incoming requests, outgoing requests)
    /// and their commands against <see cref="BackendV2Runtime.Players"/> /
    /// <see cref="BackendV2Runtime.Friends"/>.
    ///
    /// Holds only view state - loading/error flags, the last fetched list, which row has a command in
    /// flight. Backend V2 remains the source of truth for every list; nothing here is cached across a
    /// fresh refresh, and every mutating command re-fetches the affected list(s) afterward rather
    /// than guessing the new state locally.
    /// </summary>
    public sealed class FriendsCoordinator
    {
        public ListViewState<FriendSummaryDto> Friends { get; } = new ListViewState<FriendSummaryDto>();

        public ListViewState<FriendRequestResponseDto> Incoming { get; } = new ListViewState<FriendRequestResponseDto>();

        public ListViewState<FriendRequestResponseDto> Outgoing { get; } = new ListViewState<FriendRequestResponseDto>();

        public RowCommandState Commands { get; } = new RowCommandState();

        public IEnumerator RefreshFriends()
        {
            Friends.BeginLoad();
            ApiResponse<List<FriendSummaryDto>> response = null;
            yield return BackendV2Runtime.Friends.List(r => response = r);

            if (response != null && response.Success)
            {
                Friends.ReplaceWith(response.Value);
            }
            else
            {
                Friends.Fail(DescribeFailure(response));
            }
        }

        public IEnumerator RefreshIncoming()
        {
            Incoming.BeginLoad();
            ApiResponse<List<FriendRequestResponseDto>> response = null;
            yield return BackendV2Runtime.Friends.ListIncoming(r => response = r);

            if (response != null && response.Success)
            {
                Incoming.ReplaceWith(response.Value);
            }
            else
            {
                Incoming.Fail(DescribeFailure(response));
            }
        }

        public IEnumerator RefreshOutgoing()
        {
            Outgoing.BeginLoad();
            ApiResponse<List<FriendRequestResponseDto>> response = null;
            yield return BackendV2Runtime.Friends.ListOutgoing(r => response = r);

            if (response != null && response.Success)
            {
                Outgoing.ReplaceWith(response.Value);
            }
            else
            {
                Outgoing.Fail(DescribeFailure(response));
            }
        }

        public IEnumerator RefreshAll()
        {
            yield return RefreshFriends();
            yield return RefreshIncoming();
            yield return RefreshOutgoing();
        }

        /// <summary>Resolves a player by tag, then sends them a friend request - two Backend V2
        /// calls (there is no combined endpoint), reported to the caller as one logical command.
        /// Refreshes the outgoing list on success.</summary>
        public IEnumerator SendRequestByTag(string tag, Action<string> completed)
        {
            ApiResponse<PlayerProfileResponseDto> lookup = null;
            yield return BackendV2Runtime.Players.GetByTag(tag, r => lookup = r);

            if (lookup == null || !lookup.Success)
            {
                completed?.Invoke(DescribeFailure(lookup));
                yield break;
            }

            Guid playerId = lookup.Value.PlayerId;
            if (!Commands.TryBegin(playerId))
            {
                completed?.Invoke("a request to this player is already in flight");
                yield break;
            }

            ApiResponse<FriendRequestResponseDto> sendResult = null;
            yield return BackendV2Runtime.Friends.SendRequest(playerId, r => sendResult = r);

            if (sendResult != null && sendResult.Success)
            {
                // Held through the refresh, not just the network call: releasing it as soon as the
                // command settles would let a rapid second tap reach the server again for a request
                // the UI hasn't shown as resolved yet (the refresh below is what actually removes/
                // updates the row).
                yield return RefreshOutgoing();
                Commands.End(playerId);
                completed?.Invoke(null);
            }
            else
            {
                Commands.End(playerId);
                completed?.Invoke(DescribeFailure(sendResult));
            }
        }

        public IEnumerator Accept(Guid requestId, Action<string> completed)
        {
            if (!Commands.TryBegin(requestId))
            {
                completed?.Invoke("already in progress");
                yield break;
            }

            ApiResponse<ApiVoid> response = null;
            yield return BackendV2Runtime.Friends.Accept(requestId, r => response = r);

            if (response != null && response.Success)
            {
                yield return RefreshIncoming();
                yield return RefreshFriends();
                Commands.End(requestId);
                completed?.Invoke(null);
            }
            else
            {
                Commands.End(requestId);
                completed?.Invoke(DescribeFailure(response));
            }
        }

        public IEnumerator Decline(Guid requestId, Action<string> completed)
        {
            if (!Commands.TryBegin(requestId))
            {
                completed?.Invoke("already in progress");
                yield break;
            }

            ApiResponse<ApiVoid> response = null;
            yield return BackendV2Runtime.Friends.Decline(requestId, r => response = r);

            if (response != null && response.Success)
            {
                yield return RefreshIncoming();
                Commands.End(requestId);
                completed?.Invoke(null);
            }
            else
            {
                Commands.End(requestId);
                completed?.Invoke(DescribeFailure(response));
            }
        }

        public IEnumerator Cancel(Guid requestId, Action<string> completed)
        {
            if (!Commands.TryBegin(requestId))
            {
                completed?.Invoke("already in progress");
                yield break;
            }

            ApiResponse<ApiVoid> response = null;
            yield return BackendV2Runtime.Friends.Cancel(requestId, r => response = r);

            if (response != null && response.Success)
            {
                yield return RefreshOutgoing();
                Commands.End(requestId);
                completed?.Invoke(null);
            }
            else
            {
                Commands.End(requestId);
                completed?.Invoke(DescribeFailure(response));
            }
        }

        public IEnumerator Remove(Guid playerId, Action<string> completed)
        {
            if (!Commands.TryBegin(playerId))
            {
                completed?.Invoke("already in progress");
                yield break;
            }

            ApiResponse<ApiVoid> response = null;
            yield return BackendV2Runtime.Friends.Remove(playerId, r => response = r);

            if (response != null && response.Success)
            {
                yield return RefreshFriends();
                Commands.End(playerId);
                completed?.Invoke(null);
            }
            else
            {
                Commands.End(playerId);
                completed?.Invoke(DescribeFailure(response));
            }
        }

        private static string DescribeFailure<T>(ApiResponse<T> response) => BackendV2ErrorMessages.Describe(response);
    }
}
