using UnityEngine;

public class SFXBB : MonoBehaviour
{
    [SerializeField]
    private bool musicEnabled;
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip[] musicList;
    private int currentSongIndex;

    public AudioClip basketballBounce;
    public AudioClip basketballHitRim;
    public AudioClip basketballHitFence;
    public AudioClip basketballNetSwish;
    public AudioClip cameraFlash;
    public AudioClip alien_walk;
    public AudioClip gamechanger;
    public AudioClip werewolfHowl;
    public AudioClip worker_parasite;
    public AudioClip airhorn;
    public AudioClip lightningStrike;
    public AudioClip rimShot;
    public AudioClip knockedDown;
    public AudioClip blocked;
    public AudioClip skateGrind;
    public AudioClip glitch;
    public AudioClip turnIntoBat;
    public AudioClip airGuitar;
    public AudioClip chainRattle;
    public AudioClip deathRay;
    public AudioClip probeCritical;
    public AudioClip metalBang;
    public AudioClip stoneCold;
    public AudioClip chopWood;
    public AudioClip shootGun;
    public AudioClip takeDamage;
    public AudioClip shotgunRack;
    public AudioClip vampireHiss;
    public AudioClip projectileRocket;
    public AudioClip whipCrack;
    public AudioClip shootAutomaticAK47;
    public AudioClip impactRiccochet1;
    public AudioClip impactRiccochet2;
    public AudioClip impactFabric;

    public static SFXBB instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        playRandomSong();
    }

    public void playSFX(AudioClip audioClip)
    {
        if (audioSource == null || audioClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(audioClip);
    }

    private void Update()
    {
        if (audioSource == null)
        {
            return;
        }

        bool shouldPlayNextSong = musicEnabled && !audioSource.isPlaying;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        shouldPlayNextSong = shouldPlayNextSong || (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha0));
#endif
        if (shouldPlayNextSong)
        {
            playNextSong();
        }
    }

    void playRandomSong()
    {
        if (musicEnabled && musicList != null && musicList.Length > 0)
        {
            int randNum = Random.Range(0, musicList.Length);
            currentSongIndex = randNum;
            audioSource.clip = musicList[currentSongIndex];
            audioSource.Play();
        }
    }

    void playNextSong()
    {
        if (musicList == null || musicList.Length == 0)
        {
            return;
        }

        //int newIndex=0;
        if (currentSongIndex == (musicList.Length - 1))
        {
            currentSongIndex = 0;
        }
        else
        {
            currentSongIndex++;
        }
        audioSource.clip = musicList[currentSongIndex];
        audioSource.Play();
    }
}
