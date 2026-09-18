using System;
using System.Collections;

namespace Level5.BackendV2
{
    public interface IPlayersApiClient
    {
        IEnumerator GetByTag(string tag, Action<ApiResponse<PlayerProfileResponseDto>> completed);

        /// <summary>Returns the current player's id. The endpoint's body is a bare JSON GUID string,
        /// not a wrapping DTO - matches Backend V2's <c>PlayersController.GetMe</c> exactly.</summary>
        IEnumerator GetMe(Action<ApiResponse<Guid>> completed);

        IEnumerator UpdateMe(string displayName, Action<ApiResponse<PlayerProfileResponseDto>> completed);
    }
}
