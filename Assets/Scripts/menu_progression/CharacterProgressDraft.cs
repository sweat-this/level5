public class CharacterProgressDraft
{
    public int PlayerId { get; private set; }
    public int Level { get; private set; }
    public int Experience { get; private set; }

    public int OriginalAccuracy3 { get; private set; }
    public int OriginalAccuracy4 { get; private set; }
    public int OriginalAccuracy7 { get; private set; }
    public int OriginalRange { get; private set; }
    public int OriginalRelease { get; private set; }
    public int OriginalLuck { get; private set; }
    public int OriginalPointsAvailable { get; private set; }
    public int OriginalPointsUsed { get; private set; }

    public int AddTo3 { get; set; }
    public int AddTo4 { get; set; }
    public int AddTo7 { get; set; }
    public int ExtraRangePoints { get; set; }

    public int AddToLuck { get; set; }
    public int AddToRange { get; set; }
    public int AddToRelease { get; set; }

    public int PointsAvailable { get; set; }
    public int PointsUsedThisSession { get; set; }

    public int Accuracy3 { get; set; }
    public int Accuracy4 { get; set; }
    public int Accuracy7 { get; set; }
    public int Range { get; set; }
    public int Release { get; set; }
    public int Luck { get; set; }

    public bool HasPendingChanges => PointsUsedThisSession > 0;

    public static CharacterProgressDraft FromProfile(CharacterProfile characterProfile)
    {
        CharacterProgressDraft draft = new CharacterProgressDraft();
        draft.ResetToProfile(characterProfile);
        return draft;
    }

    public void ResetToProfile(CharacterProfile characterProfile)
    {
        PlayerId = characterProfile.PlayerId;
        Level = characterProfile.Level;
        Experience = characterProfile.Experience;

        OriginalAccuracy3 = (int)characterProfile.Accuracy3Pt;
        OriginalAccuracy4 = (int)characterProfile.Accuracy4Pt;
        OriginalAccuracy7 = (int)characterProfile.Accuracy7Pt;
        OriginalRange = characterProfile.Range;
        OriginalRelease = characterProfile.Release;
        OriginalLuck = characterProfile.Luck;
        OriginalPointsAvailable = characterProfile.PointsAvailable;
        OriginalPointsUsed = characterProfile.PointsUsed;

        AddTo3 = 0;
        AddTo4 = 0;
        AddTo7 = 0;
        ExtraRangePoints = 0;
        AddToLuck = 0;
        AddToRange = 0;
        AddToRelease = 0;
        PointsUsedThisSession = 0;

        Accuracy3 = OriginalAccuracy3;
        Accuracy4 = OriginalAccuracy4;
        Accuracy7 = OriginalAccuracy7;
        Range = OriginalRange;
        Release = OriginalRelease;
        Luck = OriginalLuck;
        PointsAvailable = OriginalPointsAvailable;
    }
}
