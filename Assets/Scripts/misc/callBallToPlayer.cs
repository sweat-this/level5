using Level5.Core.Match;
﻿using UnityEngine;

public class CallBallToPlayer : MonoBehaviour
{
    [SerializeField]
    internal float pullSpeed;
    [SerializeField]
    private Vector3 pullDirection;
    [SerializeField]
    private BasketBallState _basketBallState;
    [SerializeField]
    private bool locked;
    [SerializeField]
    public bool CallEnabled = true;

    public bool Locked { get => locked; set => locked = value; }

    private void Start()
    {
        Locked = false;
        pullSpeed = 2.3f;

        if (MatchRuntime.Rules.Hardcore && MatchRuntime.Rules.EnemiesOnly)
        {
            CallEnabled = false;
            if (MatchRuntime.Rules.IsThreePointContest || MatchRuntime.Rules.IsFourPointContest || MatchRuntime.Rules.IsSevenPointContest || MatchRuntime.Rules.IsAllPointContest)
            {
                CallEnabled = true;
            }
        }
    }


    public void pullBallToPlayer(GameObject basketBall)
    {
        //if (!MatchRuntime.Rules.Hardcore)
        //{
            Rigidbody basketballRigidBody = basketBall.GetComponent<Rigidbody>();

            Vector3 tempDirection = basketballRigidBody.transform.position;
            pullDirection = transform.position - tempDirection;
            basketballRigidBody.linearVelocity = pullDirection * pullSpeed;
        //}
    }
    public void pullBallToPlayerAuto(GameObject basketBallAuto)
    {
        //if (!MatchRuntime.Rules.Hardcore)
        //{
            Rigidbody basketballRigidBody = basketBallAuto.GetComponent<Rigidbody>();

            Vector3 tempDirection = basketBallAuto.transform.position;
            pullDirection = transform.position - tempDirection;
            basketballRigidBody.linearVelocity = pullDirection * pullSpeed;
        //}
    }
}
