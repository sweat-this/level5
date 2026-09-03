using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Level5.Core;
using Level5.Core.Match;

/// <summary>
/// Actor-owned basketball presentation, bound explicitly by <see cref="SpawnCoordinator"/> during
/// participant composition across three independent boundaries:
///
/// - actor ownership (<see cref="BindOwner"/>) - AUD-010 Phase 1c, mirroring <see cref="RangeMeter"/>,
///   instead of reading a parent <c>PlayerIdentifier</c>;
/// - match rules (<see cref="BindMatchRules"/>) - AUD-010 Phase 2b0, instead of reading
///   <c>MatchRuntime.Rules</c> directly;
/// - a CPU's own <see cref="IBasketballRuntime"/> (<see cref="BindBasketballRuntime"/>), bound
///   separately and optionally once that participant's basketball exists, needed only to resolve a
///   CPU's automatic meter value. A defensive/no-ball CPU never receives one - actor ownership and
///   match rules alone are sufficient for a valid <see cref="Start"/>.
/// </summary>
public class ShotMeter : MonoBehaviour
{
    IShooterActor actor;
    bool isCpu;
    IBasketballRuntime basketballRuntime;
    private ResolvedMatchRules matchRules;

    /// <summary>Whether <see cref="BindOwner"/> has run. Set once, at spawn time.</summary>
    public bool Bound { get; private set; }

    private const string sliderValueOnPressName = "slider_value_text";
    private const string sliderMessageName = "slider_message_text";
    private Text sliderValueOnPress;
    private Text sliderMessageText;

    float sliderValueOnButtonPress;
    public float SliderValueOnButtonPress{get => sliderValueOnButtonPress; set => sliderValueOnButtonPress = value;}

    ShooterAttributes shooterAttributes;
    Slider slider;
    public Slider Slider => slider;

    float meterTime;
    float meterStartTime;
    float meterEndTime;
    float meterIncrement;

    bool meterStarted;
    bool meterEnded;
    bool sliderMaxReached;
    bool sliderMinReached;

    private float currentTime;

    public float meterFillTime;
    bool locked;

    public GameObject meterRed;
    public GameObject meterYellow;
    public GameObject meterGreen;
    public GameObject meterHandle;

    public static ShotMeter instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Explicit ownership binding from <see cref="SpawnCoordinator"/>, called once immediately after
    /// the owning participant's <c>IShooterActor</c> is resolved and before Unity calls
    /// <see cref="Start"/>. Ownership-only - no presentation side effects.
    /// </summary>
    public void BindOwner(IShooterActor actor, bool isCpu)
    {
        if (actor == null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' was bound with a null actor.", this);
            return;
        }

        if (Bound)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' is already bound; ignoring a second BindOwner call.", this);
            return;
        }

        this.actor = actor;
        this.isCpu = isCpu;
        Bound = true;
    }

    /// <summary>
    /// Optional runtime association from <see cref="SpawnCoordinator.GiveBall"/>, bound once the
    /// owning participant's basketball exists - needed only to resolve a CPU's automatic meter value.
    /// A defensive/no-ball CPU never receives one and remains a valid, bound ShotMeter.
    /// </summary>
    public void BindBasketballRuntime(IBasketballRuntime runtime)
    {
        if (runtime == null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' was bound with a null basketball runtime.", this);
            return;
        }

        if (!Bound)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' cannot bind a basketball runtime before its actor owner is bound.", this);
            return;
        }

        if (basketballRuntime != null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' already has a bound basketball runtime; ignoring a second BindBasketballRuntime call.", this);
            return;
        }

        if (runtime.Actor != actor || runtime.IsCpu != isCpu)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' was bound with a basketball runtime that does not belong to its own owner.", this);
            return;
        }

        basketballRuntime = runtime;
    }

    /// <summary>
    /// Explicit match-rules binding from <see cref="SpawnCoordinator"/>, called once during participant
    /// composition alongside <see cref="BindOwner"/> and before Unity calls <see cref="Start"/>.
    /// Independent of actor ownership - binding has no gameplay or presentation side effects.
    /// </summary>
    public void BindMatchRules(ResolvedMatchRules rules)
    {
        if (rules == null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' was bound with null match rules.", this);
            return;
        }

        if (matchRules != null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' already has bound match rules; ignoring a second BindMatchRules call.", this);
            return;
        }

        matchRules = rules;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!Bound)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' reached Start() with no bound owner.", this);
            enabled = false;
            return;
        }

        if (matchRules == null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' reached Start() with no bound match rules.", this);
            enabled = false;
            return;
        }

        shooterAttributes = actor.ShooterAttributes;
        slider = GetComponentInChildren<Slider>();
        meterFillTime = calculateSliderFillTime(); // time for shot meter active, based on player jump/time until jump peak
        sliderValueOnPress = transform.Find(sliderValueOnPressName).GetComponent<Text>();
        sliderValueOnPress.text = "";
        sliderMessageText = transform.Find(sliderMessageName).GetComponent<Text>();
        sliderMessageText.text = "";

        if (matchRules.Hardcore || matchRules.EnemiesOnly || matchRules.IsBattleRoyal || isCpu)
        {
            meterRed.SetActive(false);
            meterYellow.SetActive(false);
            meterGreen.SetActive(false);
            meterHandle.SetActive(false);
            sliderValueOnPress.enabled = false;
            sliderMessageText.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // if player grounded reset slider
        if (actor != null && actor.Grounded)
        {
            slider.value = 0;
        }
        // idk
        if (meterStarted && !locked)
        {
            locked = true;
        }
        // this just to move the slider
        if (meterStarted && locked)
        {
            //ShotEnded = false;
            if (!sliderMaxReached)
            {
                currentTime = Time.time;
                meterEndTime = meterStartTime + meterFillTime;
                slider.value = (((currentTime - meterStartTime) / (meterFillTime)) * 100);
                // in case this is where it hits 100, it can carry over to next next if statement and get overwritten
                if (slider.value >= 100)
                {
                    sliderMaxReached = true;
                }
            }
            if (sliderMaxReached)
            {
                currentTime = Time.time;
                slider.value = 90 - Math.Abs(100 - (((currentTime - meterStartTime) / (meterFillTime)) * 100));
            }
        }
        // this is to set the values and text display. it is separate from the above code
        if (meterEnded)
        {
            locked = false;
            if (isCpu)
            {
                sliderValueOnButtonPress = ResolveCpuMeterValue();
            }
            else
            {
                sliderValueOnButtonPress = Mathf.CeilToInt((((Time.time - meterStartTime) / (meterFillTime) * 100)));
                if (sliderValueOnButtonPress >= 100)
                {
                    // example : 90 - ABS( 100 -115 [ 15 ])  --> 100 - 15 = 75
                    // start at 90. 10 point penalty for hitting peak
                    sliderValueOnButtonPress = 90 - Math.Abs(100 - sliderValueOnButtonPress);
                }
            }
            // used in launch
            slider.value = sliderValueOnButtonPress;
            // display number
            displaySliderValueOnPressText(sliderValueOnButtonPress.ToString("###"));

            meterStarted = false;
            meterEnded = false;
            sliderMaxReached = false;
        }
    }

    public float MeterStartTime
    {
        get => meterStartTime;
        set => meterStartTime = value;
    }

    public float MeterEndTime
    {
        get => meterEndTime;
        set => meterEndTime = value;
    }
    float calculateSliderFillTime()
    {
        float time = shooterAttributes.JumpForce / Physics.gravity.y;
        return Math.Abs(time);
    }

    /// <summary>
    /// A normal CPU shooter is expected to have its own bound <see cref="IBasketballRuntime"/> by the
    /// time its meter reaches automatic resolution - only a defensive/no-ball CPU legitimately has
    /// none, and that CPU never reaches a shot meter completion to begin with. Reaching here without
    /// one, or with a runtime that is not this participant's own CPU ball, is invalid composition:
    /// logged rather than papered over with a global lookup or participant zero.
    ///
    /// The <c>0f</c> returned on every error branch is a safe sentinel, not a real roll: it is what
    /// keeps <c>meterStarted</c>/<c>meterEnded</c> transitioning normally afterward (see the caller),
    /// which is what lets <c>BasketBallAuto.LaunchBasketBall</c>'s <c>WaitUntil</c> resolve instead of
    /// hanging - aborting the shot cycle here was rejected as a worse failure mode. The
    /// <c>Debug.LogError</c> above each return is the actual signal something is wrong; do not "clean
    /// up" these returns without keeping an equivalent signal, and do not assume <c>0f</c> reaching the
    /// shot pipeline means a real, terrible shot was rolled.
    /// </summary>
    float ResolveCpuMeterValue()
    {
        if (basketballRuntime == null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' reached CPU meter resolution with no bound basketball runtime.", this);
            return 0f;
        }

        if (!basketballRuntime.IsCpu)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' has a bound basketball runtime that is not CPU-owned.", this);
            return 0f;
        }

        BasketBallAuto autoRuntime = basketballRuntime as BasketBallAuto;
        if (autoRuntime == null)
        {
            Debug.LogError($"ShotMeter on '{gameObject.name}' has a bound CPU basketball runtime that is not a BasketBallAuto.", this);
            return 0f;
        }

        return autoRuntime.rollForAutoPlayerSliderValue();
    }
    public bool MeterStarted
    {
        get => meterStarted;
        set => meterStarted = value;
    }
    public bool MeterEnded
    {
        get => meterEnded;
        set => meterEnded = value;
    }

    public void displaySliderValueOnPressText(String message)
    {
        StartCoroutine(toggleSliderValueOnPressText(2, message));
    }

    public void displaySliderMessageText(String message)
    {
        StartCoroutine(toggleSliderMessageText(2, message));
    }
    IEnumerator toggleSliderValueOnPressText(float seconds, String message)
    {
        sliderValueOnPress.text = message;
        yield return new WaitForSeconds(seconds);
        sliderValueOnPress.text = "";
    }
    IEnumerator toggleSliderMessageText(float seconds, String message)
    {
        sliderMessageText.text = message;
        yield return new WaitForSeconds(seconds);
        sliderMessageText.text = "";
    }
}
