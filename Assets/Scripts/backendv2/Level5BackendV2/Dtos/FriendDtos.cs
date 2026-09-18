using System;

namespace Level5.BackendV2
{
    // Mirrors Level5Backend/v2/src/Level5.Api/Controllers/FriendsController.cs. Friends/friend-request
    // lists are unbounded arrays on the wire - no limit/cursor envelope, unlike series lists.

    public sealed class SendFriendRequestDto
    {
        public SendFriendRequestDto(Guid toPlayerId)
        {
            ToPlayerId = toPlayerId;
        }

        public Guid ToPlayerId { get; }
    }

    public sealed class FriendRequestResponseDto
    {
        public Guid Id { get; set; }

        public Guid FromPlayerId { get; set; }

        public Guid ToPlayerId { get; set; }

        /// <summary>The domain FriendRequestStatus enum name as a string, e.g. "Pending".</summary>
        public string Status { get; set; }
    }

    public sealed class FriendSummaryDto
    {
        public Guid PlayerId { get; set; }

        public string DisplayName { get; set; }

        public string Tag { get; set; }

        public DateTimeOffset FriendsSince { get; set; }
    }
}
