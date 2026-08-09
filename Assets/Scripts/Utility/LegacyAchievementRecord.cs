using UnityEngine;

/// <summary>
/// Compatibility component for legacy achievement records still serialized in old menu scenes.
/// </summary>
public sealed class LegacyAchievementRecord : MonoBehaviour
{
    [SerializeField] private int type;
    [SerializeField] private int achievementId;
    [SerializeField] private string achievementName;
    [SerializeField] private string achievementDescription;
    [SerializeField] private bool isLocked;
    [SerializeField] private bool allTime;
    [SerializeField] private bool singleGame;
    [SerializeField] private int charId;
    [SerializeField] private int levelId;
    [SerializeField] private int modeId;
    [SerializeField] private int friendId;
    [SerializeField] private int cpuId;
    [SerializeField] private int cpuDefenseId;
    [SerializeField] private int totalPoints;
    [SerializeField] private int totalPoints2;
    [SerializeField] private int totalPoints3;
    [SerializeField] private int totalPoints4;
    [SerializeField] private int totalPoints7;
    [SerializeField] private int totalTime;
    [SerializeField] private int lowTime;
    [SerializeField] private int highTime;
    [SerializeField] private int totalTimePlayed;
    [SerializeField] private int longestShot;
    [SerializeField] private int totalDistance;
    [SerializeField] private int consecutiveShots;
    [SerializeField] private int EnemiesKilled;
    [SerializeField] private int EnemiesKilledMinion;
    [SerializeField] private int EnemiesKilledBoss;
    [SerializeField] private int TwoMade;
    [SerializeField] private int TwoAtt;
    [SerializeField] private int ThreeMade;
    [SerializeField] private int ThreeAtt;
    [SerializeField] private int FourMade;
    [SerializeField] private int FourAtt;
    [SerializeField] private int SevenMade;
    [SerializeField] private int SevenAtt;
    [SerializeField] private int BonusPoints;
    [SerializeField] private int MoneyBallMade;
    [SerializeField] private int MoneyBallAtt;
    [SerializeField] private int SniperShots;
    [SerializeField] private int Sniperhits;
    [SerializeField] private int vsWin;
    [SerializeField] private int vsLoss;
    [SerializeField] private int vsTie;
    [SerializeField] private int campaignWins;
    [SerializeField] private int campaignLosses;
    [SerializeField] private int campaignTies;
    [SerializeField] private int hitByCar;
    [SerializeField] private int rakesSteppedOn;
}
