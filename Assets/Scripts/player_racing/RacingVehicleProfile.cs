
using UnityEngine;

public class RacingVehicleProfile : MonoBehaviour
{
    [SerializeField] private int playerId;

    [SerializeField] private float jumpForce;
    [SerializeField] private float speed;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float accelerationPercent;

    [SerializeField] private float runSpeed;

    [SerializeField] private int luck;

    void Start()
    {
        if(accelerationPercent == 0)
        {
            accelerationPercent = 0.2f;
        }
        acceleration = 1 + (accelerationPercent/100);
    }

    public RacingVehicleProfile() { }

    public int PlayerId
    {
        get => playerId;
        set => playerId = value;
    }

    public float JumpForce
    {
        get => jumpForce;
        set => jumpForce = value;
    }

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    public float RunSpeed
    {
        get => runSpeed;
        set => runSpeed = value;
    }


    public int Luck
    {
        get => luck;
        set => luck = value;
    }
    public float Acceleration { get => acceleration; }
    public float MaxSpeed { get => maxSpeed; }
}
