using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-012 Phase 4 Slices 72-73 permanent guards over <c>PlayerController</c>'s, <c>AutoPlayerController</c>'s
/// and <c>AutoPlayerDefense</c>'s locomotion, all source-text scans (no scene/composition needed) using
/// <see cref="Level5TestSourceText.StripComments"/> (the shared stripper this repo's other
/// architecture-guard tests already use) so historical explanatory comments documenting this migration
/// do not trip either guard - only actual code does:
///
/// - <b>No executable <c>MovePosition</c> call.</b> All three moved their ordinary locomotion
///   from <c>Rigidbody.MovePosition</c> to <see cref="RigidbodyLocomotionMotor"/> - see that type's doc
///   comment for why a dynamic, non-kinematic body should not be position-driven. Scoped to exactly
///   these three files, not the whole repository: Phase 4's remaining roles (<c>EnemyController</c>,
///   <c>BodyGuardController</c>, <c>RacingVehicleController</c>, <c>RacingCinderBlock</c>) still
///   legitimately drive locomotion through <c>MovePosition</c> and are deferred to later slices - see
///   the Phase 4 section of <c>docs/systems-restructure-plan.md</c>.
/// - <b><c>AutoPlayerController</c> sets <c>arrivedAtTarget = true</c> in exactly one place.</b> Code
///   review finding on this slice: <c>AutoPlayerController</c> has two independent arrival-detection
///   sites (<c>Update</c> and <c>FixedUpdate</c> - <c>Grounded</c> can differ between the two, so either
///   can be the one that observes <c>distanceToTarget &lt;= 0.05f</c> first). Both must release the
///   planar velocity the last <c>moveToPosition</c> call commanded, via <c>ApplyArrivalTransition()</c> -
///   a bare <c>arrivedAtTarget = true</c> at either site leaves the Rigidbody sliding at its last
///   commanded velocity forever, since <c>arrivedAtTarget</c> already true also blocks the other site's
///   own arrival check from ever running to correct it.
/// </summary>
public class Level5LocomotionRatchetTests
{
    private static readonly string PlayerControllerPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "player", "Level5Player", "PlayerController.cs");

    private static readonly string AutoPlayerControllerPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "player", "Level5Player", "AutoPlayerController.cs");

    private static readonly string AutoPlayerDefensePath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "player", "Level5Player", "AutoPlayerDefense.cs");

    private static readonly Regex MovePositionCall = new Regex(@"\.\s*MovePosition\s*\(");

    [Test]
    public void PlayerControllerHasNoExecutableMovePositionCall()
    {
        AssertNoExecutableMovePosition(PlayerControllerPath);
    }

    [Test]
    public void AutoPlayerControllerHasNoExecutableMovePositionCall()
    {
        AssertNoExecutableMovePosition(AutoPlayerControllerPath);
    }

    [Test]
    public void AutoPlayerDefenseHasNoExecutableMovePositionCall()
    {
        AssertNoExecutableMovePosition(AutoPlayerDefensePath);
    }

    private static void AssertNoExecutableMovePosition(string path)
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(path));

        Assert.That(
            MovePositionCall.IsMatch(text),
            Is.False,
            Level5TestSourceText.Relative(path)
                + " must drive ordinary locomotion through RigidbodyLocomotionMotor, not "
                + "Rigidbody.MovePosition - see AUD-012 Phase 4 Slices 72-73.");
    }

    [Test]
    public void AutoPlayerControllerSetsArrivedAtTargetOnlyInsideTheSharedArrivalTransition()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(AutoPlayerControllerPath));

        int rawAssignments = Regex.Matches(text, @"\barrivedAtTarget\s*=\s*true\b").Count;

        Assert.That(
            rawAssignments,
            Is.EqualTo(1),
            "AutoPlayerController.cs must set arrivedAtTarget = true in exactly one place - inside "
                + "ApplyArrivalTransition(), which also releases planar velocity - not directly at an "
                + $"arrival-detection call site. Found {rawAssignments} raw assignment(s).");
    }
}
