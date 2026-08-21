using UnityEngine;
using UnityEngine.UI;
using Level5.Core;
using Level5.Core.Match;

public class RangeMeter : MonoBehaviour
{
    PlayerIdentifier playerIdentifier;
    IShooterActor actor;
    Slider slider;
    public Slider Slider => slider;

    Text sliderText;
    Text sliderStatsText;
    const string sliderTextName = "range_slider_value_text";
    const string statsTextName = "range_slider_stats_text";

    [SerializeField]
    float range;

    int shooterRange;

    void Start()
    {
        playerIdentifier = GameLevelManager.instance.players[0];
        actor = playerIdentifier.Actor;
        shooterRange = actor.ShooterAttributes.Range;
        slider = GetComponentInChildren<Slider>();
        sliderText = GameObject.Find(sliderTextName).GetComponent<Text>();
        sliderStatsText = GameObject.Find(statsTextName).GetComponent<Text>();

        InvokeRepeating("setSliderValue", 0, 0.1f);

        if (!playerIdentifier.isCpu && ( MatchRuntime.Rules.Hardcore || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.IsBattleRoyal
            || !MatchRuntime.HasConfiguration || MatchRuntime.Rules.AllowsCpuShooters))
        {
            gameObject.SetActive(false);
        }
        if(gameObject.activeInHierarchy)
        {
            InvokeRepeating("setSliderValue", 0, 0.1f);
        }
    }

    void setSliderValue()
    {
        if (slider != null && sliderText != null)
        {
            float distance = actor.DistanceFromRim;
            slider.value = (shooterRange / (distance * 6)) * 100;
            sliderText.text = slider.value.ToString("0") + "%";
            sliderStatsText.text ="Range : "+ shooterRange + " feet";
        }
    }
}
