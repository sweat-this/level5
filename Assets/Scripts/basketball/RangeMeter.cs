using UnityEngine;
using UnityEngine.UI;
using Level5.Core.Match;

public class RangeMeter : MonoBehaviour
{
    PlayerIdentifier playerIdentifier;
    PlayerController playerController;
    AutoPlayerController autoPlayerController;
    CharacterProfile characterProfile;
    Slider slider;
    public Slider Slider => slider;

    Text sliderText;
    Text sliderStatsText;
    const string sliderTextName = "range_slider_value_text";
    const string statsTextName = "range_slider_stats_text";

    [SerializeField]
    float range;

    void Start()
    {
        playerIdentifier = GameLevelManager.instance.players[0];
        if (playerIdentifier.isCpu)
        {
            characterProfile = playerIdentifier.autoPlayer.GetComponent<CharacterProfile>();
            autoPlayerController = playerIdentifier.autoPlayer.GetComponent<AutoPlayerController>();
        }
        else
        {
            characterProfile = playerIdentifier.player.GetComponent<CharacterProfile>();
            playerController = playerIdentifier.player.GetComponent<PlayerController>();
        }
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
            float distance = playerIdentifier.isCpu ? autoPlayerController.PlayerDistanceFromRim : playerController.PlayerDistanceFromRim;
            slider.value = (characterProfile.Range / (distance * 6)) * 100;
            sliderText.text = slider.value.ToString("0") + "%";
            sliderStatsText.text ="Range : "+ characterProfile.Range + " feet";
        }
    }
}
