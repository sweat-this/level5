using UnityEngine;

#if UNITY_EDITOR
public class BasketballTestStats : MonoBehaviour
{
    public bool two, three, four;
    public bool made, miss, attempt;

    public float distance;
    public float releaseVelocity;
    public Vector3 localVelocity;
    public Vector3 globalVelocity;
    public float accuracyModifier;
    public float accuracy;

    public bool Two
    {
        get => two;
        set => two = value;
    }

    public bool Three
    {
        get => three;
        set => three = value;
    }

    public bool Four
    {
        get => four;
        set => four = value;
    }

    public bool Made
    {
        get => made;
        set => made = value;
    }

    public bool Miss
    {
        get => miss;
        set => miss = value;
    }
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    public float ReleaseVelocity
    {
        get => releaseVelocity;
        set => releaseVelocity = value;
    }

    public Vector3 LocalVelocity
    {
        get => localVelocity;
        set => localVelocity = value;
    }

    public Vector3 GlobalVelocity
    {
        get => globalVelocity;
        set => globalVelocity = value;
    }

    public float AccuracyModifier
    {
        get => accuracyModifier;
        set => accuracyModifier = value;
    }

    public float Accuracy
    {
        get => accuracy;
        set => accuracy = value;
    }
}
#endif
