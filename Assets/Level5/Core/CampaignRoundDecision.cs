public enum CampaignNextAction
{
    Advance,
    Retry,
    Complete,
    EndRun
}

public static class CampaignRoundDecision
{
    public static CampaignNextAction Decide(
        bool completedFinalLevel,
        bool winnerIsCpu,
        bool tie,
        int continuesRemaining)
    {
        if (tie)
        {
            return CampaignNextAction.Retry;
        }

        if (!winnerIsCpu)
        {
            return completedFinalLevel
                ? CampaignNextAction.Complete
                : CampaignNextAction.Advance;
        }

        return continuesRemaining > 0
            ? CampaignNextAction.Retry
            : CampaignNextAction.EndRun;
    }
}
