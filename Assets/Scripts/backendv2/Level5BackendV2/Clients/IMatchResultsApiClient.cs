using System;
using System.Collections;

namespace Level5.BackendV2
{
    public interface IMatchResultsApiClient
    {
        IEnumerator Submit(SubmitMatchResultDto request, Action<ApiResponse<MatchResultResponseDto>> completed);
    }
}
