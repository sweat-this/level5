namespace Level5.Core.Versus
{
    /// <summary>
    /// The two attempts of one simultaneous game: one per series participant, both for the same
    /// current game.
    ///
    /// Deliberately a pair and not a list. A local-simultaneous series has exactly two participants,
    /// and the aggregate operations that take or return this type exist to keep the pair together -
    /// issuing both or neither, and recording both results or neither - not to be the start of
    /// N-player infrastructure.
    /// </summary>
    public readonly struct SimultaneousAttempts
    {
        public SimultaneousAttempts(Attempt first, Attempt second)
        {
            First = first;
            Second = second;
        }

        /// <summary>The attempt issued to <see cref="VersusParticipants.First"/>.</summary>
        public Attempt First { get; }

        /// <summary>The attempt issued to <see cref="VersusParticipants.Second"/>.</summary>
        public Attempt Second { get; }
    }

    /// <summary>
    /// One participant's finished run, as handed to an aggregate submission: which attempt it
    /// completes, who it belongs to, and what it scored.
    ///
    /// The participant is carried explicitly rather than inferred from the attempt or from the order
    /// the two submissions are passed in. Who a result belongs to is never worked out from its
    /// score, its position in a list or which slot happened to finish first.
    /// </summary>
    public readonly struct AttemptSubmission
    {
        public AttemptSubmission(AttemptId attemptId, ParticipantId participantId, AttemptResult result)
        {
            AttemptId = attemptId;
            ParticipantId = participantId;
            Result = result;
        }

        public AttemptId AttemptId { get; }

        public ParticipantId ParticipantId { get; }

        public AttemptResult Result { get; }
    }
}
