using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-012 Phase 4 Slice 75: <c>BodyGuardController</c> still drives its ordinary locomotion
/// (<c>pursuePlayer</c>/<c>returnToPatrol</c>/<c>MoveToward</c>) through <c>Rigidbody.MovePosition</c>,
/// unlike the four roles <see cref="Level5LocomotionRatchetTests"/> already migrated. That remains
/// correct rather than a defect this slice must fix: <c>MovePosition</c> is Unity's documented way to
/// move a <b>kinematic</b> Rigidbody, and every authored bodyguard prefab's Rigidbody is kinematic (see
/// <c>bodyguard_ian.prefab</c>) - the same "kinematic + MovePosition is a legitimate movement model" rule
/// <see cref="RigidbodyLocomotionMotor"/>'s own doc comment states for the dynamic roles it does serve.
///
/// This class does not test runtime behavior - it is a source/asset guard, in the same source-text-scan
/// style <see cref="Level5LocomotionRatchetTests"/> and <see cref="Level5BasketballRuntimeIdentityGuardTests"/>
/// already use, over the authored prefab's serialized YAML. Its purpose is narrow and one-directional: if
/// prefab authoring ever changes this Rigidbody to dynamic (non-kinematic) while <c>BodyGuardController</c>
/// still calls <c>MovePosition</c>, that combination becomes exactly the defect Phase 4 exists to remove
/// (a dynamic body being position-driven), and this test must fail to force a re-audit. It intentionally
/// does not assert anything about <c>MovePosition</c> call sites in <c>BodyGuardController.cs</c> itself -
/// unlike the four migrated roles, that call remains legitimate here for as long as the authored
/// Rigidbody stays kinematic.
/// </summary>
public class Level5BodyGuardLocomotionExceptionTests
{
    private static readonly string BodyGuardIanPrefabPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Resources", "Prefabs", "bodyguards", "bodyguard_ian.prefab");

    /// <summary>BodyGuardController.cs's own .meta guid - the component's identity in prefab YAML.</summary>
    private const string BodyGuardControllerScriptGuid = "628079a4104bcfe4ea01dd2f1aae28bd";

    [Test]
    public void BodyGuardIanPrefabCarriesBodyGuardController()
    {
        string text = File.ReadAllText(BodyGuardIanPrefabPath);

        Assert.That(
            Regex.IsMatch(text, $@"guid: {BodyGuardControllerScriptGuid}"),
            Is.True,
            Level5TestSourceText.Relative(BodyGuardIanPrefabPath)
                + " must carry a BodyGuardController component for this exception to apply to it.");
    }

    [Test]
    public void BodyGuardIanPrefabHasExactlyOneRigidbody()
    {
        string text = File.ReadAllText(BodyGuardIanPrefabPath);

        int rigidbodyCount = Regex.Matches(text, @"^Rigidbody:\s*$", RegexOptions.Multiline).Count;

        Assert.That(
            rigidbodyCount,
            Is.EqualTo(1),
            Level5TestSourceText.Relative(BodyGuardIanPrefabPath)
                + " must carry exactly one Rigidbody - this guard's kinematic check below assumes a "
                + $"single, unambiguous Rigidbody block. Found {rigidbodyCount}.");
    }

    /// <summary>
    /// AUD-012 Phase 4's exception for this controller depends entirely on this fact staying true. If a
    /// future authoring change flips this Rigidbody to dynamic, <c>BodyGuardController</c>'s continued
    /// <c>MovePosition</c> calls become the exact defect Phase 4 targets, and this must fail loudly
    /// rather than let that combination land silently.
    /// </summary>
    [Test]
    public void BodyGuardIanPrefabRigidbodyIsKinematic()
    {
        string text = File.ReadAllText(BodyGuardIanPrefabPath);

        Match rigidbodyBlock = Regex.Match(text, @"^Rigidbody:\s*$.*?(?=^--- )", RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.That(rigidbodyBlock.Success, Is.True,
            Level5TestSourceText.Relative(BodyGuardIanPrefabPath) + " must carry a Rigidbody block.");

        Match kinematicField = Regex.Match(rigidbodyBlock.Value, @"m_IsKinematic:\s*(\d)");
        Assert.That(kinematicField.Success, Is.True,
            "the Rigidbody block must declare m_IsKinematic.");
        Assert.That(
            kinematicField.Groups[1].Value,
            Is.EqualTo("1"),
            "AUD-012 Phase 4: BodyGuardController's continued use of Rigidbody.MovePosition is classified "
                + "as an intentional exception only because this authored Rigidbody is kinematic. If this "
                + "ever becomes dynamic (non-kinematic), BodyGuardController's locomotion needs migrating "
                + "to an explicit Rigidbody velocity/motor command instead - see the Phase 4 section of "
                + "docs/systems-restructure-plan.md.");
    }
}
