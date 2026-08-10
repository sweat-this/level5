using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;

/// <summary>
/// The narrow boundary <see cref="StartManager"/> talks to for everything player-select related.
///
/// Composes the catalog adapter, the pure controller, the passive view and the session, and is
/// the only place that knows how to turn the current draft into a launchable
/// <see cref="PlayerRoster"/>. A plain C# class, not a <c>MonoBehaviour</c>: it needs no serialized
/// scene state of its own, so introducing it does not touch the start scene's serialization at
/// all - every widget it renders to is a reference <see cref="StartManager"/> already resolves
/// from <see cref="StartMenuUiObjects"/>.
/// </summary>
public sealed class PlayerSelectCoordinator
{
    private readonly PlayerSelectionState state = new PlayerSelectionState();
    private readonly PlayerSelectionController controller;

    private PlayerSelectCatalog catalog = new PlayerSelectCatalog(
        new List<CharacterSelectOption>(),
        new List<CharacterSelectOption>(),
        new Dictionary<int, CharacterSelectVisuals>(),
        string.Empty,
        CharacterSelectVisuals.Empty);

    private IReadOnlyList<CharacterProfile> primaryProfiles = new List<CharacterProfile>();
    private PlayerSelectView view;
    private GameModeDefinition currentMode;
    private MatchModifiers currentModifiers = MatchModifiers.Default;
    private int? focusedCpuSlot;

    // A revision counter instead of a formatted signature string: every mutation bumps it, and
    // RenderIfNeeded only advances lastRenderedRevision after RenderNow() returns without
    // throwing. That means a render failure retries on the next call instead of latching a stale
    // "already rendered" state forever, and the common case (nothing changed since last frame) is
    // one int comparison rather than a string allocation.
    private int revision;
    private int lastRenderedRevision = -1;

    public PlayerSelectCoordinator()
    {
        controller = new PlayerSelectionController(state);
    }

    public int ParticipantCount => controller.ParticipantCount;

    public CharacterSelectOption CurrentPrimary =>
        state.PrimaryCharacterId.HasValue ? catalog.FindPrimary(state.PrimaryCharacterId.Value) : null;

    public bool PrimaryIsLocked
    {
        get
        {
            CharacterSelectOption primary = CurrentPrimary;
            return primary != null && !primary.IsUnlocked;
        }
    }

    /// <summary>
    /// Projects the current loaded profiles into selectable catalogs, restores the remembered
    /// draft from session memory (falling back safely when a remembered id is no longer valid),
    /// and marks the view for a fresh render.
    /// </summary>
    public void Initialize(
        IReadOnlyList<CharacterProfile> primaryProfiles,
        IReadOnlyList<CharacterProfile> cpuProfiles,
        PlayerSelectView view)
    {
        this.primaryProfiles = primaryProfiles ?? new List<CharacterProfile>();
        this.view = view;
        catalog = PlayerSelectCatalogAdapter.Project(primaryProfiles, cpuProfiles);

        state.PrimaryCharacterId = PlayerSelectionSession.PrimaryCharacterId;
        controller.EnsurePrimarySelected(catalog.PrimaryOptions);
        PlayerSelectionSession.RememberPrimary(state.PrimaryCharacterId);

        for (int slot = 0; slot < PlayerSelectionState.CpuSlotCount; slot++)
        {
            controller.RestoreCpuSlot(slot, PlayerSelectionSession.GetCpu(slot), catalog.CpuOptions);
        }

        InvalidateRender();
    }

    /// <summary>
    /// Called whenever mode/modifier context that can affect character capability changes.
    /// Reconciles a required CPU opponent immediately and visibly, rather than waiting for launch.
    /// </summary>
    public void SetMatchContext(GameModeDefinition mode, MatchModifiers modifiers)
    {
        currentMode = mode;
        currentModifiers = modifiers ?? MatchModifiers.Default;
        controller.ReconcileRequiredCpu(currentMode, catalog.CpuOptions);
        InvalidateRender();
    }

    public void SelectNextPrimary()
    {
        CyclePrimary(1);
    }

    public void SelectPreviousPrimary()
    {
        CyclePrimary(-1);
    }

    public void SelectNextCpu(int slotIndex)
    {
        CycleCpu(slotIndex, 1);
    }

    public void SelectPreviousCpu(int slotIndex)
    {
        CycleCpu(slotIndex, -1);
    }

    public void FocusPrimary()
    {
        if (focusedCpuSlot == null)
        {
            return;
        }

        focusedCpuSlot = null;
        InvalidateRender();
    }

    public void FocusCpu(int slotIndex)
    {
        if (focusedCpuSlot == slotIndex)
        {
            return;
        }

        focusedCpuSlot = slotIndex;
        InvalidateRender();
    }

    /// <summary>
    /// Builds the launch roster from the current draft. Side-effect free except for the defensive
    /// required-CPU re-check, which only activates a slot that visible reconciliation should
    /// already have activated when the mode context changed - it never silently changes the
    /// primary or removes a choice.
    /// </summary>
    public bool TryBuildRoster(out PlayerRoster roster, out string error)
    {
        controller.ReconcileRequiredCpu(currentMode, catalog.CpuOptions);

        PlayerRosterBuildResult result = controller.TryBuildRoster(
            catalog.PrimaryOptions,
            catalog.CpuOptions,
            currentMode,
            LegacyCharacterVariantResolver.ResolveObjectName);

        roster = result.Roster;
        error = result.Error;
        return result.Succeeded;
    }

    /// <summary>
    /// Player-specific launch details that are not part of the match configuration: end-round
    /// portraits and the progression snapshot carried into non-arcade modes. Moved here from
    /// <c>StartManager</c> so it no longer indexes the selected profile at launch.
    /// </summary>
    public void ApplyLaunchSideEffects(MatchConfiguration configuration)
    {
        CharacterProfile player = FindProfile(state.PrimaryCharacterId);
        if (player == null || configuration == null)
        {
            return;
        }

        EndRoundData.currentRoundPlayerWinnerImage = player.winPortrait;
        EndRoundData.currentRoundPlayerLoserImage = player.losePortrait;

        string modeName = configuration.Mode.DisplayName.ToLowerInvariant();
        if (modeName.Contains("free") || !modeName.Contains("arcade"))
        {
            PlayerData.instance.CurrentExperience = player.Experience;
            PlayerData.instance.CurrentLevel = player.Level;
            PlayerData.instance.UpdatePointsAvailable = player.PointsAvailable;
            PlayerData.instance.UpdatePointsUsed = player.PointsUsed;
        }
    }

    /// <summary>Writes the current draft back to session memory, mirroring the old save-on-launch timing.</summary>
    public void PersistSessionPreferences()
    {
        PlayerSelectionSession.RememberPrimary(state.PrimaryCharacterId);
        for (int slot = 0; slot < PlayerSelectionState.CpuSlotCount; slot++)
        {
            PlayerSelectionSession.RememberCpu(slot, state.CpuSlots[slot].CharacterId);
        }
    }

    /// <summary>Renders only when selection, focus, or mode context has changed since the last render.</summary>
    public void RenderIfNeeded()
    {
        if (view == null || revision == lastRenderedRevision)
        {
            return;
        }

        RenderNow();
        lastRenderedRevision = revision;
    }

    private void CyclePrimary(int step)
    {
        controller.CyclePrimary(catalog.PrimaryOptions, currentMode, currentModifiers, step);
        PlayerSelectionSession.RememberPrimary(state.PrimaryCharacterId);
        InvalidateRender();
    }

    private void CycleCpu(int slotIndex, int step)
    {
        controller.CycleCpuSlot(catalog.CpuOptions, slotIndex, step);
        PlayerSelectionSession.RememberCpu(slotIndex, state.CpuSlots[slotIndex].CharacterId);
        InvalidateRender();
    }

    private void RenderNow()
    {
        CharacterSelectOption primary = CurrentPrimary;
        if (primary != null)
        {
            view.RenderPrimary(primary, catalog.VisualsFor(primary.CharacterId).Portrait, ParticipantCount);
        }

        for (int slot = 0; slot < PlayerSelectionState.CpuSlotCount; slot++)
        {
            PlayerSelectionSlot cpuSlot = state.CpuSlots[slot];
            if (cpuSlot.IsActive)
            {
                CharacterSelectOption option = catalog.FindCpu(cpuSlot.CharacterId.Value);
                view.RenderCpuSlot(
                    slot,
                    option != null ? option.DisplayName : string.Empty,
                    option != null ? catalog.VisualsFor(option.CharacterId).Portrait : null);
            }
            else
            {
                view.RenderCpuSlot(slot, catalog.CpuNoneDisplayName, catalog.CpuNoneVisuals.Portrait);
            }
        }

        CharacterSelectOption focused = null;
        if (focusedCpuSlot.HasValue)
        {
            PlayerSelectionSlot cpuSlot = state.CpuSlots[focusedCpuSlot.Value];
            focused = cpuSlot.IsActive ? catalog.FindCpu(cpuSlot.CharacterId.Value) : null;
        }

        view.RenderFocusedCpuStats(focused);
    }

    private void InvalidateRender()
    {
        revision++;
    }

    private CharacterProfile FindProfile(int? characterId)
    {
        if (!characterId.HasValue)
        {
            return null;
        }

        if (LoadedData.instance != null)
        {
            CharacterProfile loadedProfile = LoadedData.instance.getSelectedCharacterProfile(characterId.Value);
            if (loadedProfile != null)
            {
                return loadedProfile;
            }
        }

        return LoadedData.GetSelectedCharacterProfile(primaryProfiles, characterId.Value);
    }
}
