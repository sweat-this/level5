using System;
using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.UI;

public class StatsTableAllTime : MonoBehaviour
{
    const string twoName = "two_points";
    const string threeName = "three_points";
    const string fourName = "four_points";
    const string sevenName = "seven_points";
    const string moneyballName = "moneyball";
    const string sniperName = "sniper";
    const string totalDistanceName = "total_distance";
    const string totalShotsName = "total_shots";
    const string consecutiveShotsName = "consecutive_shots";
    const string totalPointsName = "total_points";
    const string timePlayedName = "time_played";

    const string twoDBField = "twoMade";
    const string twoAttemptDBField = "twoAtt";
    const string threeDBField = "threeMade";
    const string threeAttemptDBField = "threeAtt";
    const string fourDBField = "fourMade";
    const string fourAttemptDBField = "fourAtt";
    const string sevenDBField = "sevenMade";
    const string sevenAttemptDBField = "sevenAtt";
    const string moneyballMadeDBField = "moneyBallMade";
    const string moneyballAttemptDBField = "moneyBallAtt";
    const string sniperHitDBField = "sniperHits";
    const string sniperShotDBField = "sniperShots";
    const string totalDistanceDBField = "totalDistance";
    const string totalPointsDBField = "totalPoints";
    const string timePlayedDBField = "timePlayed";


    Text twoText;
    Text threeText;
    Text fourText;
    Text sevenText;
    Text moneyBallText;
    Text sniperText;
    Text totalDistanceText;
    Text totalShotsText;
    Text consecutiveShotsText;
    Text totalPointsText;
    Text timePlayedText;

    // Start is called before the first frame update
    void Awake()
    {
        // find object, get second child objects text.
        // 1st child : score description, 2nd : actual score

        twoText = ResolveStatText(twoName);
        threeText = ResolveStatText(threeName);
        fourText = ResolveStatText(fourName);
        sevenText = ResolveStatText(sevenName);
        moneyBallText = ResolveStatText(moneyballName);
        sniperText = ResolveStatText(sniperName);
        totalDistanceText = ResolveStatText(totalDistanceName);
        totalShotsText = ResolveStatText(totalShotsName);
        consecutiveShotsText = ResolveStatText(consecutiveShotsName);
        totalPointsText = ResolveStatText(totalPointsName);
        timePlayedText = ResolveStatText(timePlayedName);

        // loadAllTimeStats() (called from Start(), and only from Start()) dereferences all eleven
        // unconditionally - a partial resolution here would crash there instead of here.
        if (twoText == null || threeText == null || fourText == null || sevenText == null
            || moneyBallText == null || sniperText == null || totalDistanceText == null
            || totalShotsText == null || consecutiveShotsText == null || totalPointsText == null
            || timePlayedText == null)
        {
            Debug.LogError("StatsTableAllTime could not resolve its required scene objects and has been disabled.", this);
            gameObject.SetActive(false);
        }
    }

    /// <summary>The named object's second child holds the value text; both are required.</summary>
    private Text ResolveStatText(string objectName)
    {
        GameObject found = SceneObjects.Find(objectName, this);
        if (found == null || found.transform.childCount < 2)
        {
            return null;
        }

        return found.transform.GetChild(1).GetComponent<Text>();
    }

    // Update is called once per frame
    void Start()
    {
        if (GameObject.FindGameObjectWithTag("database") != null)
            try
            {
                loadAllTimeStats();
            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                return;
            }
    }

    public void loadAllTimeStats()
    {
        int twoM = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", twoDBField);
        int twoA = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", twoAttemptDBField);

        int threeM = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", threeDBField);
        int threeA = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", threeAttemptDBField);

        int fourM = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", fourDBField);
        int fourA = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", fourAttemptDBField);

        int sevenM = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", sevenDBField);
        int sevenA = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", sevenAttemptDBField);

        int mbM = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", moneyballMadeDBField);
        int mbA = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", moneyballAttemptDBField);

        int sHit = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", sniperHitDBField);
        int sShot = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", sniperShotDBField);

        float dist = DBHelper.instance.getFloatValueAllTimeFromTableByField("AllTimeStats", totalDistanceDBField);

        int shotsM = twoM + threeM + fourM + sevenM;
        int shotsA = twoA + threeA + fourA + sevenA;

        int consecSots = DBHelper.instance.getMostConsecutiveShots();

        float longShot = DBHelper.instance.getLongestShotMadeShots();

        int points = DBHelper.instance.getIntValueAllTimeFromTableByField("AllTimeStats", totalPointsDBField);
        float played = DBHelper.instance.getFloatValueAllTimeFromTableByField("AllTimeStats", timePlayedDBField);

        twoText.text = twoM + " / " + twoA + "  " + divideIntsReturnFloatPercentage(twoM, twoA).ToString("00.00") + "%";
        threeText.text = threeM + " / " + threeA + "  " + divideIntsReturnFloatPercentage(threeM, threeA).ToString("00.00") + "%";
        fourText.text = fourM + " / " + fourA + "  " + divideIntsReturnFloatPercentage(fourM, fourA).ToString("00.00") + "%";
        sevenText.text = sevenM + " / " + sevenA + "  " + divideIntsReturnFloatPercentage(sevenM, sevenA).ToString("00.00") + "%";
        moneyBallText.text = mbM + " / " + mbA + "  " + divideIntsReturnFloatPercentage(mbM, mbA).ToString("00.00") + "%";
        sniperText.text = sHit + " / " + sShot + "  " + divideIntsReturnFloatPercentage(sHit, sShot).ToString("00.00") + "%";

        totalDistanceText.text = convertFeetToMiles(dist);
        totalShotsText.text = shotsM + " / " + shotsA + "  " + divideIntsReturnFloatPercentage(shotsM, shotsA).ToString("00.00") + "%";
        consecutiveShotsText.text = consecSots.ToString();
        totalPointsText.text = points.ToString();
        timePlayedText.text = convertSecondsToHoursAndMinutes(played);
    }

    float divideIntsReturnFloatPercentage(int divisor, int dividend)
    {
        return ((float)divisor / (float)dividend * 100);
    }

    string convertSecondsToHoursAndMinutes(float seconds)
    {
        float hours;
        float minutes;
        //float secs;

        hours = Mathf.FloorToInt(seconds / 3600);
        minutes = Mathf.FloorToInt((seconds - (hours * 3600)) / 60);

        return hours.ToString("###") + " hrs " + minutes.ToString("00") + " mins";
    }

    string convertFeetToMiles(float feet)
    {
        float miles;
        float foot;

        miles = Mathf.FloorToInt(feet / 5280);
        foot = Mathf.FloorToInt((feet - (miles * 5280)));
        float percent = miles + (foot / 5280);

        return percent.ToString("00.00") + " miles";
    }
}
