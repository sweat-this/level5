using System;
using System.Collections.Generic;
using Level5.Core.Match;

namespace Level5.Core.PlayerSelection
{
    /// <summary>
    /// Pure orchestration over a <see cref="PlayerSelectionState"/> and the catalogs it draws from.
    ///
    /// Cycling, CPU slot activation, participant counting, required-opponent reconciliation and
    /// launch validation all live here so they are testable without a scene and so no second copy
    /// of the fighter/shooter rule exists outside <see cref="GameModeCompatibility"/>. No Unity
    /// API, no <c>GameOptions</c>, no scene operations - the adapter/coordinator layer owns those.
    /// </summary>
    public sealed class PlayerSelectionController
    {
        private readonly PlayerSelectionState state;

        public PlayerSelectionController(PlayerSelectionState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public PlayerSelectionState State => state;

        public int ParticipantCount => state.ParticipantCount;

        /// <summary>
        /// Picks the first catalog entry as the primary when nothing is selected yet, or the
        /// remembered id is no longer in the catalog. Used to resolve session memory into a
        /// concrete default rather than leaving the primary unset.
        /// </summary>
        public void EnsurePrimarySelected(IReadOnlyList<CharacterSelectOption> catalog)
        {
            if (catalog == null || catalog.Count == 0)
            {
                return;
            }

            if (state.PrimaryCharacterId.HasValue && CharacterSelectOptions.IndexOf(catalog, state.PrimaryCharacterId.Value) >= 0)
            {
                return;
            }

            state.PrimaryCharacterId = catalog[0].CharacterId;
        }

        /// <summary>
        /// Moves the primary selection by one, skipping characters that cannot play the way the
        /// current mode/modifiers require. Locked characters are not skipped - they remain
        /// browseable, only unplayable-by-capability ones are.
        /// </summary>
        public void CyclePrimary(
            IReadOnlyList<CharacterSelectOption> catalog,
            GameModeDefinition mode,
            MatchModifiers modifiers,
            int step)
        {
            if (catalog == null || catalog.Count == 0 || step == 0)
            {
                return;
            }

            int currentIndex = state.PrimaryCharacterId.HasValue ? CharacterSelectOptions.IndexOf(catalog, state.PrimaryCharacterId.Value) : -1;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            for (int offset = 1; offset <= catalog.Count; offset++)
            {
                int index = IndexMath.Wrap(currentIndex + (offset * step), catalog.Count);
                CharacterSelectOption candidate = catalog[index];
                if (CanCharacterPlay(mode, modifiers, candidate))
                {
                    state.PrimaryCharacterId = candidate.CharacterId;
                    return;
                }
            }

            // Nothing qualifies. Step once anyway so the control still responds; launch validation
            // is what refuses an unplayable combination.
            state.PrimaryCharacterId = catalog[IndexMath.Wrap(currentIndex + step, catalog.Count)].CharacterId;
        }

        /// <summary>
        /// Moves one CPU draft slot by one, cycling through "no CPU" and every catalog entry as a
        /// single ring: none -&gt; option 0 -&gt; option 1 -&gt; ... -&gt; none.
        /// </summary>
        public void CycleCpuSlot(IReadOnlyList<CharacterSelectOption> cpuCatalog, int slotIndex, int step)
        {
            ValidateSlotIndex(slotIndex);
            if (cpuCatalog == null || cpuCatalog.Count == 0 || step == 0)
            {
                return;
            }

            PlayerSelectionSlot current = state.GetCpuSlot(slotIndex);
            int currentPosition = current.IsActive ? CharacterSelectOptions.IndexOf(cpuCatalog, current.CharacterId.Value) + 1 : 0;
            int totalPositions = cpuCatalog.Count + 1;
            int newPosition = IndexMath.Wrap(currentPosition + step, totalPositions);

            state.SetCpuSlot(
                slotIndex,
                newPosition == 0
                    ? PlayerSelectionSlot.Inactive(PlayerControlType.Cpu)
                    : PlayerSelectionSlot.Active(PlayerControlType.Cpu, cpuCatalog[newPosition - 1].CharacterId));
        }

        public void ActivateCpuSlot(int slotIndex, IReadOnlyList<CharacterSelectOption> cpuCatalog)
        {
            ValidateSlotIndex(slotIndex);
            if (cpuCatalog == null || cpuCatalog.Count == 0)
            {
                return;
            }

            state.SetCpuSlot(slotIndex, PlayerSelectionSlot.Active(PlayerControlType.Cpu, cpuCatalog[0].CharacterId));
        }

        public void DeactivateCpuSlot(int slotIndex)
        {
            ValidateSlotIndex(slotIndex);
            state.SetCpuSlot(slotIndex, PlayerSelectionSlot.Inactive(PlayerControlType.Cpu));
        }

        /// <summary>
        /// Restores a CPU slot to a remembered character id, or leaves it inactive if the id is
        /// absent or no longer in the catalog. Used to resolve session memory back into the draft
        /// without assuming the remembered id is still valid.
        /// </summary>
        public void RestoreCpuSlot(int slotIndex, int? characterId, IReadOnlyList<CharacterSelectOption> cpuCatalog)
        {
            ValidateSlotIndex(slotIndex);
            state.SetCpuSlot(
                slotIndex,
                characterId.HasValue && CharacterSelectOptions.Find(cpuCatalog, characterId.Value) != null
                    ? PlayerSelectionSlot.Active(PlayerControlType.Cpu, characterId.Value)
                    : PlayerSelectionSlot.Inactive(PlayerControlType.Cpu));
        }

        /// <summary>
        /// When the mode requires a CPU opponent and none is active, activates the first real CPU
        /// option as slot 0 and reports that it changed the draft. Called explicitly by the
        /// coordinator when the mode context changes and again as a defensive pre-launch check -
        /// never from inside roster construction.
        /// </summary>
        public bool ReconcileRequiredCpu(GameModeDefinition mode, IReadOnlyList<CharacterSelectOption> cpuCatalog)
        {
            if (mode == null || !mode.RequiresCpuOpponent || mode.AddsImplicitDefender)
            {
                return false;
            }

            if (state.ParticipantCount > 1 || cpuCatalog == null || cpuCatalog.Count == 0)
            {
                return false;
            }

            state.SetCpuSlot(0, PlayerSelectionSlot.Active(PlayerControlType.Cpu, cpuCatalog[0].CharacterId));
            return true;
        }

        /// <summary>Whether the primary selection can be launched: it must resolve and be unlocked.</summary>
        public PlayerSelectValidation ValidateLaunch(IReadOnlyList<CharacterSelectOption> catalog)
        {
            CharacterSelectOption primary = FindPrimary(catalog);
            if (primary == null)
            {
                return PlayerSelectValidation.Failure("no primary character is selected");
            }

            if (!primary.IsUnlocked)
            {
                return PlayerSelectValidation.Failure($"{primary} is locked and cannot be played yet");
            }

            return PlayerSelectValidation.Success();
        }

        /// <summary>
        /// Converts the current draft into a <see cref="PlayerRoster"/>. Side-effect free: it never
        /// changes the selection, including when a mode requires a CPU opponent - that
        /// reconciliation is <see cref="ReconcileRequiredCpu"/>, called separately and visibly.
        ///
        /// <paramref name="resolveObjectName"/> lets the caller substitute a legacy runtime variant
        /// (Wizard of Boat) at conversion time without this method knowing about randomness.
        /// </summary>
        public PlayerRosterBuildResult TryBuildRoster(
            IReadOnlyList<CharacterSelectOption> catalog,
            IReadOnlyList<CharacterSelectOption> cpuCatalog,
            GameModeDefinition mode,
            Func<CharacterSelectOption, string> resolveObjectName = null)
        {
            PlayerSelectValidation validation = ValidateLaunch(catalog);
            if (!validation.IsValid)
            {
                return PlayerRosterBuildResult.Failure(validation.Reason);
            }

            CharacterSelectOption primary = FindPrimary(catalog);
            string objectName = resolveObjectName != null ? resolveObjectName(primary) : null;

            List<PlayerRosterEntry> entries = new List<PlayerRosterEntry>
            {
                PlayerRosterEntry.LocalHuman(primary.ToSelection(objectName))
            };

            // Lockdown and other implicit-defender modes bring their own defender and ignore the
            // authored CPU draft entirely, exactly as the legacy launch path did.
            if (mode == null || !mode.AddsImplicitDefender)
            {
                foreach (PlayerSelectionSlot slot in state.CpuSlots)
                {
                    if (!slot.IsActive)
                    {
                        continue;
                    }

                    CharacterSelectOption cpuOption = CharacterSelectOptions.Find(cpuCatalog, slot.CharacterId.Value);
                    if (cpuOption != null)
                    {
                        entries.Add(PlayerRosterEntry.Cpu(cpuOption.ToSelection()));
                    }
                }
            }

            return PlayerRosterBuildResult.Success(PlayerRoster.Build(entries));
        }

        /// <summary>
        /// The single character-capability query player select uses. Delegates to
        /// <see cref="GameModeCompatibility"/> so cycling can never drift from full roster
        /// validation - there is exactly one fighter/shooter rule in the codebase.
        /// </summary>
        public bool CanCharacterPlay(GameModeDefinition mode, MatchModifiers modifiers, CharacterSelectOption option)
        {
            return option != null && GameModeCompatibility.CharacterCanPlay(mode, modifiers, option.ToSelection());
        }

        private CharacterSelectOption FindPrimary(IReadOnlyList<CharacterSelectOption> catalog)
        {
            return state.PrimaryCharacterId.HasValue ? CharacterSelectOptions.Find(catalog, state.PrimaryCharacterId.Value) : null;
        }

        private static void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= PlayerSelectionState.CpuSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
        }

    }

    /// <summary>Whether the current draft can launch, and why not if it cannot.</summary>
    public readonly struct PlayerSelectValidation
    {
        private PlayerSelectValidation(bool isValid, string reason)
        {
            IsValid = isValid;
            Reason = reason ?? string.Empty;
        }

        public bool IsValid { get; }

        public string Reason { get; }

        public static PlayerSelectValidation Success()
        {
            return new PlayerSelectValidation(true, string.Empty);
        }

        public static PlayerSelectValidation Failure(string reason)
        {
            return new PlayerSelectValidation(false, reason);
        }
    }

    /// <summary>The outcome of converting a draft to a roster: either a roster or a reason it could not.</summary>
    public readonly struct PlayerRosterBuildResult
    {
        private PlayerRosterBuildResult(PlayerRoster roster, string error)
        {
            Roster = roster;
            Error = error ?? string.Empty;
        }

        public PlayerRoster Roster { get; }

        public string Error { get; }

        public bool Succeeded => Roster != null;

        public static PlayerRosterBuildResult Success(PlayerRoster roster)
        {
            return new PlayerRosterBuildResult(roster, string.Empty);
        }

        public static PlayerRosterBuildResult Failure(string error)
        {
            return new PlayerRosterBuildResult(null, error);
        }
    }
}
