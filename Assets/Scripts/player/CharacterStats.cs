using System;
using UnityEngine;

[Serializable]
public class CharacterStats
{
    public float accuracy2Pt;
    public float accuracy3Pt;
    public float accuracy4Pt;
    public float accuracy7Pt;
    public float jumpForce;
    public float speed;
    public float runSpeed;
    public float runSpeedHasBall;
    public int range;
    public int release;
    public int luck;
    public int shootAngle;

    public CharacterStats Clone()
    {
        return new CharacterStats
        {
            accuracy2Pt = accuracy2Pt,
            accuracy3Pt = accuracy3Pt,
            accuracy4Pt = accuracy4Pt,
            accuracy7Pt = accuracy7Pt,
            jumpForce = jumpForce,
            speed = speed,
            runSpeed = runSpeed,
            runSpeedHasBall = runSpeedHasBall,
            range = range,
            release = release,
            luck = luck,
            shootAngle = shootAngle
        };
    }

    public static CharacterStats Add(CharacterStats baseStats, CharacterStats bonusStats)
    {
        if (baseStats == null)
        {
            return bonusStats == null ? new CharacterStats() : bonusStats.Clone();
        }

        if (bonusStats == null)
        {
            return baseStats.Clone();
        }

        return new CharacterStats
        {
            accuracy2Pt = baseStats.accuracy2Pt + bonusStats.accuracy2Pt,
            accuracy3Pt = baseStats.accuracy3Pt + bonusStats.accuracy3Pt,
            accuracy4Pt = baseStats.accuracy4Pt + bonusStats.accuracy4Pt,
            accuracy7Pt = baseStats.accuracy7Pt + bonusStats.accuracy7Pt,
            jumpForce = baseStats.jumpForce + bonusStats.jumpForce,
            speed = baseStats.speed + bonusStats.speed,
            runSpeed = baseStats.runSpeed + bonusStats.runSpeed,
            runSpeedHasBall = baseStats.runSpeedHasBall + bonusStats.runSpeedHasBall,
            range = baseStats.range + bonusStats.range,
            release = baseStats.release + bonusStats.release,
            luck = baseStats.luck + bonusStats.luck,
            shootAngle = baseStats.shootAngle + bonusStats.shootAngle
        };
    }

    public static CharacterStats Clamp(CharacterStats value, CharacterStats min, CharacterStats max)
    {
        if (value == null)
        {
            return new CharacterStats();
        }

        return new CharacterStats
        {
            accuracy2Pt = ClampFloat(value.accuracy2Pt, min?.accuracy2Pt, max?.accuracy2Pt),
            accuracy3Pt = ClampFloat(value.accuracy3Pt, min?.accuracy3Pt, max?.accuracy3Pt),
            accuracy4Pt = ClampFloat(value.accuracy4Pt, min?.accuracy4Pt, max?.accuracy4Pt),
            accuracy7Pt = ClampFloat(value.accuracy7Pt, min?.accuracy7Pt, max?.accuracy7Pt),
            jumpForce = ClampFloat(value.jumpForce, min?.jumpForce, max?.jumpForce),
            speed = ClampFloat(value.speed, min?.speed, max?.speed),
            runSpeed = ClampFloat(value.runSpeed, min?.runSpeed, max?.runSpeed),
            runSpeedHasBall = ClampFloat(value.runSpeedHasBall, min?.runSpeedHasBall, max?.runSpeedHasBall),
            range = ClampInt(value.range, min?.range, max?.range),
            release = ClampInt(value.release, min?.release, max?.release),
            luck = ClampInt(value.luck, min?.luck, max?.luck),
            shootAngle = ClampInt(value.shootAngle, min?.shootAngle, max?.shootAngle)
        };
    }

    private static float ClampFloat(float value, float? min, float? max)
    {
        if (min.HasValue && value < min.Value)
        {
            value = min.Value;
        }

        if (max.HasValue && value > max.Value)
        {
            value = max.Value;
        }

        return value;
    }

    private static int ClampInt(int value, int? min, int? max)
    {
        if (min.HasValue && value < min.Value)
        {
            value = min.Value;
        }

        if (max.HasValue && value > max.Value)
        {
            value = max.Value;
        }

        return value;
    }
}
