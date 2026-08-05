using UnityEngine;

public class CheerleaderSwapAnimation : MonoBehaviour
{

    public AnimationClip[] animations;
    [SerializeField]
    protected Animator anim;
    [SerializeField]
    AnimatorOverrideController animatorOverrideController;

    protected int index;
    [SerializeField]
    bool swapped = false;
    [SerializeField]
    bool originalAnimations;

    public void Start()
    {
        //anim = GameLevelManager.instance.Anim;
        anim = GetComponent<Animator>();
        animatorOverrideController = anim.runtimeAnimatorController as AnimatorOverrideController;
        //index = 0;
        SetOriginalAnimation(anim);
    }

    public void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GameLevelManager.instance.Controls.Other.change.enabled
            && Input.GetKeyDown(KeyCode.Alpha9)
            && !swapped)
        {
            swapped = true;
            if (originalAnimations)
            {
                SetCurrentAnimation(anim);
            }
            else
            {
                SetOriginalAnimation(anim);
            }
        }
#endif
    }

    public void SetCurrentAnimation(Animator animator)
    {
        // has to be the original controller animation names, not just overrides.
        // need to create a default animator and override for jessica
        animatorOverrideController["jessica_critical_success"] = animations[3];
        animatorOverrideController["jessica_idle"] = animations[2];
        animator.runtimeAnimatorController = animatorOverrideController;
        originalAnimations = false;
        swapped = false;
    }

    public void SetOriginalAnimation(Animator animator)
    {
        // has to be the original controller animation names, not just overrides.
        // need to create a default animator and override for jessica
        animatorOverrideController["jessica_critical_success"] = animations[1];
        animatorOverrideController["jessica_idle"] = animations[0];
        animator.runtimeAnimatorController = animatorOverrideController;
        originalAnimations = true;
        swapped = false;
    }
}
