using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Level5.Core;
using Level5.Core.Match;

public class ShotMeter : MonoBehaviour
{
    PlayerIdentifier playerIdentifier;
    IShooterActor actor;

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

    // Start is called before the first frame update
    void Start()
    {
        playerIdentifier = GetComponentInParent<PlayerIdentifier>();
        actor = playerIdentifier.Actor;
        shooterAttributes = actor.ShooterAttributes;
        slider = GetComponentInChildren<Slider>();
        meterFillTime = calculateSliderFillTime(); // time for shot meter active, based on player jump/time until jump peak
        sliderValueOnPress = transform.Find(sliderValueOnPressName).GetComponent<Text>();
        sliderValueOnPress.text = "";
        sliderMessageText = transform.Find(sliderMessageName).GetComponent<Text>();
        sliderMessageText.text = "";

        if (MatchRuntime.Rules.Hardcore || MatchRuntime.Rules.EnemiesOnly
            || MatchRuntime.Rules.IsBattleRoyal /*|| !MatchRuntime.HasConfiguration*/ || playerIdentifier.isCpu)
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
            if (playerIdentifier.isCpu)
            {
                sliderValueOnButtonPress = playerIdentifier.basketBallAutoController.rollForAutoPlayerSliderValue();
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
