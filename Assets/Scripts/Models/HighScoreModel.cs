using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Level5.Core.Match;

namespace Assets.Scripts.database
{
    [Serializable]
    public class HighScoreModel
    {
        public int Id;
        public int Userid;
        public string UserName;
        public int Modeid;
        public int Characterid;
        public int Levelid;
        public string Character;
        public string Level;
        public string Os;
        public string Version;
        public string Date;
        public float Time;
        public int Difficulty;
        public int TotalPoints;
        public float LongestShot;
        public float TotalDistance;
        public int MaxShotMade;
        public int MaxShotAtt;
        public int ConsecutiveShots;
        public int TrafficEnabled;
        public int HardcoreEnabled;
        public int EnemiesEnabled;
        public int EnemiesKilled;
        public string Platform;
        public string Device;
        public string Ipaddress;
        public string Scoreid;
        public int TwoMade;
        public int TwoAtt;
        public int ThreeMade;
        public int ThreeAtt;
        public int FourMade;
        public int FourAtt;
        public int SevenMade;
        public int SevenAtt;
        public int BonusPoints;
        public int MoneyBallMade;
        public int MoneyBallAtt;
        public int SniperEnabled;
        public int SniperMode;
        public string SniperModeName;
        public int SniperShots;
        public int Sniperhits;
        // add versus mode stats to save
        public int p1TotalPoints;
        public int p2TotalPoints;
        public int p3TotalPoints;
        public int p4TotalPoints;
        public string firstPlace;
        public string secondPlace;
        public string thirdPlace;
        public string fourthPlace;
        public int p1IsCpu;
        public int p2IsCpu;
        public int p3IsCpu;
        public int p4IsCpu;
        public int numPlayers;
        public int campaignWins;
        public int campaignLosses;
        public int campaignTies;

        public HighScoreModel convertCampaignBasketBallStatsToModel(GameStats gameStats)
        {
            int trafficEnabled = 0;
            if (MatchRuntime.Rules.TrafficEnabled)
            {
                trafficEnabled = 1;
            }
            int hardcoreEnabled = 0;
            if (MatchRuntime.Rules.Hardcore)
            {
                hardcoreEnabled = 1;
            }
            int enemiesEnabled = 0;
            if (MatchRuntime.Rules.EnemiesEnabled)
            {
                enemiesEnabled = 1;
            }
            int sniperEnabled = 0;
            if (MatchRuntime.Rules.SniperEnabled)
            {
                sniperEnabled = 1;
            }

            HighScoreModel model = new HighScoreModel();

            model.Scoreid = generateUniqueScoreID();
            model.Modeid = MatchRuntime.RawModeId;
            model.Characterid = MatchRuntime.PrimaryCharacterId;
            model.Character = MatchRuntime.PrimaryCharacterDisplayName;
            model.Levelid = MatchRuntime.LevelId;
            model.Level = MatchRuntime.LevelDisplayName;
            model.Os = SystemInfo.operatingSystem;
            model.Version = Application.version;
            model.Date = DateTime.Now.ToString();
            model.Time = gameStats.Stats.TimePlayed;
            model.Difficulty = MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty);
            model.TotalPoints = gameStats.Stats.TotalPoints;
            model.LongestShot = gameStats.Stats.LongestShotMade;
            model.TotalDistance = gameStats.Stats.TotalDistance;
            model.MaxShotMade = gameStats.Stats.ShotMade;
            model.MaxShotAtt = gameStats.Stats.ShotAttempt;
            model.ConsecutiveShots = gameStats.Stats.MostConsecutiveShots;
            model.TrafficEnabled = trafficEnabled;
            model.HardcoreEnabled = hardcoreEnabled;
            model.EnemiesKilled = gameStats.Stats.EnemiesKilled;
            model.Device = SystemInfo.deviceModel;
            model.Platform = SystemInfo.deviceType.ToString();
            //model.Ipaddress = GetExternalIpAdress();
            model.TwoMade = gameStats.Stats.TwoPointerMade;
            model.TwoAtt = gameStats.Stats.TwoPointerAttempts;
            model.ThreeMade = gameStats.Stats.ThreePointerMade;
            model.ThreeAtt = gameStats.Stats.ThreePointerAttempts;
            model.FourMade = gameStats.Stats.FourPointerMade;
            model.FourAtt = gameStats.Stats.FourPointerAttempts;
            model.SevenMade = gameStats.Stats.SevenPointerMade;
            model.SevenAtt = gameStats.Stats.SevenPointerAttempts;
            model.BonusPoints = gameStats.Stats.BonusPoints;
            model.MoneyBallMade = gameStats.Stats.MoneyBallMade;
            model.MoneyBallAtt = gameStats.Stats.MoneyBallAttempts;
            model.EnemiesEnabled = enemiesEnabled;
            model.UserName = GameOptions.userName;
            model.Userid = GameOptions.userid;
            model.SniperEnabled = sniperEnabled;
            if (!MatchRuntime.Rules.SniperEnabled)
            {
                model.SniperMode = 0;
                model.SniperModeName = "none";
            }
            if (MatchRuntime.Rules.Sniper == Level5.Core.Match.SniperMode.Bullet)
            {
                model.SniperMode = 1;
                model.SniperModeName = "single bullet";
            }
            if (MatchRuntime.Rules.Sniper == Level5.Core.Match.SniperMode.MachineGun)
            {
                model.SniperMode = 2;
                model.SniperModeName = "machine gun ";
            }
            if (MatchRuntime.Rules.Sniper == Level5.Core.Match.SniperMode.Laser)
            {
                model.SniperMode = 3;
                model.SniperModeName = "disintegration ray";
            }
            model.SniperShots = gameStats.Stats.SniperShots;
            model.Sniperhits = gameStats.Stats.SniperHits;
            //Debug.Log("MatchRuntime.ParticipantCount : " + MatchRuntime.ParticipantCount);
            //Debug.Log("isCpu : " + isCpu);
            //Debug.Log(" : " + characterProfile.PlayerDisplayName);
            //Debug.Log("pi[1]. : " + pi[1].characterProfile.PlayerDisplayName);

            model.numPlayers = MatchRuntime.ParticipantCount;
            model.Difficulty = MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty);
            model.campaignWins = gameStats.Stats.CampaignWins;
            model.campaignLosses = gameStats.Stats.CampaignLosses;
            model.campaignTies = gameStats.Stats.CampaignTies;

            return model;
        }
        public HighScoreModel convertBasketBallStatsToModel(List<PlayerIdentifier> pi)
        {
            PlayerIdentifier primaryPlayer = GetPrimaryPlayer(pi);
            GameStats primaryStats = primaryPlayer != null ? primaryPlayer.gameStats : null;
            int trafficEnabled = 0;
            if (MatchRuntime.Rules.TrafficEnabled)
            {
                trafficEnabled = 1;
            }
            int hardcoreEnabled = 0;
            if (MatchRuntime.Rules.Hardcore)
            {
                hardcoreEnabled = 1;
            }
            int enemiesEnabled = 0;
            if (MatchRuntime.Rules.EnemiesEnabled)
            {
                enemiesEnabled = 1;
            }
            int sniperEnabled = 0;
            if (MatchRuntime.Rules.SniperEnabled)
            {
                sniperEnabled = 1;
            }

            HighScoreModel model = new HighScoreModel();

            model.Scoreid = generateUniqueScoreID();
            model.Modeid = MatchRuntime.RawModeId;
            model.Characterid = MatchRuntime.PrimaryCharacterId;
            model.Character = MatchRuntime.PrimaryCharacterDisplayName;
            model.Levelid = MatchRuntime.LevelId;
            model.Level = MatchRuntime.LevelDisplayName;
            model.Os = SystemInfo.operatingSystem;
            model.Version = Application.version;
            model.Date = DateTime.Now.ToString();
            model.Difficulty = MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty);
            if (primaryStats != null)
            {
                model.Time = primaryStats.TimePlayed;
                model.TotalPoints = primaryStats.TotalPoints;
                model.LongestShot = primaryStats.LongestShotMade;
                model.TotalDistance = primaryStats.TotalDistance;
                model.MaxShotMade = primaryStats.ShotMade;
                model.MaxShotAtt = primaryStats.ShotAttempt;
                model.ConsecutiveShots = primaryStats.MostConsecutiveShots;
            }
            model.TrafficEnabled = trafficEnabled;
            model.HardcoreEnabled = hardcoreEnabled;
            if (primaryStats != null)
            {
                model.EnemiesKilled = primaryStats.EnemiesKilled;
            }
            model.Device = SystemInfo.deviceModel;
            model.Platform = SystemInfo.deviceType.ToString();
            //model.Ipaddress = GetExternalIpAdress();
            if (primaryStats != null)
            {
                model.TwoMade = primaryStats.TwoPointerMade;
                model.TwoAtt = primaryStats.TwoPointerAttempts;
                model.ThreeMade = primaryStats.ThreePointerMade;
                model.ThreeAtt = primaryStats.ThreePointerAttempts;
                model.FourMade = primaryStats.FourPointerMade;
                model.FourAtt = primaryStats.FourPointerAttempts;
                model.SevenMade = primaryStats.SevenPointerMade;
                model.SevenAtt = primaryStats.SevenPointerAttempts;
                model.BonusPoints = primaryStats.BonusPoints;
                model.MoneyBallMade = primaryStats.MoneyBallMade;
                model.MoneyBallAtt = primaryStats.MoneyBallAttempts;
            }
            model.EnemiesEnabled = enemiesEnabled;
            model.UserName = GameOptions.userName;
            model.Userid = GameOptions.userid;
            model.SniperEnabled = sniperEnabled;
            if (!MatchRuntime.Rules.SniperEnabled)
            {
                model.SniperMode = 0;
                model.SniperModeName = "none";
            }
            if (MatchRuntime.Rules.Sniper == Level5.Core.Match.SniperMode.Bullet)
            {
                model.SniperMode = 1;
                model.SniperModeName = "single bullet";
            }
            if (MatchRuntime.Rules.Sniper == Level5.Core.Match.SniperMode.MachineGun)
            {
                model.SniperMode = 2;
                model.SniperModeName = "machine gun ";
            }
            if (MatchRuntime.Rules.Sniper == Level5.Core.Match.SniperMode.Laser)
            {
                model.SniperMode = 3;
                model.SniperModeName = "disintegration ray";
            }
            if (primaryStats != null)
            {
                model.SniperShots = primaryStats.SniperShots;
                model.Sniperhits = primaryStats.SniperHits;
            }
            //Debug.Log("MatchRuntime.ParticipantCount : " + MatchRuntime.ParticipantCount);
            //Debug.Log("primaryPlayer.isCpu : " + primaryPlayer.isCpu);
            //Debug.Log("primaryPlayer : " + primaryPlayer.characterProfile.PlayerDisplayName);
            //Debug.Log("pi[1]. : " + pi[1].characterProfile.PlayerDisplayName);

            if (TryGetPlayer(pi, 0, out PlayerIdentifier firstPlacePlayer))
            {
                model.p1TotalPoints = firstPlacePlayer.gameStats.Stats.TotalPoints;
                model.firstPlace = firstPlacePlayer.characterProfile.PlayerDisplayName;
                model.p1IsCpu = GetCpuFlag(firstPlacePlayer);
            }
            else
            {
                model.p1TotalPoints = 0;
                model.firstPlace = "";
                model.p1IsCpu = 99;
            }
            //player2
            if (MatchRuntime.ParticipantCount > 1
                && MatchRuntime.RawModeId != Modes.Lockdown
                && TryGetPlayer(pi, 1, out PlayerIdentifier secondPlacePlayer))
            {
                model.p2TotalPoints = secondPlacePlayer.gameStats.Stats.TotalPoints;
                model.secondPlace = secondPlacePlayer.characterProfile.PlayerDisplayName;
                model.p2IsCpu = GetCpuFlag(secondPlacePlayer);
            }
            else
            {
                model.p2TotalPoints = 0;
                model.secondPlace = "";
                model.p2IsCpu = 99;
            }
            //player 3
            if (MatchRuntime.ParticipantCount > 2 && TryGetPlayer(pi, 2, out PlayerIdentifier thirdPlacePlayer))
            {
                model.p3TotalPoints = thirdPlacePlayer.gameStats.Stats.TotalPoints;
                model.thirdPlace = thirdPlacePlayer.characterProfile.PlayerDisplayName;
                model.p3IsCpu = GetCpuFlag(thirdPlacePlayer);
            }
            else
            {
                model.p3TotalPoints = 0;
                model.thirdPlace = "";
                model.p3IsCpu = 99;
            }
            //player 4
            if (MatchRuntime.ParticipantCount > 3 && TryGetPlayer(pi, 3, out PlayerIdentifier fourthPlacePlayer))
            {
                model.p4TotalPoints = fourthPlacePlayer.gameStats.Stats.TotalPoints;
                model.fourthPlace = fourthPlacePlayer.characterProfile.PlayerDisplayName;
                model.p4IsCpu = GetCpuFlag(fourthPlacePlayer);
            }
            else
            {
                model.p4TotalPoints = 0;
                model.fourthPlace = "";
                model.p4IsCpu = 99;
            }
            model.numPlayers = MatchRuntime.ParticipantCount;
            model.Difficulty = MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty);

            return model;
        }

        private static PlayerIdentifier GetPrimaryPlayer(List<PlayerIdentifier> players)
        {
            if (players == null)
            {
                return null;
            }

            foreach (PlayerIdentifier player in players)
            {
                if (player != null && !player.isCpu && player.gameStats != null)
                {
                    return player;
                }
            }

            foreach (PlayerIdentifier player in players)
            {
                if (player != null && player.gameStats != null)
                {
                    return player;
                }
            }

            return null;
        }

        private static bool TryGetPlayer(List<PlayerIdentifier> players, int index, out PlayerIdentifier player)
        {
            player = null;
            if (players == null || index < 0 || index >= players.Count)
            {
                return false;
            }

            player = players[index];
            return player != null && player.gameStats != null && player.characterProfile != null;
        }

        private static int GetCpuFlag(PlayerIdentifier player)
        {
            return player.isCpu ? 1 : 0;
        }
       

        string generateUniqueScoreID()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

}
