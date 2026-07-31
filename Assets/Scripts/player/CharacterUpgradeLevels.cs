using System;

[Serializable]
public class CharacterUpgradeLevels
{
    public int accuracy2Pt;
    public int accuracy3Pt;
    public int accuracy4Pt;
    public int accuracy7Pt;
    public int jumpForce;
    public int speed;
    public int runSpeed;
    public int runSpeedHasBall;
    public int range;
    public int release;
    public int luck;
    public int shootAngle;

    public int TotalSpent =>
        accuracy2Pt
        + accuracy3Pt
        + accuracy4Pt
        + accuracy7Pt
        + jumpForce
        + speed
        + runSpeed
        + runSpeedHasBall
        + range
        + release
        + luck
        + shootAngle;

    public static CharacterUpgradeLevels Sanitize(CharacterUpgradeLevels value)
    {
        if (value == null)
        {
            return new CharacterUpgradeLevels();
        }

        return new CharacterUpgradeLevels
        {
            accuracy2Pt = Math.Max(0, value.accuracy2Pt),
            accuracy3Pt = Math.Max(0, value.accuracy3Pt),
            accuracy4Pt = Math.Max(0, value.accuracy4Pt),
            accuracy7Pt = Math.Max(0, value.accuracy7Pt),
            jumpForce = Math.Max(0, value.jumpForce),
            speed = Math.Max(0, value.speed),
            runSpeed = Math.Max(0, value.runSpeed),
            runSpeedHasBall = Math.Max(0, value.runSpeedHasBall),
            range = Math.Max(0, value.range),
            release = Math.Max(0, value.release),
            luck = Math.Max(0, value.luck),
            shootAngle = Math.Max(0, value.shootAngle)
        };
    }

    public CharacterStats ToBonusStats(CharacterStats upgradeStep)
    {
        if (upgradeStep == null)
        {
            return new CharacterStats();
        }

        return new CharacterStats
        {
            accuracy2Pt = upgradeStep.accuracy2Pt * accuracy2Pt,
            accuracy3Pt = upgradeStep.accuracy3Pt * accuracy3Pt,
            accuracy4Pt = upgradeStep.accuracy4Pt * accuracy4Pt,
            accuracy7Pt = upgradeStep.accuracy7Pt * accuracy7Pt,
            jumpForce = upgradeStep.jumpForce * jumpForce,
            speed = upgradeStep.speed * speed,
            runSpeed = upgradeStep.runSpeed * runSpeed,
            runSpeedHasBall = upgradeStep.runSpeedHasBall * runSpeedHasBall,
            range = upgradeStep.range * range,
            release = upgradeStep.release * release,
            luck = upgradeStep.luck * luck,
            shootAngle = upgradeStep.shootAngle * shootAngle
        };
    }
}
