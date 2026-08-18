using UnityEngine;

public class CharacterProgressionService
{
    public CharacterProgressDraft CreateDraft(CharacterProfile characterProfile)
    {
        return CharacterProgressDraft.FromProfile(characterProfile);
    }

    public void ResetDraft(CharacterProgressDraft draft, CharacterProfile characterProfile, ProgressionState progressionState)
    {
        draft.ResetToProfile(characterProfile);
        RecalculateDraft(draft, progressionState);
    }

    public bool TryAddAccuracy3(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        return TryAddAccuracy(draft, progressionState, AccuracySlot.Three);
    }

    public bool TryAddAccuracy4(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        return TryAddAccuracy(draft, progressionState, AccuracySlot.Four);
    }

    public bool TryAddAccuracy7(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        return TryAddAccuracy(draft, progressionState, AccuracySlot.Seven);
    }

    public bool TrySubtractAccuracy3(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        return TrySubtractAccuracy(draft, progressionState, AccuracySlot.Three);
    }

    public bool TrySubtractAccuracy4(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        return TrySubtractAccuracy(draft, progressionState, AccuracySlot.Four);
    }

    public bool TrySubtractAccuracy7(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        return TrySubtractAccuracy(draft, progressionState, AccuracySlot.Seven);
    }

    public bool CommitDraft(CharacterProgressDraft draft, CharacterProfile characterProfile)
    {
        if (draft == null || characterProfile == null)
        {
            Debug.LogError("Character progression could not be saved because the draft or character profile is missing.");
            return false;
        }

        if (DBHelper.instance == null)
        {
            Debug.LogError("Character progression could not be saved because DBHelper is unavailable.");
            return false;
        }

        float originalAccuracy3 = characterProfile.Accuracy3Pt;
        float originalAccuracy4 = characterProfile.Accuracy4Pt;
        float originalAccuracy7 = characterProfile.Accuracy7Pt;
        int originalRange = characterProfile.Range;
        int originalRelease = characterProfile.Release;
        int originalLuck = characterProfile.Luck;
        int originalPointsAvailable = characterProfile.PointsAvailable;
        int originalPointsUsed = characterProfile.PointsUsed;

        characterProfile.Accuracy3Pt = draft.Accuracy3;
        characterProfile.Accuracy4Pt = draft.Accuracy4;
        characterProfile.Accuracy7Pt = draft.Accuracy7;
        characterProfile.Range = draft.Range;
        characterProfile.Release = draft.Release;
        characterProfile.Luck = draft.Luck;
        characterProfile.PointsAvailable = draft.PointsAvailable;
        characterProfile.PointsUsed = draft.OriginalPointsUsed + draft.PointsUsedThisSession;

        if (DBHelper.instance.UpdateCharacterProfile(characterProfile))
        {
            return true;
        }

        characterProfile.Accuracy3Pt = originalAccuracy3;
        characterProfile.Accuracy4Pt = originalAccuracy4;
        characterProfile.Accuracy7Pt = originalAccuracy7;
        characterProfile.Range = originalRange;
        characterProfile.Release = originalRelease;
        characterProfile.Luck = originalLuck;
        characterProfile.PointsAvailable = originalPointsAvailable;
        characterProfile.PointsUsed = originalPointsUsed;
        return false;
    }

    public void ApplyDraftToState(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        if (draft == null || progressionState == null)
        {
            return;
        }

        progressionState.AddTo3 = draft.AddTo3;
        progressionState.AddTo4 = draft.AddTo4;
        progressionState.AddTo7 = draft.AddTo7;
        progressionState.AddToLuck = draft.AddToLuck;
        progressionState.AddToRange = draft.AddToRange;
        progressionState.AddToRelease = draft.AddToRelease;
        progressionState.PointsAvailable = draft.PointsAvailable;
        progressionState.PointsUsedThisSession = draft.PointsUsedThisSession;
        progressionState.Accuracy3 = draft.Accuracy3;
        progressionState.Accuracy4 = draft.Accuracy4;
        progressionState.Accuracy7 = draft.Accuracy7;
        progressionState.Range = draft.Range;
        progressionState.Release = draft.Release;
        progressionState.Luck = draft.Luck;
        progressionState.Level = draft.Level;
        progressionState.Experience = draft.Experience;
    }

    private bool TryAddAccuracy(
        CharacterProgressDraft draft,
        ProgressionState progressionState,
        AccuracySlot slot)
    {
        if (draft == null || progressionState == null)
        {
            return false;
        }

        if (draft.PointsAvailable <= 0)
        {
            return false;
        }

        if (GetAccuracy(draft, slot) < GetMaxAccuracy(progressionState, slot))
        {
            SetPendingAccuracy(draft, slot, GetPendingAccuracy(draft, slot) + 1);
        }
        else
        {
            draft.ExtraRangePoints++;
        }

        RecalculateDraft(draft, progressionState);
        return true;
    }

    private bool TrySubtractAccuracy(
        CharacterProgressDraft draft,
        ProgressionState progressionState,
        AccuracySlot slot)
    {
        if (draft == null || progressionState == null)
        {
            return false;
        }

        int pendingAccuracy = GetPendingAccuracy(draft, slot);
        if (GetAccuracy(draft, slot) >= GetMaxAccuracy(progressionState, slot) && draft.ExtraRangePoints > 0)
        {
            draft.ExtraRangePoints--;
        }
        else if (pendingAccuracy > 0)
        {
            SetPendingAccuracy(draft, slot, pendingAccuracy - 1);
        }
        else if (draft.ExtraRangePoints > 0)
        {
            draft.ExtraRangePoints--;
        }
        else
        {
            return false;
        }

        RecalculateDraft(draft, progressionState);
        return true;
    }

    private void RecalculateDraft(CharacterProgressDraft draft, ProgressionState progressionState)
    {
        draft.PointsUsedThisSession = draft.AddTo3 + draft.AddTo4 + draft.AddTo7 + draft.ExtraRangePoints;
        draft.PointsAvailable = draft.OriginalPointsAvailable - draft.PointsUsedThisSession;

        draft.Accuracy3 = Mathf.Min(draft.OriginalAccuracy3 + draft.AddTo3, progressionState.MaxThreeAccuraccy);
        draft.Accuracy4 = Mathf.Min(draft.OriginalAccuracy4 + draft.AddTo4, progressionState.MaxFourAccuraccy);
        draft.Accuracy7 = Mathf.Min(draft.OriginalAccuracy7 + draft.AddTo7, progressionState.MaxSevenAccuraccy);

        int lastUpdate = draft.Level - draft.OriginalPointsAvailable;
        int luckPointsAvailable = (draft.Level / 3) - (lastUpdate / 3);
        draft.AddToLuck = draft.PointsUsedThisSession <= luckPointsAvailable && draft.OriginalLuck < progressionState.MaxLuck
            ? draft.PointsUsedThisSession
            : 0;

        draft.AddToRelease = draft.OriginalRelease < progressionState.MaxReleaseAccuraccy
            ? draft.PointsUsedThisSession
            : 0;
        draft.AddToRange = draft.PointsUsedThisSession * 5;

        draft.Luck = Mathf.Min(draft.OriginalLuck + draft.AddToLuck, progressionState.MaxLuck);
        draft.Release = Mathf.Min(draft.OriginalRelease + draft.AddToRelease, progressionState.MaxReleaseAccuraccy);
        // Range is deliberately uncapped - see ProgressionState. The drift it is exposed to comes
        // from LoadManager.getPointsUsed reconstructing spent points as (Range - 25) / 5, which is
        // wrong for every character authored at range 55, so each save/load cycle credits phantom
        // points. That reconstruction is the bug, not the absence of a ceiling.
        draft.Range = draft.OriginalRange + draft.AddToRange;
    }

    private int GetAccuracy(CharacterProgressDraft draft, AccuracySlot slot)
    {
        switch (slot)
        {
            case AccuracySlot.Three:
                return draft.Accuracy3;
            case AccuracySlot.Four:
                return draft.Accuracy4;
            case AccuracySlot.Seven:
                return draft.Accuracy7;
            default:
                return 0;
        }
    }

    private int GetMaxAccuracy(ProgressionState progressionState, AccuracySlot slot)
    {
        switch (slot)
        {
            case AccuracySlot.Three:
                return progressionState.MaxThreeAccuraccy;
            case AccuracySlot.Four:
                return progressionState.MaxFourAccuraccy;
            case AccuracySlot.Seven:
                return progressionState.MaxSevenAccuraccy;
            default:
                return 0;
        }
    }

    private int GetPendingAccuracy(CharacterProgressDraft draft, AccuracySlot slot)
    {
        switch (slot)
        {
            case AccuracySlot.Three:
                return draft.AddTo3;
            case AccuracySlot.Four:
                return draft.AddTo4;
            case AccuracySlot.Seven:
                return draft.AddTo7;
            default:
                return 0;
        }
    }

    private void SetPendingAccuracy(CharacterProgressDraft draft, AccuracySlot slot, int value)
    {
        switch (slot)
        {
            case AccuracySlot.Three:
                draft.AddTo3 = value;
                break;
            case AccuracySlot.Four:
                draft.AddTo4 = value;
                break;
            case AccuracySlot.Seven:
                draft.AddTo7 = value;
                break;
        }
    }

    private enum AccuracySlot
    {
        Three,
        Four,
        Seven
    }
}
