using System;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Owns the life of a match: which phase it is in, and the single door out of it.
///
/// The clock running out, the last shot marker clearing, the player dying and the pause menu can
/// all decide a match is over, sometimes in the same frame. Every one of them calls
/// <see cref="RequestEnd"/>; the first wins and the rest are no-ops. That is what keeps a score
/// from being saved twice and experience from being applied twice.
///
/// The phase machine itself is <see cref="MatchLifecycle"/>, which has no Unity in it and is tested
/// directly. This component is the scene-facing wrapper.
/// </summary>
public class MatchController : MonoBehaviour
{
    private readonly MatchLifecycle lifecycle = new MatchLifecycle();

    [SerializeField] private MatchPhase currentPhase = MatchPhase.Preparing;
    [SerializeField] private MatchEndCause endCause = MatchEndCause.Unknown;

    public static MatchController instance;

    /// <summary>Raised once, when the first end request is accepted.</summary>
    public event Action<MatchEndReason> Ending;

    /// <summary>Raised once, when the end-of-match work reports finished.</summary>
    public event Action<MatchEndReason> Completed;

    public MatchPhase Phase => lifecycle.Phase;

    public MatchEndReason EndReason => lifecycle.EndReason;

    /// <summary>True from the moment an end is accepted, including while the end work is running.</summary>
    public bool IsOver => lifecycle.IsOver;

    public bool IsPlaying => lifecycle.IsPlaying;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Only this component, never the object it sits on - it is attached to a manager that
            // has nothing to do with the duplication.
            Destroy(this);
            return;
        }

        instance = this;
        lifecycle.PhaseChanged += OnPhaseChanged;
        lifecycle.Ending += OnEnding;
        lifecycle.Completed += OnCompleted;
    }

    private void OnDestroy()
    {
        lifecycle.PhaseChanged -= OnPhaseChanged;
        lifecycle.Ending -= OnEnding;
        lifecycle.Completed -= OnCompleted;
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        // No pre-match countdown exists yet, so a match goes live as soon as the scene is up. When
        // one arrives it calls BeginCountdown here instead.
        BeginPlay();
    }

    public void BeginCountdown()
    {
        lifecycle.BeginCountdown();
    }

    public void BeginPlay()
    {
        lifecycle.BeginPlay();
    }

    /// <summary>
    /// Asks to end the match. Returns true only for the caller that actually ended it, so a caller
    /// can tell "I ended this" from "it was already ending".
    /// </summary>
    public bool RequestEnd(MatchEndReason reason)
    {
        return lifecycle.RequestEnd(reason);
    }

    public bool RequestEnd(MatchEndCause cause, string detail = null)
    {
        return RequestEnd(new MatchEndReason(cause, detail));
    }

    /// <summary>Reports the durable end-of-match work finished. Retried work simply does not call this yet.</summary>
    public bool CompleteEnd()
    {
        return lifecycle.CompleteEnd();
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        currentPhase = phase;
    }

    private void OnEnding(MatchEndReason reason)
    {
        endCause = reason.Cause;
        Ending?.Invoke(reason);
    }

    private void OnCompleted(MatchEndReason reason)
    {
        Completed?.Invoke(reason);
    }
}
