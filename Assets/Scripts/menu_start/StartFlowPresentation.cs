using UnityEngine;

/// <summary>Presentation memory for one pass through the start flow, shared by tips and CPU select.</summary>
public static class StartFlowPresentation
{
    public static bool TipsShown { get; private set; }

    public static void MarkTipsShown() => TipsShown = true;

    // A successful ordinary launch opens the next pass. Editor play sessions start fresh too,
    // including when domain reload is disabled. Scene transitions within a pass do not reset it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetForNextPass() => TipsShown = false;
}
