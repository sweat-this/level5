using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// The match phase machine, with no Unity in it so it can be tested directly.
    ///
    /// The rule that matters: the transition into <see cref="MatchPhase.Ending"/> happens at most
    /// once. The clock, a cleared marker, the player dying and the pause menu can all ask to end the
    /// same match in the same frame; every one of them after the first is a no-op. Duplicated
    /// end-of-match work is how a score gets saved twice and experience gets applied twice.
    /// </summary>
    public sealed class MatchLifecycle
    {
        private MatchPhase phase = MatchPhase.Preparing;

        /// <summary>Raised once per transition, with the phase just entered.</summary>
        public event Action<MatchPhase> PhaseChanged;

        /// <summary>Raised once, when the first end request is accepted.</summary>
        public event Action<MatchEndReason> Ending;

        /// <summary>Raised once, when end-of-match work reports finished.</summary>
        public event Action<MatchEndReason> Completed;

        public MatchPhase Phase => phase;

        public MatchEndReason EndReason { get; private set; } = MatchEndReason.Unknown;

        public bool IsOver => phase == MatchPhase.Ending || phase == MatchPhase.Completed;

        public bool IsPlaying => phase == MatchPhase.Playing;

        /// <summary>Moves into the pre-match countdown. Ignored once the match is over.</summary>
        public bool BeginCountdown()
        {
            return phase == MatchPhase.Preparing && MoveTo(MatchPhase.Countdown);
        }

        /// <summary>Starts play, from either Preparing or Countdown. Ignored once the match is over.</summary>
        public bool BeginPlay()
        {
            if (phase != MatchPhase.Preparing && phase != MatchPhase.Countdown)
            {
                return false;
            }

            return MoveTo(MatchPhase.Playing);
        }

        /// <summary>
        /// Asks to end the match. Returns true only for the request that actually ends it, so a
        /// caller can tell "I ended it" from "it was already ending".
        /// </summary>
        public bool RequestEnd(MatchEndReason reason)
        {
            if (IsOver)
            {
                return false;
            }

            EndReason = reason;
            MoveTo(MatchPhase.Ending);
            Ending?.Invoke(reason);
            return true;
        }

        /// <summary>
        /// Marks the end-of-match work finished. Only valid from <see cref="MatchPhase.Ending"/>;
        /// end work that fails and retries simply does not call this yet.
        /// </summary>
        public bool CompleteEnd()
        {
            if (phase != MatchPhase.Ending)
            {
                return false;
            }

            MoveTo(MatchPhase.Completed);
            Completed?.Invoke(EndReason);
            return true;
        }

        private bool MoveTo(MatchPhase next)
        {
            if (phase == next)
            {
                return false;
            }

            phase = next;
            PhaseChanged?.Invoke(next);
            return true;
        }
    }
}
