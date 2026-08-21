using UnityEngine;

public struct CombatReservation
{
    public CombatReservation(GameObject attacker, int slotId, Transform slotTransform)
    {
        Attacker = attacker;
        SlotId = slotId;
        SlotTransform = slotTransform;
    }

    public GameObject Attacker { get; private set; }
    public int SlotId { get; private set; }
    public Transform SlotTransform { get; private set; }
    public bool IsValid => Attacker != null && SlotId >= 0 && SlotTransform != null;
}
