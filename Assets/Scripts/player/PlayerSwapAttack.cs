using UnityEngine;
using Random = System.Random;

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
        //anim = GameLevelManager.instance.Anim;
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

        // if enemy has more than one close attack, chose random one
        if (closeAttacks != null && closeAttacks.Length > 1)
        {
            Random random = new Random();
            int randomIndex = random.Next(0, closeAttacks.Length);
            animatorOverrideController["attack"] = closeAttacks[randomIndex];
            anim.runtimeAnimatorController = animatorOverrideController;
        }
        // else use default
        else if (closeAttacks != null && closeAttacks.Length == 1)
        {
            animatorOverrideController["attack"] = closeAttacks[0];
            anim.runtimeAnimatorController = animatorOverrideController;
        }
        else
        {
            if (animatorOverrideController != null)
            {
                anim.runtimeAnimatorController = animatorOverrideController;
            }
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
