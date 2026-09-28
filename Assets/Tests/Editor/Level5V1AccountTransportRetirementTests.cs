using System.Reflection;
using Assets.Scripts.restapi;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Regression guard for retiring the legacy V1 account/authentication transport in favor of fully
    /// local profiles (DBHelper.CreateLocalProfile) and Backend V2 as the sole online-account system
    /// (see docs/persistence-boundaries.md). Reflection is used instead of a source-text scan (contrast
    /// <see cref="Level5LocalProfileNetworkIndependenceTests"/>) so this cannot be defeated by a comment
    /// or a doc reference still naming these symbols - if any of them exist at all, production code
    /// could call them again. Mirrors <see cref="Level5V1ScoreTransportRetirementTests"/>'s exact
    /// pattern for the account/auth surface instead of the score/leaderboard one.
    /// </summary>
    public class Level5V1AccountTransportRetirementTests
    {
        private const BindingFlags AnyDeclared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        [Test]
        public void ApiHelperNoLongerExposesTheRetiredAccountAuthMethods()
        {
            string[] retiredMethods =
            {
                "PostUser",
                "UserExists",
                "UserNameExists",
                "EmailExists",
                "GetUserByUserName",
                "PostToken",
                "ClearSession",
            };

            foreach (string name in retiredMethods)
            {
                Assert.That(
                    typeof(APIHelper).GetMethod(name, AnyDeclared), Is.Null,
                    "APIHelper." + name + " is retired V1 account/auth transport and must not exist.");
            }
        }

        [Test]
        public void ApiHelperNoLongerExposesTheRetiredSessionMachinery()
        {
            Assert.That(
                typeof(APIHelper).GetProperty("HasSession", AnyDeclared), Is.Null,
                "APIHelper.HasSession is retired V1 session state and must not exist.");
            Assert.That(
                typeof(APIHelper).GetProperty("BearerToken", AnyDeclared), Is.Null,
                "APIHelper.BearerToken is retired V1 session state and must not exist.");
            Assert.That(
                typeof(APIHelper).GetField("bearerToken", AnyDeclared), Is.Null,
                "APIHelper.bearerToken is retired V1 session state and must not exist.");
        }

        [Test]
        public void ApiHelperNoLongerHasAnAuthenticatedParameterAnywhere()
        {
            // The `authenticated` bool became dead configurability once every remaining caller
            // (PostReport, GetLatestBuildVersion, GetServerMessages) always passed false - removed
            // from ResourceExists/GetJson/GetText/SendJson rather than left as false-only plumbing.
            foreach (MethodInfo method in typeof(APIHelper).GetMethods(AnyDeclared))
            {
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Assert.That(
                        parameter.Name, Is.Not.EqualTo("authenticated"),
                        "APIHelper." + method.Name + " still has an 'authenticated' parameter - it should have been removed as dead configurability.");
                }
            }
        }

        [Test]
        public void ApiHelperNoLongerExposesTheRetiredAccountEndpointConstants()
        {
            string[] retired =
            {
                "API_ADDRESS_DEV_publicApi",
                "API_ADDRESS_DEV_publicApiUsers",
                "API_ADDRESS_DEV_publicApiUsersByUserid",
                "API_ADDRESS_DEV_publicApiUsersByUserName",
                "API_ADDRESS_DEV_publicApiUsersByEmail",
                "API_ADDRESS_DEV_publicApiToken",
            };

            foreach (string name in retired)
            {
                Assert.That(
                    typeof(Constants).GetField(name, AnyDeclared), Is.Null,
                    "Constants." + name + " is a retired V1 account/auth endpoint and must not exist.");
            }
        }

        [Test]
        public void AccountManagerNoLongerExposesTheRetiredExistingAccountLoginSurface()
        {
            string[] retiredMethods = { "checkEmailAddressFormat", "checkUserName", "LoginUser" };

            foreach (string name in retiredMethods)
            {
                Assert.That(
                    typeof(AccountManager).GetMethod(name, AnyDeclared), Is.Null,
                    "AccountManager." + name + " is retired V1 account/login UI and must not exist.");
            }

            Assert.That(
                typeof(AccountManager).GetProperty("LoginExistingButtonName", AnyDeclared), Is.Null,
                "AccountManager.LoginExistingButtonName is retired - Login Existing is no longer player-facing.");
        }
    }
}
