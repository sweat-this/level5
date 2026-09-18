using System;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// The re-entrancy guard <see cref="RemoteAttemptResultSubmitter"/> uses to stop
    /// <c>GameRules</c>' own match-end retry loop from firing a second, concurrent
    /// <c>CompleteAttempt</c> submission for the same attempt while a prior one is still in
    /// flight. Tested as a pure state transition (claim/release) rather than by driving the real
    /// coroutine, since <c>MonoBehaviour.StartCoroutine</c> does not advance in EditMode.
    /// </summary>
    public class Level5BackendV2RemoteAttemptSubmissionTests
    {
        [Test]
        public void ASecondClaimForTheSameAttemptIsRefusedUntilReleased()
        {
            Guid attemptId = Guid.NewGuid();

            Assert.That(RemoteAttemptResultSubmitter.TryClaim(attemptId), Is.True);
            Assert.That(
                RemoteAttemptResultSubmitter.TryClaim(attemptId),
                Is.False,
                "a concurrent claim for the same attempt must be refused while one is already in flight");

            RemoteAttemptResultSubmitter.Release(attemptId);

            Assert.That(
                RemoteAttemptResultSubmitter.TryClaim(attemptId),
                Is.True,
                "once released (e.g. after a failed submission), a later retry may claim it again");

            RemoteAttemptResultSubmitter.Release(attemptId);
        }

        [Test]
        public void ReleasingAnAttemptThatIsNotClaimedIsANoOp()
        {
            Assert.DoesNotThrow(() => RemoteAttemptResultSubmitter.Release(Guid.NewGuid()));
        }
    }
}
