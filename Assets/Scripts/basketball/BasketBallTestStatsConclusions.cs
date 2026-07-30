using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
public class BasketBallTestStatsConclusions : MonoBehaviour
{
    public List<BasketballTestStats> shotStats;

    public int two, three, four;
    public int twoAttempt, threeAttempt, fourAttempt;
    public int made, miss, attempt;

    public float avgReleaseVelocity;
    public float avgAccuracyModifier;

    public float avgReleaseVelocityTwoMade;
    public float avgReleaseVelocityTwoMiss;

    public float avgReleaseVelocityThreeMade;
    public float avgReleaseVelocityThreeMiss;

    public float avgReleaseVelocityFourMade;
    public float avgReleaseVelocityFourMiss;

    public float avgAccuracyModifier2made;
    public float avgAccuracyModifier3made;
    public float avgAccuracyModifier4made;

    public float avgAccuracyModifier2miss;
    public float avgAccuracyModifier3miss;
    public float avgAccuracyModifier4miss;


    public void getDataFromList()
    {
        foreach (var shot in shotStats)
        {
            avgAccuracyModifier += shot.AccuracyModifier;
            avgReleaseVelocity += shot.ReleaseVelocity;
            if (shot.Two)
            {
                twoAttempt++;
                if (shot.Made)
                {
                    avgAccuracyModifier2made += shot.AccuracyModifier;
                    avgReleaseVelocityTwoMade += shot.ReleaseVelocity;
                    two++;
                    made++;

                }
                else
                {
                    avgAccuracyModifier2miss += shot.AccuracyModifier;
                    avgReleaseVelocityTwoMiss += shot.ReleaseVelocity;
                    miss++;
                }
            }

            if (shot.Three)
            {
                threeAttempt++;
                if (shot.Made)
                {
                    avgAccuracyModifier3made += shot.AccuracyModifier;
                    avgReleaseVelocityThreeMade += shot.ReleaseVelocity;
                    three++;
                    made++;
                }
                else
                {
                    avgAccuracyModifier3miss += avgAccuracyModifier3miss;
                    avgReleaseVelocityThreeMiss += shot.ReleaseVelocity;
                    miss++;
                }

            }

            if (shot.Four)
            {
                fourAttempt++;
                if (shot.Made)
                {
                    avgAccuracyModifier4made += shot.AccuracyModifier;
                    avgReleaseVelocityFourMade += shot.ReleaseVelocity;
                    four++;
                    made++;
                }
                else
                {
                    avgAccuracyModifier4miss += shot.AccuracyModifier;
                    avgReleaseVelocityFourMiss += shot.ReleaseVelocity;
                    miss++;
                }
            }
        }

        attempt = shotStats.Count;
    }

    public void printConclusions()
    {

        Debug.Log("========================================================================================");
        Debug.Log("avg shot release velocity : " + (avgReleaseVelocity / attempt).ToString() + "\n");

        Debug.Log("avg shot release velocity : made" + (avgReleaseVelocity / attempt).ToString());
        Debug.Log("     2 - made : " + (avgReleaseVelocityTwoMade / two));
        Debug.Log("     3 - made : " + (avgReleaseVelocityThreeMade / two));
        Debug.Log("     4 - made : " + (avgReleaseVelocityFourMade / two));
        Debug.Log("========================================================================================");
        Debug.Log("avg shot release velocity : miss ");
        Debug.Log("     2 - made : " + (avgReleaseVelocityTwoMiss / (twoAttempt - two)));
        Debug.Log("     3 - made : " + (avgReleaseVelocityThreeMiss / (threeAttempt - three)));
        Debug.Log("     4 - made : " + (avgReleaseVelocityFourMiss / (fourAttempt - four)));
        Debug.Log("========================================================================================");
        Debug.Log("avg accuracy modifer : " + (avgAccuracyModifier / attempt).ToString());
        Debug.Log("avg accuracy modifier - made ");
        Debug.Log("     2 - made : " + (avgAccuracyModifier2made / (twoAttempt - two)));
        Debug.Log("     3 - made : " + (avgAccuracyModifier3made / (threeAttempt - three)));
        Debug.Log("     4 - made : " + (avgAccuracyModifier4made / (fourAttempt - four)));
        Debug.Log("========================================================================================");
        Debug.Log("avg accuracy modifier - miss ");
        Debug.Log("     2 - miss : " + (avgAccuracyModifier2miss / (twoAttempt - two)));
        Debug.Log("     3 - miss : " + (avgAccuracyModifier3miss / (threeAttempt - three)));
        Debug.Log("     4 - miss : " + (avgAccuracyModifier4miss / (fourAttempt - four)));
        Debug.Log("========================================================================================");
        Debug.Log("avg 2 accuracy : " + ((float)(two / twoAttempt)).ToString());
        Debug.Log("avg 3 accuracy : " + ((float)(three / threeAttempt)).ToString());
        Debug.Log("avg 4 accuracy : " + ((float)(four / fourAttempt)).ToString());
        Debug.Log("========================================================================================");
    }
}
#endif
