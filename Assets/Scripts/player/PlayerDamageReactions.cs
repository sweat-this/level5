using System.Collections;
using UnityEngine;

/// <summary>
/// AUD-002: the player's damage/knockdown/lightning/shrink reaction coroutines, extracted from
/// <see cref="PlayerController"/> so this one cohesive concern - freeze the rigidbody, play a
/// reaction animation, wait for it, restore state - can be read without wading through movement,
/// input, and basketball code in the same 1000+ line file.
///
/// Deliberately a plain object, not a <c>MonoBehaviour</c>: the coroutines still run under
/// <see cref="PlayerController"/>'s own <c>StartCoroutine</c> exactly as before (a plain method can
/// return an <c>IEnumerator</c> for any <c>MonoBehaviour</c> to drive), so no prefab, component
/// lifecycle, or <c>GetComponent</c> wiring changes. <see cref="PlayerController"/> keeps every
/// flag this reads and writes - <c>TakeDamage</c>, <c>KnockedDown</c>, <c>Locked</c>,
/// <c>AvoidedKnockDown</c>, <c>isShrunk</c>, <c>currentState</c> - because its own
/// <c>Update()</c>/<c>FixedUpdate()</c> gate movement on the same flags every frame. Splitting that
/// coupling is a larger, separate decision than this slice; see AUD-002/AUD-007 in
/// docs/architecture-audit.md.
///
/// No logic changed from the original methods - only where they live and how they reach the
/// controller's state.
/// </summary>
public sealed class PlayerDamageReactions
{
    private readonly PlayerController controller;

    public PlayerDamageReactions(PlayerController controller)
    {
        this.controller = controller;
    }

    public IEnumerator PlayerTakeDamage(float takeDamageTime)
    {
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        controller.anim.SetBool("takeDamage", true);
        controller.anim.Play("takeDamage");

        float startTime = Time.time;
        float endTime = startTime + takeDamageTime;
        yield return new WaitUntil(() => Time.time > endTime);
        controller.anim.SetBool("takeDamage", false);
        yield return new WaitUntil(() => controller.currentState != controller.takeDamageState);

        controller.TakeDamage = false;
        controller.KnockedDown = false;
        controller.Locked = false;

        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator PlayerFreezeForXSeconds(float time)
    {
        Debug.Log("freeze player");
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        controller.anim.SetBool("takeDamage", true);
        controller.anim.Play("takeDamage");

        float startTime = Time.time;
        float endTime = startTime + time;
        yield return new WaitUntil(() => Time.time > endTime);
        controller.anim.SetBool("takeDamage", false);
        yield return new WaitUntil(() => controller.currentState != controller.takeDamageState);

        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator PlayerKnockedDown(float knockDownTime)
    {
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        controller.anim.SetBool("knockedDown", true);
        controller.anim.Play("knockedDown");

        float startTime = Time.time;
        float endTime = startTime + knockDownTime;
        yield return new WaitUntil(() => Time.time > endTime);
        controller.anim.SetBool("knockedDown", false);
        yield return new WaitUntil(() => controller.currentState != controller.knockedDownState);

        controller.KnockedDown = false;
        controller.TakeDamage = false;
        controller.Locked = false;

        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator PlayerDisintegrated()
    {
        controller.Locked = true;
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        controller.anim.Play("disintegrated");
        yield return new WaitUntil(() => controller.currentState == controller.disintegratedState);
        yield return new WaitForSeconds(2);
        controller.playerHealth.IsDead = true;
        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator PlayerStruckByLightning()
    {
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        controller.anim.Play("lightning");
        yield return new WaitUntil(() => controller.currentState == controller.lightningState);
        yield return new WaitUntil(() => controller.currentState != controller.lightningState);
        controller.KnockedDown = true;
        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator ShrinkPlayer()
    {
        controller.isShrunk = true;
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        controller.anim.Play("lightning");
        yield return new WaitUntil(() => controller.currentState == controller.lightningState);
        yield return new WaitUntil(() => controller.currentState != controller.lightningState);
        controller.KnockedDown = true;
        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;

        Transform transform = controller.transform;
        Vector3 originalScale = transform.localScale;
        Vector3 newScale = transform.localScale / 2;

        // AUD-054: the restore below used the literal 50 rather than the value captured here, so a
        // camera at any other FOV was permanently retuned by shrinking once.
        Camera shrinkCamera = CameraManager.instance != null && CameraManager.instance.Cameras != null
            && CameraManager.instance.Cameras.Length > 0 && CameraManager.instance.Cameras[0] != null
            ? CameraManager.instance.Cameras[0].GetComponent<Camera>()
            : null;
        float camFOV = shrinkCamera != null ? shrinkCamera.fieldOfView : 0f;

        controller.gameObject.transform.localScale = newScale;
        if (shrinkCamera != null)
        {
            shrinkCamera.fieldOfView = camFOV / 2;
        }

        yield return new WaitForSeconds(10);

        controller.gameObject.transform.localScale = originalScale;
        controller.FacingRight = transform.localScale.x > 0 ? true : false;
        if (shrinkCamera != null)
        {
            shrinkCamera.fieldOfView = camFOV;
        }
        controller.isShrunk = false;
    }

    public void PlayerAvoidKnockedDown()
    {
        controller.anim.Play("knockedDown");
        controller.AvoidedKnockDown = false;
        controller.Locked = false;
    }
}
