using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class CampaignRoundPlayModeTests
{
    [UnityTest]
    public IEnumerator FinalLossRetriesWhileAContinueRemains()
    {
        yield return null;

        Assert.That(
            CampaignRoundDecision.Decide(true, true, false, 1),
            Is.EqualTo(CampaignNextAction.Retry));
    }
}
