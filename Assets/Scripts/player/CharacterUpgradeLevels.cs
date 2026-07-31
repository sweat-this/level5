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
