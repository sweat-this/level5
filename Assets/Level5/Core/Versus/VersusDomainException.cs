using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// A competitive rule was broken by the caller: an illegal state transition, a duplicate
    /// submission, a result that does not belong to the attempt it was submitted for.
    ///
    /// These throw rather than return false because none of them is a race the caller can lose
    /// innocently. "The match was already ending" is a race and returns false elsewhere in this
    /// project; "you submitted player B's result under player A's attempt" is a bug or an exploit,
    /// and silently ignoring it is how a corrupt series survives to be persisted.
    ///
    /// This is a developer diagnostic. Anything a player can legitimately do wrong - picking an
    /// incompatible playlist, challenging with a mode that has no asynchronous support - comes back
    /// as a <see cref="VersusValidationResult"/> instead, with a code and a message a screen can
    /// show.
    /// </summary>
    public class VersusDomainException : Exception
    {
        public VersusDomainException(string message)
            : base(message)
        {
        }
    }
}
