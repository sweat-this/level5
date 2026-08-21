using System.Collections.Generic;
using Level5.Core;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression coverage for a bug found in code review of the player↔basketball cycle-cut slice:
/// <see cref="IShooterActor.ShooterAttributes"/> is memoized on first access (see PlayerController's
/// and AutoPlayerController's explicit implementations), which is safe for the fields it carries but
/// would have been a silent regression for <see cref="IShooterActor.Clutch"/> specifically -
/// <c>BasketBallAuto.rollForAutoPlayerSliderValue</c> used to read <c>CharacterProfile.Clutch</c>
/// live, well after every Start() has run, and folding it into the memoized struct would have exposed
/// it to a cross-GameObject Start()-order race it was previously immune to. <c>Clutch</c> was kept as
/// its own, deliberately non-memoized interface member instead. These tests assert that non-memoized
/// behavior directly, so a future change that folds it back into the cached struct fails here first.
/// </summary>
public class Level5ShooterActorClutchTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    [Test]
    public void AutoPlayerControllerReadsClutchLiveRatherThanMemoizingIt()
    {
        GameObject go = Spawn("cpu-actor");
        CharacterProfile profile = go.AddComponent<CharacterProfile>();
        profile.Clutch = 42;
        IShooterActor actor = go.AddComponent<AutoPlayerController>();

        Assert.That(actor.Clutch, Is.EqualTo(42));

        profile.Clutch = 77;

        Assert.That(
            actor.Clutch,
            Is.EqualTo(77),
            "Clutch must be read live - a memoized read (like ShooterAttributes) would still report 42 here");
    }

    [Test]
    public void PlayerControllerReadsClutchLiveRatherThanMemoizingIt()
    {
        GameObject go = Spawn("human-actor");
        CharacterProfile profile = go.AddComponent<CharacterProfile>();
        profile.Clutch = 10;
        IShooterActor actor = go.AddComponent<PlayerController>();

        Assert.That(actor.Clutch, Is.EqualTo(10));

        profile.Clutch = 99;

        Assert.That(actor.Clutch, Is.EqualTo(99));
    }

    [Test]
    public void ClutchIsZeroRatherThanThrowingWhenNoCharacterProfileIsPresent()
    {
        GameObject go = Spawn("actor-without-profile");
        IShooterActor actor = go.AddComponent<AutoPlayerController>();

        Assert.That(actor.Clutch, Is.EqualTo(0));
    }
}
