using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;

/// <summary>
/// Resolves the current local player's stable primary character selection - the same identity
/// <see cref="PlayerSelectCoordinator"/> uses for ordinary local launches - into a launchable
/// <see cref="CharacterSelection"/> for a remote correspondence attempt (issue #179).
///
/// Reuses the existing player-select authority end to end rather than creating a second,
/// correspondence-owned character system: <see cref="PlayerSelectionSession"/>'s remembered stable
/// id, <see cref="PlayerSelectCatalogAdapter"/>'s live profile projection, <see
/// cref="UnlockSnapshotBuilder"/>'s unlock answer, <see cref="PlayerSelectionController"/>'s
/// null/stale-id fallback and launch validation, and <see cref="LegacyCharacterVariantResolver"/>'s
/// Wizard-of-Boat launch-time variant.
/// </summary>
public static class RemoteCharacterSelectionResolver
{
    /// <summary>
    /// Production entry point: projects the same loaded profile/unlock data local player select
    /// already draws from (<see cref="LoadedData"/>, <see cref="MatchCatalogs.Levels"/>) and
    /// resolves the current primary through it.
    /// </summary>
    public static RemoteCharacterSelectionResult ResolveCurrentPrimary()
    {
        IReadOnlyList<CharacterProfile> primaryProfiles = LoadedData.instance != null ? LoadedData.instance.PlayerSelectedData : null;
        if (primaryProfiles == null || primaryProfiles.Count == 0)
        {
            return RemoteCharacterSelectionResult.Failure("no player character data is loaded yet");
        }

        IReadOnlyList<CharacterProfile> cpuProfiles = LoadedData.instance.CpuPlayerSelectedData;
        UnlockSnapshot unlock = UnlockSnapshotBuilder.Build(primaryProfiles, cpuProfiles, MatchCatalogs.Levels);
        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(primaryProfiles, cpuProfiles, unlock);

        return ResolveFromCatalog(catalog.PrimaryOptions);
    }

    /// <summary>
    /// The pure resolution core, exposed so it is testable without a loaded scene: resolves <see
    /// cref="PlayerSelectionSession.PrimaryCharacterId"/> against <paramref name="primaryOptions"/>
    /// using exactly the same fallback (<see cref="PlayerSelectionController.EnsurePrimarySelected"/>),
    /// launch validation (<see cref="PlayerSelectionController.ValidateLaunch"/>) and match-facing
    /// conversion (<see cref="CharacterSelectOption.ToSelection"/> plus <see
    /// cref="LegacyCharacterVariantResolver"/>) normal player-select initialization uses. A fallback
    /// that changes the selected id is remembered back into <see cref="PlayerSelectionSession"/>,
    /// matching existing behavior.
    /// </summary>
    public static RemoteCharacterSelectionResult ResolveFromCatalog(IReadOnlyList<CharacterSelectOption> primaryOptions)
    {
        if (primaryOptions == null || primaryOptions.Count == 0)
        {
            return RemoteCharacterSelectionResult.Failure("no selectable player characters are available");
        }

        PlayerSelectionState state = new PlayerSelectionState { PrimaryCharacterId = PlayerSelectionSession.PrimaryCharacterId };
        PlayerSelectionController controller = new PlayerSelectionController(state);

        controller.EnsurePrimarySelected(primaryOptions);
        if (state.PrimaryCharacterId != PlayerSelectionSession.PrimaryCharacterId)
        {
            PlayerSelectionSession.RememberPrimary(state.PrimaryCharacterId);
        }

        PlayerSelectValidation validation = controller.ValidateLaunch(primaryOptions);
        if (!validation.IsValid)
        {
            return RemoteCharacterSelectionResult.Failure(validation.Reason);
        }

        CharacterSelectOption primary = CharacterSelectOptions.Find(primaryOptions, state.PrimaryCharacterId.Value);
        string objectNameOverride = LegacyCharacterVariantResolver.ResolveObjectName(primary);
        return RemoteCharacterSelectionResult.Success(primary.ToSelection(objectNameOverride));
    }
}

/// <summary>The outcome of resolving a remote attempt's local character: either a launchable
/// <see cref="CharacterSelection"/> or a clear reason it could not be resolved.</summary>
public readonly struct RemoteCharacterSelectionResult
{
    private RemoteCharacterSelectionResult(bool succeeded, CharacterSelection character, string error)
    {
        Succeeded = succeeded;
        Character = character;
        Error = error ?? string.Empty;
    }

    public bool Succeeded { get; }

    public CharacterSelection Character { get; }

    public string Error { get; }

    public static RemoteCharacterSelectionResult Success(CharacterSelection character)
    {
        return new RemoteCharacterSelectionResult(true, character, string.Empty);
    }

    public static RemoteCharacterSelectionResult Failure(string error)
    {
        return new RemoteCharacterSelectionResult(false, CharacterSelection.None, error);
    }
}
