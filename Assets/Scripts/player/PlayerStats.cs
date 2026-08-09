using UnityEngine;

/// <summary>
/// Compatibility component for old scenes that still serialize the removed PlayerStats script.
/// </summary>
public sealed class PlayerStats : MonoBehaviour
{
    [SerializeField] private float money;

    public static PlayerStats instance;

    public float Money
    {
        get => money;
        set => money = value;
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
