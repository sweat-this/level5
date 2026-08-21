namespace Level5.Core
{
    /// <summary>
    /// Everything the basketball shot pipeline needs from whoever is shooting, without depending on a
    /// concrete <c>PlayerController</c>/<c>AutoPlayerController</c>.
    ///
    /// Player↔basketball cycle-cut slice. <c>PlayerController</c> and <c>AutoPlayerController</c> both
    /// implement this directly - Unity resolves interfaces through <c>GetComponent&lt;T&gt;()</c>, so no
    /// adapter type is needed. Basketball-side code reaches an instance through
    /// <c>PlayerIdentifier.Actor</c> rather than through a concrete player type, which is what lets
    /// <c>BasketBall</c>/<c>BasketBallAuto</c>/<c>ShotMeter</c>/<c>RangeMeter</c> stop referencing
    /// <c>PlayerController</c>, <c>AutoPlayerController</c> and <c>CharacterProfile</c> entirely.
    ///
    /// Every member here is traced to a real basketball-side call site - see
    /// docs/systems-restructure-plan.md's player/basketball edge measurement. None are speculative.
    ///
    /// Code review on this slice: unlike the <c>GameObject</c>/<c>Component</c> references it
    /// replaced, a plain <c>IShooterActor</c> reference does not carry Unity's overloaded
    /// <c>==</c>/fake-null semantics - a cached reference to a <c>Destroy()</c>-ed implementer would
    /// compare as non-null and NRE on the next member access, where the old GameObject-guarded reads
    /// (<c>playerIdentifier.player &amp;&amp; ...</c>) degraded gracefully instead. Not a live bug
    /// today - players are disabled/teleported, never <c>Destroy()</c>-ed, anywhere in the current
    /// codebase - but worth knowing before adding player pooling or a destroy-based respawn path.
    /// </summary>
    public interface IShooterActor
    {
        bool HasBasketball { get; set; }

        bool FacingFront { get; }

        bool Grounded { get; }

        bool InAir { get; }

        /// <summary>True while the actor's dunk takeoff animation state is active.</summary>
        bool InDunkState { get; }

        float DistanceFromRim { get; }

        ShooterAttributes ShooterAttributes { get; }

        /// <summary>
        /// The CPU clutch-bonus roll stat, read live rather than folded into
        /// <see cref="ShooterAttributes"/>. Code review on this slice: <c>ShooterAttributes</c> is
        /// memoized once, on first access, by both controllers - fine for the fields the human path
        /// reads at Start()-adjacent times, but the CPU clutch roll used to read
        /// <c>CharacterProfile.Clutch</c> live, well after every Start() had run, which made it
        /// immune to the cross-GameObject Start()-order question entirely. Folding it into the
        /// memoized struct would have exposed it to that question for the first time - a real
        /// regression risk with no error or log to surface it. This member exists so it stays live.
        /// </summary>
        int Clutch { get; }

        float ShotMeterSliderValue { get; }

        bool ShotMeterEnded { get; }

        void SetAnimBool(string name, bool value);

        void SetAnimTrigger(string name);

        void LockCallBallToPlayer(bool locked);

        void DisplayShotMeterMessage(string message);

        /// <summary>
        /// CPU-only shot-cycle reset. A no-op on the human implementation - the human path never had an
        /// equivalent step, and calling this unconditionally from both <c>BasketBall.Launch</c> and
        /// <c>BasketBallAuto.Launch</c> keeps the two launch call sites symmetric without changing human
        /// behavior.
        /// </summary>
        void EndShootCycle();
    }
}
