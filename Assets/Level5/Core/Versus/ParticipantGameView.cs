namespace Level5.Core.Versus
{
    /// <summary>
    /// One game as one participant is entitled to see it.
    ///
    /// Everything a screen renders comes from here. The opponent's result is genuinely absent until
    /// the game resolves - not null-checked, not blanked out downstream, absent - so there is no
    /// arrangement of UI code that can show it early.
    ///
    /// A value object built on demand. It is a snapshot of what was true when it was asked for and
    /// is never held as state.
    /// </summary>
    public sealed class ParticipantGameView
    {
        public ParticipantGameView(
            int gameIndex,
            RulesetId rulesetId,
            int rulesetVersion,
            VersusGameStatus status,
            InformationPolicy informationPolicy,
            AttemptState ownAttemptState,
            AttemptResult ownResult,
            AttemptId ownAttemptId,
            AttemptState opponentAttemptState,
            AttemptResult opponentResult,
            GameResult result,
            float? target,
            AttemptMetric targetMetric)
        {
            GameIndex = gameIndex;
            RulesetId = rulesetId;
            RulesetVersion = rulesetVersion;
            Status = status;
            InformationPolicy = informationPolicy;
            OwnAttemptState = ownAttemptState;
            OwnResult = ownResult;
            OwnAttemptId = ownAttemptId;
            OpponentAttemptState = opponentAttemptState;
            OpponentResult = opponentResult;
            Result = result;
            Target = target;
            TargetMetric = targetMetric;
        }

        public int GameIndex { get; }

        public int GameNumber => GameIndex + 1;

        public RulesetId RulesetId { get; }

        public int RulesetVersion { get; }

        public VersusGameStatus Status { get; }

        public InformationPolicy InformationPolicy { get; }

        public AttemptState OwnAttemptState { get; }

        /// <summary>Your own result. Always yours to see.</summary>
        public AttemptResult OwnResult { get; }

        public AttemptId OwnAttemptId { get; }

        /// <summary>
        /// Whether the opponent has finished. A state, never a number - "Alex: pending" is what a
        /// turn list is made of, and it says nothing about how the run went.
        /// </summary>
        public AttemptState OpponentAttemptState { get; }

        /// <summary>The opponent's result once the game has resolved. Null before that, always.</summary>
        public AttemptResult OpponentResult { get; }

        /// <summary>The verdict once the game has resolved. Null before that.</summary>
        public GameResult Result { get; }

        /// <summary>
        /// The number to beat, under an open-target game whose first attempt is in. Null under a
        /// sealed attempt, and null before the target exists.
        /// </summary>
        public float? Target { get; }

        /// <summary>Which measurement <see cref="Target"/> is quoted in.</summary>
        public AttemptMetric TargetMetric { get; }

        /// <summary>True once both results are on the table together.</summary>
        public bool IsRevealed => OpponentResult != null || Result != null;

        public bool IsAwaitingOpponent => OwnAttemptState == AttemptState.Completed
            && OpponentAttemptState != AttemptState.Completed
            && Status == VersusGameStatus.Active;

        public bool IsYourTurn => Status == VersusGameStatus.Active
            && OwnAttemptState != AttemptState.Completed;

        public override string ToString()
        {
            return $"game {GameNumber} ({RulesetId.Value}): you {OwnAttemptState}, them {OpponentAttemptState}";
        }
    }
}
