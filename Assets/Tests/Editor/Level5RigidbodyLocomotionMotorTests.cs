using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 4 Slice 72: <see cref="RigidbodyLocomotionMotor.SetPlanarVelocity"/> is the one
/// authoritative planar Rigidbody locomotion write now shared by <c>PlayerController</c> and
/// <c>AutoPlayerController</c>. Its entire contract is "write X/Z, leave Y alone" - these tests cover
/// exactly that against a real <see cref="Rigidbody"/> component, no scene required.
/// </summary>
public class Level5RigidbodyLocomotionMotorTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private Rigidbody SpawnRigidbody()
    {
        GameObject go = new GameObject("locomotion-motor-test-body");
        spawned.Add(go);
        return go.AddComponent<Rigidbody>();
    }

    [Test]
    public void SetPlanarVelocity_AppliesRequestedXVelocity()
    {
        Rigidbody body = SpawnRigidbody();

        RigidbodyLocomotionMotor.SetPlanarVelocity(body, 5f, 0f);

        Assert.That(body.linearVelocity.x, Is.EqualTo(5f));
    }

    [Test]
    public void SetPlanarVelocity_AppliesRequestedZVelocity()
    {
        Rigidbody body = SpawnRigidbody();

        RigidbodyLocomotionMotor.SetPlanarVelocity(body, 0f, -3f);

        Assert.That(body.linearVelocity.z, Is.EqualTo(-3f));
    }

    [Test]
    public void SetPlanarVelocity_PreservesExistingYVelocity()
    {
        Rigidbody body = SpawnRigidbody();
        body.linearVelocity = new Vector3(0f, 12f, 0f);

        RigidbodyLocomotionMotor.SetPlanarVelocity(body, 4f, 2f);

        Assert.That(body.linearVelocity.y, Is.EqualTo(12f),
            "a planar locomotion write must never touch Y - that's gravity/jump/impulse territory");
    }

    [Test]
    public void SetPlanarVelocity_ZeroRequestedPlanarVelocity_PreservesY()
    {
        Rigidbody body = SpawnRigidbody();
        body.linearVelocity = new Vector3(7f, -9.8f, 7f);

        RigidbodyLocomotionMotor.SetPlanarVelocity(body, 0f, 0f);

        Vector3 result = body.linearVelocity;
        Assert.That(result.x, Is.EqualTo(0f));
        Assert.That(result.z, Is.EqualTo(0f));
        Assert.That(result.y, Is.EqualTo(-9.8f),
            "clearing planar velocity (e.g. on arrival) must not clear a falling body's Y velocity");
    }
}
