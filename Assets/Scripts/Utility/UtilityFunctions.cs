using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Utility
{
    public static class UtilityFunctions
    {
        public static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        public static DateTime GetCurrentDeviceHour()
        {
            Debug.Log(DateTime.Now.Hour.ToString());
            Debug.Log(DateTime.Now.Minute.ToString());
            return DateTime.Now;
        }
        public static string RemoveWhitespace(string str)
        {
            return string.Join("", str.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }
        public static bool ContainsWhiteSpace(String s)
        {
            return s.Contains(" ");
        }
        public static float GetRandomFloat(float min, float max)
        {
            float randNum = Random.Range(min, max);
            return randNum;
        }
        public static int GetRandomInteger(int min, int max)
        {
            int randNum = Random.Range(min, max);
            return randNum;
        }
        public static float getPercentageFloat(int made, int attempt)
        {
            if (attempt > 0)
            {
                float accuracy = (float)made / (float)attempt;
                return (accuracy * 100);
            }
            else
            {
                return 0;
            }
        }
        /// <summary>
        /// Rolls a percentage chance. 0 never succeeds, 100 always does.
        /// This is the single roll helper for the project - see PercentChance for the rules.
        /// </summary>
        public static bool RollPercent(float chancePercent)
        {
            return PercentChance.Succeeds(chancePercent, Random.value);
        }

        public static bool rollForCriticalInt(int max)
        {
            return RollPercent(max);
        }

        public static Transform FindDeepChild(this Transform aParent, string aName)
        {
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(aParent);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c.name == aName)
                    return c;
                foreach (Transform t in c)
                    queue.Enqueue(t);
            }
            return null;
        }
    }
}

