using System.Collections;
using UnityEngine;
using Assets.Scripts.Utility;
using Level5.Core.Match;

public class BehaviorNpcCritical : MonoBehaviour
{
    Animator anim;
    AudioSource audioSource;
    //bool shotMade;
    // CHR-3: `percentChanceOfCritical` and the rollForCritical/rollForPhotoChance pair that read it
    // are gone. Nothing called them, and the field was authored as 0 on every cheerleader prefab -
    // an inspector-visible tuning value that looked live and controlled nothing. The flourish is
    // driven by direct playAnimationCriticalSuccesful() calls from BasketBall, BasketBallAuto and
    // EnemyController, which decide for themselves what deserves one.
    public Animator animOnCamera;
    //PlayerController playerState;

    //private string npcName;
    [SerializeField]
    GameObject spriteObject;
    public static BehaviorNpcCritical instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();
        // AUD-053: names the missing object instead of throwing partway through Start
        animOnCamera = SceneObjects.Find<Animator>("camera_flash", this);
        //npcName = gameObject.transform.root.name;
        spriteObject = transform.gameObject;
        if (MatchRuntime.CustomCamera)
        {
            spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }


    public void playAnimationCriticalSuccesful()
    {
        playCriticalSuccessfulAnim();
    }

    IEnumerator wait(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        //Debug.Log("jessica take photo");
        anim.Play("critical_success");
        audioSource.PlayOneShot(SFXBB.instance.cameraFlash);
    }

    private void playCriticalSuccessfulAnim()
    {
        anim.Play("critical_success");
    }

    /// <summary>
    /// Animation event on jessica_critical_success.anim. Not called from C#.
    /// </summary>
    private void playAnimationCameraFlash()
    {
        if (animOnCamera == null)
        {
            return;
        }

        animOnCamera.Play("camera_flash");
    }
}
