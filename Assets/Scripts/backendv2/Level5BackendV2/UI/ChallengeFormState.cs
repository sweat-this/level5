using System;

namespace Level5.BackendV2
{
    /// <summary>A logical challenge payload and its retry-stable request identity.</summary>
    public sealed class ChallengeFormState
    {
        private Guid? opponentId;
        private string rulesetId;
        private int? rulesetVersion;
        private int totalGames = 3;
        private string informationPolicy;

        public Guid? OpponentId
        {
            get => opponentId;
            set
            {
                if (opponentId != value) CompleteSubmission();
                opponentId = value;
            }
        }

        public string RulesetId
        {
            get => rulesetId;
            set
            {
                if (rulesetId != value) CompleteSubmission();
                rulesetId = value;
            }
        }

        public int? RulesetVersion
        {
            get => rulesetVersion;
            set
            {
                if (rulesetVersion != value) CompleteSubmission();
                rulesetVersion = value;
            }
        }

        public int TotalGames
        {
            get => totalGames;
            set
            {
                if (totalGames != value) CompleteSubmission();
                totalGames = value;
            }
        }

        public string InformationPolicy
        {
            get => informationPolicy;
            set
            {
                if (informationPolicy != value) CompleteSubmission();
                informationPolicy = value;
            }
        }

        public Guid? ClientRequestId { get; private set; }

        public Guid GetOrBeginClientRequestId() => ClientRequestId ??= ClientRequestIdGenerator.NewId();

        public void CompleteSubmission() => ClientRequestId = null;
    }
}
