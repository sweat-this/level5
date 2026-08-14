using UnityEngine;

public class CheerleaderSwapAnimation : MonoBehaviour
{

    public AnimationClip[] animations;
    [SerializeField]
    protected Animator anim;
    [SerializeField]
    AnimatorOverrideController animatorOverrideController;

    protected int index;
    // CHR-2: the `swapped` flag that used to sit here is gone. Both animation setters reset it to
    // false as their last statement, so the `!swapped` guard it was supposed to provide could
    // never hold - the GetKeyDown edge is what makes the dev toggle fire once per press.
    [SerializeField]
    bool originalAnimations;

    // The clip names in the base controller every cheerleader override derives from
    // (npc_critical_success.controller). They are jessica_* for all of them, not only Jessica.
    private const string CriticalSuccessClip = "jessica_critical_success";
    private const string IdleClip = "jessica_idle";

    public void Start()
    {
        //anim = GameLevelManager.instance.Anim;
        anim = GetComponent<Animator>();

        // CHR-2: this assigned `anim.runtimeAnimatorController as AnimatorOverrideController`
        // directly, which is the project asset rather than a copy - so writing clips into it below
        // edited the checked-in .overrideController from play mode. It was invisible only because
        // the clips written happened to equal what the asset already held; reordering `animations`
        // would have silently modified a shared asset used by other prefabs. Instantiating gives
        // this instance its own controller to mutate.
        AnimatorOverrideController sourceController = anim.runtimeAnimatorController as AnimatorOverrideController;
        if (sourceController == null)
        {
            Debug.LogError(
                $"CheerleaderSwapAnimation on {name} needs an AnimatorOverrideController; animation swapping is disabled.",
                this);
            enabled = false;
            return;
        }

        if (animations == null || animations.Length < 4)
        {
            Debug.LogError(
                $"CheerleaderSwapAnimation on {name} needs four animation clips; animation swapping is disabled.",
                this);
            enabled = false;
            return;
        }

        animatorOverrideController = Instantiate(sourceController);
        //index = 0;
        SetOriginalAnimation(anim);
    }

    public void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // CHR-2: `swapped` was reset to false inside both setters below, so it could never gate
        // anything - it is the GetKeyDown edge that makes this fire once per press.
        if (GameLevelManager.instance != null
            && GameLevelManager.instance.Controls.Other.change.enabled
            && Input.GetKeyDown(KeyCode.Alpha9))
        {
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
        ApplyClips(animator, animations[3], animations[2]);
        originalAnimations = false;
    }

    public void SetOriginalAnimation(Animator animator)
    {
        ApplyClips(animator, animations[1], animations[0]);
        originalAnimations = true;
    }

    private void ApplyClips(Animator animator, AnimationClip criticalSuccess, AnimationClip idle)
    {
        if (animator == null || animatorOverrideController == null)
        {
            return;
        }

        // has to be the original controller animation names, not just overrides.
        animatorOverrideController[CriticalSuccessClip] = criticalSuccess;
        animatorOverrideController[IdleClip] = idle;
        animator.runtimeAnimatorController = animatorOverrideController;
    }
}
