using UnityEngine;

public class ProgressionState : MonoBehaviour
{
    [SerializeField] int maxThreeAccuraccy;
    [SerializeField] int maxFourAccuraccy;
    [SerializeField] int maxSevenAccuraccy;
    [SerializeField] int maxReleaseAccuraccy;
    [SerializeField] int maxLuck;

    [SerializeField] int addTo3;
    [SerializeField] int addTo4;
    [SerializeField] int addTo7;

    [SerializeField] int addToLuck;
    [SerializeField] int addToRange;
    [SerializeField] int addToRelease;

    [SerializeField] int pointsAvailable;
    [SerializeField] int pointsUsedThisSession;

    [SerializeField] int accuracy3;
    [SerializeField] int accuracy4;
    [SerializeField] int accuracy7;

    [SerializeField] int range;
    [SerializeField] int release;
    [SerializeField] int luck;
    [SerializeField] int level;
    [SerializeField] int experience;

    [SerializeField] int playerId;

    public static ProgressionState instance;

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
        instance = this;
    }
    private void Start()
    {
        maxThreeAccuraccy = 100;
        maxFourAccuraccy = 100;
        maxSevenAccuraccy = 100;
        maxReleaseAccuraccy = 100;
        maxLuck = 10;
    }

    public void initializeState(CharacterProfile characterProfile)
    {
        // check if stats that can be changed are lower than max allowed stats
        //3 accuracy ( X accuracy of projectile)
        if (accuracy3 < maxThreeAccuraccy)
        {
            accuracy3 = (int)characterProfile.Accuracy3Pt;
        }
        else
        {
            accuracy3 = maxThreeAccuraccy;
        }
        //4 accuracy ( X accuracy of projectile)
        if (accuracy4 < maxFourAccuraccy)
        {
            accuracy4 = (int)characterProfile.Accuracy4Pt;
        }
        else
        {
            accuracy4 = maxFourAccuraccy;
        }
        //7 accuracy ( X accuracy of projectile)
        if (accuracy7 < maxSevenAccuraccy)
        {
            accuracy7 = (int)characterProfile.Accuracy7Pt;
        }
        else
        {
            accuracy7 = maxSevenAccuraccy;
        }
        // release accuracy ( Y accuracy of projectile)
        if (release < maxReleaseAccuraccy)
        {
            release = (int)characterProfile.Release;
        }
        else
        {
            release = maxReleaseAccuraccy;
        }
        // luck (% chance of removing all modifiers on shot)
        if (luck < maxLuck)
        {
            luck = (int)characterProfile.Luck;
        }
        else
        {
            luck = maxLuck;
        }
        // (Z accuracy of projectile).
        range = (int)characterProfile.Range;

        pointsAvailable = (int)characterProfile.PointsAvailable;
        level = (int)characterProfile.Level;
        experience = (int)characterProfile.Experience;

        playerId = characterProfile.PlayerId;

    }

    public void clearState()
    {
        accuracy3 = 0;
        accuracy4 = 0;
        accuracy7 = 0;

        range = 0;
        release = 0;
        luck = 0;

        pointsAvailable = 0;
        level = 0;
        experience = 0;

        playerId = 0;
    }

    public int AddTo3 { get => addTo3; set => addTo3 = value; }
    public int AddTo4 { get => addTo4; set => addTo4 = value; }
    public int AddTo7 { get => addTo7; set => addTo7 = value; }
    public int PointsAvailable { get => pointsAvailable; set => pointsAvailable = value; }
    public int PointsUsedThisSession { get => pointsUsedThisSession; set => pointsUsedThisSession = value; }
    public int Accuracy3 { get => accuracy3; set => accuracy3 = value; }
    public int Accuracy4 { get => accuracy4; set => accuracy4 = value; }
    public int Accuracy7 { get => accuracy7; set => accuracy7 = value; }
    public int Range { get => range; set => range = value; }
    public int Release { get => release; set => release = value; }
    public int Luck { get => luck; set => luck = value; }
    public int Level { get => level; set => level = value; }
    public int Experience { get => experience; set => experience = value; }
    public int AddToLuck { get => addToLuck; set => addToLuck = value; }
    public int AddToRange { get => addToRange; set => addToRange = value; }
    public int AddToRelease { get => addToRelease; set => addToRelease = value; }
    public int MaxThreeAccuraccy { get => maxThreeAccuraccy; set => maxThreeAccuraccy = value; }
    public int MaxFourAccuraccy { get => maxFourAccuraccy; set => maxFourAccuraccy = value; }
    public int MaxSevenAccuraccy { get => maxSevenAccuraccy; set => maxSevenAccuraccy = value; }
    public int MaxReleaseAccuraccy { get => maxReleaseAccuraccy; set => maxReleaseAccuraccy = value; }
    public int MaxLuck { get => maxLuck; set => maxLuck = value; }
}
