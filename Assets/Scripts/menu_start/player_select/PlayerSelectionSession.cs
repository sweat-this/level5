using System;
using Level5.Core.PlayerSelection;

/// <summary>
/// Remembers the player-selection draft's stable character ids across a menu scene transition -
/// the start menu to the progression screen and back.
///
/// Replaces <c>GameOptions.playerSelectedIndex</c>/<c>cpu1SelectedIndex</c>/<c>cpu2SelectedIndex</c>/
/// <c>cpu3SelectedIndex</c>. Those were catalog indices, meaningful only against the exact list
/// that produced them; this remembers identity instead, which stays meaningful even if the
/// catalog order or length changes between visits.
///
/// Session memory only, matching what those fields already were: process-wide static state that
/// does not survive an application restart. Encapsulated behind methods rather than public
/// mutable fields so nothing outside this class can put it in a partially-updated state.
/// </summary>
public static class PlayerSelectionSession
{
    private static int? primaryCharacterId;
    private static readonly int?[] cpuCharacterIds = new int?[PlayerSelectionState.CpuSlotCount];

    public static int? PrimaryCharacterId => primaryCharacterId;

    public static void RememberPrimary(int? characterId)
    {
        primaryCharacterId = characterId;
    }

    public static int? GetCpu(int slotIndex)
    {
        return cpuCharacterIds[ValidateSlot(slotIndex)];
    }

    public static void RememberCpu(int slotIndex, int? characterId)
    {
        cpuCharacterIds[ValidateSlot(slotIndex)] = characterId;
    }

    public static void Clear()
    {
        primaryCharacterId = null;
        Array.Clear(cpuCharacterIds, 0, cpuCharacterIds.Length);
    }

    private static int ValidateSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= PlayerSelectionState.CpuSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        return slotIndex;
    }
}
