using UnityEngine;

public class PlayerSwapAttack : MonoBehaviour
{
    public AnimationClip[] closeAttacks;
    [SerializeField]
    protected Animator anim;
    [SerializeField]
    AnimatorOverrideController animatorOverrideController;

    public AnimationClip longRangeAttack;
    protected int index;

    public AnimatorOverrideController AnimatorOverrideController { get => animatorOverrideController; }

    public void Start()
    {
        anim = GetComponentInChildren<Animator>();
        animatorOverrideController = anim.runtimeAnimatorController as AnimatorOverrideController;
        index = 0;
    }

    public void setCloseAttack()
    {
        if (animatorOverrideController == null || anim == null)
        {
            return;
        }

        if (closeAttacks != null && closeAttacks.Length > 1)
        {
            int randomIndex = Random.Range(0, closeAttacks.Length);
            animatorOverrideController["attack"] = closeAttacks[randomIndex];
            anim.runtimeAnimatorController = animatorOverrideController;
        }
        else if (closeAttacks != null && closeAttacks.Length == 1)
        {
            animatorOverrideController["attack"] = closeAttacks[0];
            anim.runtimeAnimatorController = animatorOverrideController;
        }
        else
        {
            anim.runtimeAnimatorController = animatorOverrideController;
        }
    }

    public void setLongRangeAttack()
    {
        if (animatorOverrideController == null || longRangeAttack == null)
        {
            return;
        }

        animatorOverrideController["attack"] = longRangeAttack;
        anim.runtimeAnimatorController = animatorOverrideController;
    }
}
