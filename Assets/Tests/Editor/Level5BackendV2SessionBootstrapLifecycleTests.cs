using System;
using System.IO;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Guards for promoting persisted Backend V2 session restoration from correspondence-only
    /// initialization to the normal application startup path.
    ///
    /// These are source-text checks, the same convention as <see cref="Level5BackendV2ArchitectureTests"/>
    /// and the other asmdef-free architecture guards under this folder (<see cref="Level5TestSourceText"/>).
    /// They exist because none of these properties are safe to prove by actually calling
    /// <c>BackendV2SessionPersistenceBootstrap.EnsureInitialized()</c> from a shared EditMode test
    /// process: it subscribes to <c>BackendV2SessionStore.Changed</c> with no unsubscribe, so a direct
    /// call here would leave every later test in the same batch silently writing/reading a real
    /// session file under <c>Application.persistentDataPath</c> - precisely the failure mode that is
    /// already documented as the reason this bootstrap is not a
    /// <c>[RuntimeInitializeOnLoadMethod]</c>. See
    /// <c>Assets/Tests/PlayMode/BackendV2SessionRestorationPlayModeTests.cs</c> for the isolated,
    /// process-level behavioral proof this class deliberately does not attempt.
    /// </summary>
    public class Level5BackendV2SessionBootstrapLifecycleTests
    {
        private static readonly string BootstrapSource = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "backendv2", "Level5BackendV2",
            "Session", "BackendV2SessionPersistenceBootstrap.cs");

        private static readonly string UserAccountManagerSource = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "account", "UserAccountManager.cs");

        private static readonly string CorrespondenceScreenControllerSource = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_multiplayer",
            "CorrespondenceScreenController.cs");

        private static readonly string SessionRoot = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "backendv2", "Level5BackendV2", "Session");

        [Test]
        public void TheProductionStartupPathInvokesTheBootstrap()
        {
            // UserAccountManager.Awake is the earliest ordinary production composition seam: the
            // build's first scene is level_00_account_loginLocal, which UserAccountManager owns.
            string text = StripComments(File.ReadAllText(UserAccountManagerSource));
            int awakeIndex = text.IndexOf("void Awake()", StringComparison.Ordinal);
            Assert.That(awakeIndex, Is.GreaterThanOrEqualTo(0), "UserAccountManager.Awake() not found");

            string awakeBody = ExtractMethodBody(text, awakeIndex);
            Assert.That(
                awakeBody, Does.Contain("BackendV2SessionPersistenceBootstrap.EnsureInitialized()"),
                "UserAccountManager.Awake must call BackendV2SessionPersistenceBootstrap.EnsureInitialized() " +
                "so a persisted Backend V2 session is restored application-wide, not only after a player " +
                "opens Multiplayer.");
        }

        [Test]
        public void CorrespondenceStillUsesTheSameIdempotentBootstrapAsAFallback()
        {
            string text = StripComments(File.ReadAllText(CorrespondenceScreenControllerSource));
            int awakeIndex = text.IndexOf("void Awake()", StringComparison.Ordinal);
            Assert.That(awakeIndex, Is.GreaterThanOrEqualTo(0), "CorrespondenceScreenController.Awake() not found");

            string awakeBody = ExtractMethodBody(text, awakeIndex);
            Assert.That(
                awakeBody, Does.Contain("BackendV2SessionPersistenceBootstrap.EnsureInitialized()"),
                "the correspondence screen must keep calling EnsureInitialized() itself: idempotency makes " +
                "this a no-op in normal production (UserAccountManager already ran it), but it is still the " +
                "correct fallback for any path that opens the correspondence screen without ever going " +
                "through the local-account scene (e.g. a direct-scene test/dev entry point).");
        }

        [Test]
        public void TheBootstrapNoLongerPerformsAnEagerForceRefresh()
        {
            string text = StripComments(File.ReadAllText(BootstrapSource));

            Assert.That(
                text, Does.Not.Contain("ForceRefresh"),
                "application startup must not perform a network request merely to restore a locally " +
                "persisted session - a temporary Backend V2 outage at launch must not be able to delete an " +
                "otherwise locally valid session. Remote validation belongs to the request-time refresh " +
                "path (AuthenticatedApiClientBase / BackendV2SessionManager), not to restoration.");
            Assert.That(
                text, Does.Not.Contain("RefreshRestoredSession"),
                "the bootstrap-owned eager refresh coroutine must be removed, not merely renamed");
            Assert.That(
                text, Does.Not.Contain("StartCoroutine"),
                "the bootstrap must perform zero network requests: it should have no coroutine of its own " +
                "at all, only a synchronous local disk read (TryLoad) and an in-memory Set");
        }

        [Test]
        public void NoBackendV2SessionInitializationUsesRuntimeInitializeOnLoadMethod()
        {
            // Repository history (see the bootstrap's own doc comment) established that
            // [RuntimeInitializeOnLoadMethod] breaks EditMode test isolation for this subsystem:
            // Unity's batchmode EditMode runner still fires it, and Application.isPlaying does not
            // reliably read false while that happens. Production initialization must stay an explicit
            // call from a real scene-composition seam.
            foreach (string file in Directory.EnumerateFiles(SessionRoot, "*.cs", SearchOption.AllDirectories))
            {
                string text = StripComments(File.ReadAllText(file));
                Assert.That(
                    text, Does.Not.Contain("RuntimeInitializeOnLoadMethod"),
                    Level5TestSourceText.Relative(file) +
                    " must not use [RuntimeInitializeOnLoadMethod] for Backend V2 session initialization");
            }
        }

        /// <summary>Finds the <c>{ ... }</c> body immediately following the method signature at
        /// <paramref name="methodStartIndex"/> by brace counting - simple and sufficient for these
        /// small, single-purpose Awake() methods, and avoids the false negatives a naive
        /// "call appears anywhere in the file" check would let through if some other method in the
        /// same file happened to mention the same string.</summary>
        private static string ExtractMethodBody(string text, int methodStartIndex)
        {
            int openBrace = text.IndexOf('{', methodStartIndex);
            Assert.That(openBrace, Is.GreaterThanOrEqualTo(0));

            int depth = 0;
            for (int i = openBrace; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(openBrace, i - openBrace + 1);
                    }
                }
            }

            Assert.Fail("unterminated method body");
            return null;
        }

        private static string StripComments(string text) => Level5TestSourceText.StripComments(text);
    }
}
