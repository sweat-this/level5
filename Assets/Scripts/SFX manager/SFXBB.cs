using UnityEngine;

public class SFXBB : MonoBehaviour
{
    [SerializeField]
    private bool musicEnabled;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

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

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (musicSource == null || effectsSource == null || musicSource == effectsSource)
        {
            Debug.LogError("SFXBB requires distinct serialized music and effects sources.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        playRandomSong();
    }

    public void playSFX(AudioClip audioClip)
    {
        if (!enabled || effectsSource == null || audioClip == null)
        {
            return;
        }

        effectsSource.PlayOneShot(audioClip);
    }

    private void Update()
    {
        // Paused sources report !isPlaying. Do not advance/restart the playlist during Pause.
        if (musicSource == null || Time.timeScale == 0f)
        {
            return;
        }

        bool shouldPlayNextSong = musicEnabled && !musicSource.isPlaying;
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
            musicSource.clip = musicList[currentSongIndex];
            musicSource.Play();
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
        musicSource.clip = musicList[currentSongIndex];
        musicSource.Play();
    }
}
