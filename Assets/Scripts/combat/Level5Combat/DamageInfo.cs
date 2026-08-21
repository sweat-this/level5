using UnityEngine;

public struct DamageInfo
{
    public DamageInfo(float amount)
        : this(amount, null, Vector3.zero, Vector3.zero, string.Empty)
    {
    }

    public DamageInfo(float amount, GameObject source, Vector3 point, Vector3 force, string damageType)
    {
        Amount = amount;
        Source = source;
        Point = point;
        Force = force;
        DamageType = damageType;
    }

    public float Amount { get; private set; }
    public GameObject Source { get; private set; }
    public Vector3 Point { get; private set; }
    public Vector3 Force { get; private set; }
    public string DamageType { get; private set; }
}
