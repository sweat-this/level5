using NUnit.Framework;

/// <summary>
/// Installs the real production <see cref="MatchRuntime"/> legacy-fallback reader once for the whole
/// EditMode test assembly.
///
/// <c>GameOptions</c>' own <c>RuntimeInitializeOnLoadMethod(BeforeSceneLoad)</c> bootstrap is what
/// installs this in a Player or Play Mode - see <see cref="MatchRuntime.InstallLegacyFallbackReader"/>
/// - but <c>RuntimeInitializeOnLoadMethod</c> does not fire for EditMode tests (Unity's docs are
/// explicit about this - it is a Player/Play Mode load event only). Without an assembly-wide install,
/// any fixture that exercises <c>MatchRuntime</c>'s direct-scene fallback (<c>ActiveMatch</c>
/// unconfigured) would hit the explicit "no fallback reader installed" composition error - not
/// because the seam is broken, but because nothing in this process ever wired it.
///
/// This mirrors production exactly rather than replacing it with a test fake: same reader
/// (<c>GameOptions</c>'s private snapshot builder, reached through reflection since nothing else in
/// production needs it public), same installation call. A fixture that specifically needs to test the
/// missing-reader path resets and restores this around that one test.
/// </summary>
[SetUpFixture]
public class Level5MatchRuntimeFallbackTestBootstrap
{
    [OneTimeSetUp]
    public void InstallProductionFallbackReader()
    {
        MatchRuntime.InstallLegacyFallbackReader(Level5MatchRuntimeFallbackTestSupport.ProductionReader());
    }

    [OneTimeTearDown]
    public void ResetFallbackReader()
    {
        MatchRuntime.ResetLegacyFallbackReader();
    }
}
