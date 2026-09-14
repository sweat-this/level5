
using UnityEngine;

public class CheerleaderProfile : MonoBehaviour
{
    /// <summary>
    /// Compatibility alias for <see cref="Level5.Core.Match.CheerleaderSelection.LegacyNoneObjectName"/>
    /// (AUD-012 Phase 2b Slice 58). Existing callers keep reading this exact constant; the neutral
    /// definition now lives in the <c>Level5.Core.Match</c> cheerleader-selection contract so that
    /// runtime code outside <c>Level5.MenuStart</c> (e.g. the eventual <c>Level5.Match</c> home for
    /// <c>SpawnCoordinator</c>) can recognise the sentinel without a <c>Level5.Match -&gt;
    /// Level5.MenuStart</c> reference.
    /// </summary>
    public const string NoneObjectName = Level5.Core.Match.CheerleaderSelection.LegacyNoneObjectName;

    [SerializeField] private string cheerleaderDisplayName;
    [SerializeField] private string cheerleaderObjectName;
    [SerializeField] private Sprite cheerleaderPortrait;
    [SerializeField] private GameObject cheerleaderProfileObject;
    [SerializeField] private bool isLocked;
    [SerializeField] private int cheerleaderId;
    [SerializeField] private string unlockCharacterText;

    [SerializeField] public int bonus3Accuracy;
    [SerializeField] public int bonus4Accuracy;
    [SerializeField] public int bonus7Accuracy;
    [SerializeField] public int bonusLuck;
    [SerializeField] public int bonusRelease;
    [SerializeField] public int bonusRange;
    [SerializeField] public int bonusSpeed;
    [SerializeField] public int bonusClutch;
    [SerializeField] public int bonusAttack;
    [SerializeField] public int bonusHealth;
    [SerializeField] public int bonusDefense;

    public int CheerleaderId { get => cheerleaderId; set => cheerleaderId = value; }
    public string CheerleaderDisplayName { get => cheerleaderDisplayName; set => cheerleaderDisplayName = value; }
    public string CheerleaderObjectName { get => cheerleaderObjectName; set => cheerleaderObjectName = value; }
    public bool IsLocked { get => isLocked; set => isLocked = value; }
    public string UnlockCharacterText { get => unlockCharacterText; set => unlockCharacterText = value; }
    public Sprite CheerleaderPortrait { get => cheerleaderPortrait; set => cheerleaderPortrait = value; }
}
