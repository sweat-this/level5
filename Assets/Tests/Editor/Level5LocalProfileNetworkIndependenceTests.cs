using System;
using System.Collections.Generic;
using System.IO;
using Level5.BackendV2;
using Level5.Core;
using NUnit.Framework;

/// <summary>
/// Guards the two halves of the V1 account/auth retirement's identity boundary (spec: "local and
/// Backend V2 identities remain independent"):
///
/// 1. Local profile/account code (<c>Assets/Scripts/account/*.cs</c>, <c>AccountManager.cs</c>) must
///    never call the retired V1 account transport (<c>APIHelper.PostToken</c>/<c>PostUser</c>/
///    <c>UserNameExists</c>/<c>GetUserByUserName</c>/<c>HasSession</c>/<c>BearerToken</c>) or reference
///    Backend V2 at all - selecting/creating a local profile or continuing as guest must be a zero-
///    network operation. Mirrors <see cref="Level5.BackendV2.Tests.Level5BackendV2ArchitectureTests"/>'s
///    text-scan pattern, in the reverse direction.
/// 2. A local profile/guest switch must never touch an existing Backend V2 session, and vice versa -
///    already covered from the Backend V2 side by
///    <see cref="Level5.BackendV2.Tests.Level5BackendV2OnlineAccountCoordinatorTests"/>'s
///    <c>AssertLocalIdentityUnchanged</c>; this covers the other direction directly.
/// </summary>
public class Level5LocalProfileNetworkIndependenceTests
{
    private static readonly string AccountFolder =
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "account");

    private static readonly string AccountManagerFile = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_login", "AccountManager.cs");

    private static readonly string[] RetiredApiHelperSymbols =
    {
        "APIHelper.PostToken",
        "APIHelper.PostUser",
        "APIHelper.UserExists",
        "APIHelper.UserNameExists",
        "APIHelper.EmailExists",
        "APIHelper.GetUserByUserName",
        "APIHelper.HasSession",
        "APIHelper.BearerToken",
        "APIHelper.ClearSession",
    };

    [Test]
    public void LocalProfileCodeNeverCallsRetiredV1AccountTransport()
    {
        List<string> offenders = new List<string>();

        foreach (string file in LocalProfileFiles())
        {
            string text = Level5TestSourceText.StripComments(File.ReadAllText(file));
            foreach (string symbol in RetiredApiHelperSymbols)
            {
                if (text.Contains(symbol))
                {
                    offenders.Add(Level5TestSourceText.Relative(file) + " : " + symbol);
                }
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "local profile selection/creation/guest must be zero-network - no retired V1 account call may remain:\n"
                + string.Join("\n", offenders));
    }

    [Test]
    public void LocalProfileCodeNeverTouchesBackendV2SIdentityOrSession()
    {
        // UserAccountManager.Awake legitimately calls BackendV2SessionPersistenceBootstrap.EnsureInitialized()
        // - an idempotent, zero-network app-startup call that never reads/writes local identity or the
        // Backend V2 session itself (see its own doc comment). That one sanctioned call aside, local
        // profile code must never touch the online session/identity types directly.
        List<string> offenders = new List<string>();

        foreach (string file in LocalProfileFiles())
        {
            string text = Level5TestSourceText.StripComments(File.ReadAllText(file));
            text = text.Replace("BackendV2SessionPersistenceBootstrap.EnsureInitialized", string.Empty);

            if (text.Contains("BackendV2SessionStore")
                || text.Contains("BackendV2Runtime")
                || text.Contains("BackendV2Session "))
            {
                offenders.Add(Level5TestSourceText.Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "local profile identity must stay independent of Backend V2's online identity/session:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> LocalProfileFiles()
    {
        foreach (string file in Directory.EnumerateFiles(AccountFolder, "*.cs", SearchOption.AllDirectories))
        {
            yield return file;
        }

        yield return AccountManagerFile;
    }

    [TearDown]
    public void TearDown()
    {
        BackendV2SessionStore.Clear();
    }

    [Test]
    public void SwitchingLocalProfileLeavesAnExistingBackendV2SessionUnchanged()
    {
        BackendV2Session existing = new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30));
        BackendV2SessionStore.Set(existing);

        // Exactly what LocalAccount.LoginButton/UserAccountManager.LoginButton do now: set the local
        // selection directly, with no APIHelper/BackendV2 call in between.
        LocalAccountIdentity.UserId = 99;
        LocalAccountIdentity.UserName = "someone-else";

        Assert.That(BackendV2SessionStore.Current, Is.SameAs(existing));
    }

    [Test]
    public void SelectingGuestLeavesAnExistingBackendV2SessionUnchanged()
    {
        BackendV2Session existing = new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
            DateTimeOffset.UtcNow.AddDays(30));
        BackendV2SessionStore.Set(existing);

        // Exactly what LocalAccount.LoginAsGuest/UserAccountManager.ContinueButton do now.
        LocalAccountIdentity.UserId = UserAccountManager.GuestUserid;
        LocalAccountIdentity.UserName = UserAccountManager.GuestUsername;

        Assert.That(BackendV2SessionStore.Current, Is.SameAs(existing));
    }
}
