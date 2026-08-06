using System.Collections;
using UnityEngine;
using Assets.Scripts.Utility;

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

    // Start is called before the first frame update
    void Start()
    {
        instance = this;
        audioSource = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();
        animOnCamera = GameObject.Find("camera_flash").GetComponent<Animator>();
        //npcName = gameObject.transform.root.name;
        spriteObject = transform.gameObject;
        if (GameOptions.customCamera)
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
        animOnCamera.Play("camera_flash");
    }
}
