using System.Collections;
using UnityEngine;

/// <summary>
/// AUD-002: the CPU player's damage/knockdown reaction coroutines, extracted from
/// <see cref="AutoPlayerController"/> the same way <see cref="PlayerDamageReactions"/> was
/// extracted from <see cref="PlayerController"/> - the two controllers carried byte-identical
/// copies of this logic (the human path also has disintegrate/lightning/shrink reactions the CPU
/// path never implemented, so those stay human-only).
///
/// Deliberately a plain object, not a <c>MonoBehaviour</c> or a type shared with
/// <see cref="PlayerDamageReactions"/>: <see cref="PlayerController"/> and
/// <see cref="AutoPlayerController"/> are unrelated sibling classes with no common interface for
/// their Rigidbody/Animator/state fields, and introducing one is a larger decision than this
/// extraction - see the note on this in docs/architecture-audit.md AUD-002. Coroutines still run
/// under <see cref="AutoPlayerController"/>'s own <c>StartCoroutine</c> exactly as before.
///
/// No logic changed from the original methods - only where they live and how they reach the
/// controller's state.
/// </summary>
public sealed class AutoPlayerDamageReactions
{
    private readonly AutoPlayerController controller;

    public AutoPlayerDamageReactions(AutoPlayerController controller)
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

    public void PlayerAvoidKnockedDown()
    {
        controller.anim.Play("knockedDown");
        controller.AvoidedKnockDown = false;
        controller.Locked = false;
    }

    /// <summary>
    /// Added 2026-08-13, mirroring <see cref="PlayerDamageReactions.PlayerDisintegrated"/> exactly -
    /// CPU players were already gated out of movement while <c>disintegratedState</c> is active
    /// (<c>AutoPlayerController</c>'s movement check already excluded it) but had no trigger for it
    /// until now: no <c>Disintegrated</c> property, no coroutine, no collision-side detection.
    /// </summary>
    public IEnumerator PlayerDisintegrated()
    {
        controller.Locked = true;
        controller.rigidBody.constraints =
            RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        controller.anim.Play("disintegrated");
        yield return new WaitUntil(() => controller.currentState == controller.disintegratedState);
        yield return new WaitForSeconds(2);
        controller.PlayerHealth.IsDead = true;
        controller.rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }
}
