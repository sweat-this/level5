using System;

namespace Level5.BackendV2
{
    // Mirrors Level5Backend/v2/src/Level5.Api/Controllers/PlayersController.cs.

    public sealed class PlayerProfileResponseDto
    {
        public Guid PlayerId { get; set; }

        public string DisplayName { get; set; }

        public string Tag { get; set; }
    }

    public sealed class UpdatePlayerProfileRequestDto
    {
        public UpdatePlayerProfileRequestDto(string displayName)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; }
    }
}
