using UnityEngine;
using UnityEngine.UI;
using Level5.Core;
using Level5.Core.Match;

/// <summary>
/// Actor-owned basketball presentation. Bound explicitly by <see cref="SpawnCoordinator"/> during
/// participant composition (<see cref="BindOwner"/>) instead of reading
/// <c>GameLevelManager.instance.players[0]</c> - AUD-010 Phase 1c's RangeMeter slice. Reads its
/// shooter data through <see cref="IShooterActor"/> only, so it never depends on
/// <c>PlayerIdentifier</c>, <c>GameLevelManager</c>, or a concrete player type.
/// </summary>
public class RangeMeter : MonoBehaviour
{
    IShooterActor actor;
    bool isCpu;

    /// <summary>Whether <see cref="BindOwner"/> has run. Set once, at spawn time.</summary>
    public bool Bound { get; private set; }

    Slider slider;
    public Slider Slider => slider;

    Text sliderText;
    Text sliderStatsText;
    const string sliderTextName = "range_slider_value_text";
    const string statsTextName = "range_slider_stats_text";

    [SerializeField]
    float range;

    int shooterRange;

    /// <summary>
    /// Explicit ownership binding from <see cref="SpawnCoordinator"/>, called once immediately after
    /// the owning participant's <c>IShooterActor</c> is resolved and before Unity calls
    /// <see cref="Start"/>. Ownership-only - no presentation side effects.
    /// </summary>
    public void BindOwner(IShooterActor actor, bool isCpu)
    {
        if (actor == null)
        {
            Debug.LogError($"RangeMeter on '{gameObject.name}' was bound with a null actor.", this);
            return;
        }

        if (Bound)
        {
            Debug.LogError($"RangeMeter on '{gameObject.name}' is already bound; ignoring a second BindOwner call.", this);
            return;
        }

        this.actor = actor;
        this.isCpu = isCpu;
        Bound = true;
    }

    void Start()
    {
        if (!Bound)
        {
            Debug.LogError($"RangeMeter on '{gameObject.name}' reached Start() with no bound owner.", this);
            enabled = false;
            return;
        }

        shooterRange = actor.ShooterAttributes.Range;
        slider = GetComponentInChildren<Slider>();
        sliderText = GameObject.Find(sliderTextName).GetComponent<Text>();
        sliderStatsText = GameObject.Find(statsTextName).GetComponent<Text>();

        if (!isCpu && ( MatchRuntime.Rules.Hardcore || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.IsBattleRoyal
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
