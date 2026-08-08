using System.Collections;
using UnityEngine;
using Level5.Core.Match;

public class timeOfDay : MonoBehaviour
{


    [SerializeField] float dayOpacity, sunsetOpacity, nightOpacity, sunriseOpacity;
    SpriteRenderer daySpriteRenderer, sunsetSpriteRenderer, nightSpriteRenderer, sunriseSpriteRenderer;

    [SerializeField] bool day, sunset, sunrise, night, transitionInProgress;


    public GameObject dayObject, sunsetObject, nightObject, sunriseObject;
    public float transitionRate, transitionTime;

    [SerializeField]
    float startTime, backgroundTransitionFrequency;

    //note: for sprite renderer;
    // 1f = 100 percent opacity 
    // .5 = 50%
    //  0 = 0%
    // usage -  spriteRenderer.color = new Color(1f, 1f, 1f, alphaLevel);

    // #todo write function to set to a specific time of day
    // the slab only at night

    // Use this for initialization
    void Start()
    {


        daySpriteRenderer = dayObject.GetComponentInChildren<SpriteRenderer>();
        sunsetSpriteRenderer = sunsetObject.GetComponentInChildren<SpriteRenderer>();
        sunriseSpriteRenderer = sunriseObject.GetComponentInChildren<SpriteRenderer>();
        nightSpriteRenderer = nightObject.GetComponentInChildren<SpriteRenderer>();

        dayOpacity = 1;
        sunsetOpacity = 1;
        nightOpacity = 1;
        sunriseOpacity = 1;

        /*layer order :
         * 1 - day
         * 2 - sunset
         * 3 - night
         * 4 - sunrise
         * */

        // if level doesnt require this object, disable it
        if (!MatchRuntime.LevelRequiresTimeOfDay)
        {
            gameObject.SetActive(false);
        }
    }

    void Awake()
    {
        startTime = Time.time;
        backgroundTransitionFrequency = 15;
    }

    // Update is called once per frame
    void Update()
    {
        if (day && transitionInProgress && dayOpacity > 0)
        {
            sunsetSpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            //sunsetOpacity = 1;

            transitionInProgress = false;
            StartCoroutine(DayToSunsetTransition(transitionRate, transitionTime));
        }
        if (sunset && transitionInProgress && sunsetOpacity > 0)
        {
            nightSpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            //nightOpacity = 1f;

            transitionInProgress = false;
            StartCoroutine(SunsetToNightTransition(transitionRate, transitionTime));
        }
        if (night && transitionInProgress && nightOpacity > 0)
        {
            //nightSpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            sunriseSpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
            //sunriseOpacity = 0f;

            transitionInProgress = false;
            StartCoroutine(NightToSunriseTransition(transitionRate, transitionTime));
        }
        if (sunrise && transitionInProgress && sunriseOpacity > 0)
        {
            //daySpriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            //dayOpacity = 1f;

            transitionInProgress = false;
            StartCoroutine(SunriseToDayTransition(transitionRate, transitionTime));
        }
        if (startTime + backgroundTransitionFrequency <= Time.time)
        {
            transitionInProgress = true;
            startTime = Time.time;
            //Debug.Log("if (day && Input.GetButtonDown(KeyCode.Alpha9))");
            //StartCoroutine(TimeOfDayTransition(1, 60));
        }
        /*
        if (Input.GetButtonDown(KeyCode.Alpha9))
        {
            transitionInProgress = true;
            //Debug.Log("if (day && Input.GetButtonDown(KeyCode.Alpha9))");
            //StartCoroutine(TimeOfDayTransition(1, 60));
        }  
        */

    }

    IEnumerator DayToSunsetTransition(float rateOfTransition, float totalTimeForTransition)
    {

        dayOpacity -= (rateOfTransition / 100);
        ////Debug.Log("IEnumerator DayToSunsetTransition(float rateOfTransition, float totalTimeForTransition)");
        ////Debug.Log("opacity : " + dayOpacity);

        yield return new WaitForSecondsRealtime(totalTimeForTransition / 60);

        daySpriteRenderer.color = new Color(1f, 1f, 1f, dayOpacity);

        if (dayOpacity <= 0)
        {
            transitionInProgress = false;
            day = false;
            sunset = true;
            sunsetOpacity = 1;
            dayOpacity = 0;

            //opacity = 1;
        }
        else
        {
            transitionInProgress = true;
        }
    }
    IEnumerator SunsetToNightTransition(float rateOfTransition, float totalTimeForTransition)
    {
        sunsetOpacity -= (rateOfTransition / 100);

        //Debug.Log("IEnumerator SunsetToNightTransition(float rateOfTransition, float totalTimeForTransition)");
        //Debug.Log("opacity : " + sunsetOpacity);

        yield return new WaitForSecondsRealtime(totalTimeForTransition / 60);

        sunsetSpriteRenderer.color = new Color(1f, 1f, 1f, sunsetOpacity);

        if (sunsetOpacity <= 0)
        {
            transitionInProgress = false;
            sunset = false;
            night = true;
            sunsetOpacity = 0;
            nightOpacity = 1;
            //opacity = 1;
        }
        else
        {
            transitionInProgress = true;
        }

    }
    IEnumerator NightToSunriseTransition(float rateOfTransition, float totalTimeForTransition)
    {
        nightOpacity -= (rateOfTransition / 100);

        //Debug.Log("IEnumerator NightToSunriseTransition(float rateOfTransition, float totalTimeForTransition)");
        //Debug.Log("opacity : " + nightOpacity);

        yield return new WaitForSecondsRealtime(totalTimeForTransition / 60);

        nightSpriteRenderer.color = new Color(1f, 1f, 1f, nightOpacity);

        if (nightOpacity <= 0)
        {
            transitionInProgress = false;
            night = false;
            sunrise = true;
            nightOpacity = 0;
            sunriseOpacity = 1;
            //nightSpriteRenderer.color = new Color(1f, 1f, 1f, 0);
            //opacity = 1;
        }
        else
        {
            transitionInProgress = true;
        }
    }
    IEnumerator SunriseToDayTransition(float rateOfTransition, float totalTimeForTransition)
    {
        dayOpacity += (rateOfTransition / 100);

        //Debug.Log("IEnumerator SunriseToDayTransition(float rateOfTransition, float totalTimeForTransition)");
        //Debug.Log("opacity : " + dayOpacity);

        yield return new WaitForSecondsRealtime(totalTimeForTransition / 60);

        daySpriteRenderer.color = new Color(1f, 1f, 1f, dayOpacity);

        if (dayOpacity >= 1)
        {
            transitionInProgress = false;
            sunrise = false;
            day = true;
            sunriseOpacity = 0;
            dayOpacity = 1;
            //opacity = 1;
        }
        else
        {
            transitionInProgress = true;
        }
    }
}

