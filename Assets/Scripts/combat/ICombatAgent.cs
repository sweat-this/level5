using UnityEngine;

public interface ICombatAgent
{
    GameObject CombatObject { get; }
    Transform CombatTransform { get; }
    bool CanAct { get; }
}
