using System.Collections;
using UnityEngine;
using Assets.Scripts.Utility;
using Level5.Core.Match;

public class BehaviorNpcCritical : MonoBehaviour
{
    Animator anim;
    AudioSource audioSource;
    //bool shotMade;
    public float percentChanceOfCritical;
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


    public void rollForCritical()
    {
        if (rollForPhotoChance(percentChanceOfCritical))// && playerState.playerDistanceFromRim < 10)
        {
            playCriticalSuccessfulAnim();
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


    public bool rollForPhotoChance(float maxPercent)
    {
        return UtilityFunctions.RollPercent(maxPercent);
    }
    private void playAnimationCameraFlash()
    {
        if (animOnCamera == null)
        {
            return;
        }

        animOnCamera.Play("camera_flash");
    }
}
