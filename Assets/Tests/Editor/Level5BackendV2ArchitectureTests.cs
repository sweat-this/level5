using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// Guard rails for the Backend V2 client boundary, the same idea as
    /// <c>Level5VersusArchitectureTests</c>: these properties are invisible in any single file, so
    /// they are asserted here rather than trusted to review.
    /// </summary>
    public class Level5BackendV2ArchitectureTests
    {
        private static readonly string ClientRoot =
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "backendv2");

        private static readonly string LegacyApiHelper = Path.Combine(
            Directory.GetCurrentDirectory(), "Assets", "Scripts", "RESTApi", "APIHelper.cs");

        /// <summary>
        /// The #159 UI/app-layer files that had to move out of <see cref="ClientRoot"/> into the
        /// default assembly (they need <c>LegacyGameOptionsBridge</c>, which has no assembly
        /// definition of its own - see the doc comment on <c>RemoteAttemptLauncher</c>). They are
        /// exactly as bound by the "typed clients only" boundary as everything under
        /// <see cref="ClientRoot"/>, so the raw-UnityWebRequest/legacy-APIHelper checks below cover
        /// them too, explicitly - <see cref="ClientFiles"/> alone would miss them entirely.
        /// </summary>
        private static readonly string CorrespondenceUiRoot =
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_multiplayer");

        private static readonly string[] CorrespondenceLauncherFiles =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "versus", "RemoteAttemptLauncher.cs"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "versus", "RemoteAttemptLaunch.cs"),
        };

        /// <summary>
        /// The general match-result files that, for the same reason as
        /// <see cref="CorrespondenceLauncherFiles"/> (they need <c>HighScoreModel</c>, which has no
        /// assembly definition of its own), live outside <see cref="ClientRoot"/> in the default
        /// assembly. <see cref="OrdinaryResultOwnershipNeverReadsLegacyGameOptionsIdentity"/> is the
        /// one check that specifically needs these - the raw-UnityWebRequest/legacy-APIHelper checks
        /// above only run over <see cref="ClientRoot"/> and would miss them entirely.
        /// </summary>
        private static readonly string[] MatchResultDefaultAssemblyFiles =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "backendv2", "BackendV2MatchResultAdapter.cs"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "backendv2", "BackendV2MatchResultSubmission.cs"),
        };

        /// <summary>
        /// The online-account screen's MonoBehaviour, which - like <see cref="MatchResultDefaultAssemblyFiles"/> -
        /// lives in the default assembly (it needs <c>MenuFooterUiObjects</c>/<c>UiSelectionAdapter</c>,
        /// which have no assembly definition of their own), so <see cref="ClientFiles"/> alone would
        /// miss it. <see cref="OnlineAccountControllerNeverUsesLocalProfileIdentity"/> is the one check
        /// that specifically needs this file.
        /// </summary>
        private static readonly string[] OnlineAccountControllerFiles =
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_login", "OnlineAccountController.cs"),
        };

        [Test]
        public void LegacyApiHelperHasNoBackendV2MethodsAddedToIt()
        {
            string text = StripComments(File.ReadAllText(LegacyApiHelper));

            Assert.That(text, Does.Not.Contain("BackendV2"));
            Assert.That(text, Does.Not.Contain("api/v2"));
        }

        [Test]
        public void NothingInTheClientReferencesIVersusSeriesRepository()
        {
            // IVersusSeriesRepository's own doc comment says it is not the remote seam: a remote
            // backend must be a separate typed client speaking narrow commands, never a repository
            // implementation that uploads or downloads a whole VersusSeries.
            List<string> offenders = new List<string>();

            foreach (string file in ClientFiles())
            {
                string text = StripComments(File.ReadAllText(file));
                if (text.Contains("IVersusSeriesRepository"))
                {
                    offenders.Add(Relative(file));
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "the Backend V2 client must not touch IVersusSeriesRepository:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void NothingInTheClientUsesSynchronousOrLegacyNetworkingApis()
        {
            List<string> offenders = new List<string>();
            Regex forbidden = new Regex(@"\b(HttpWebRequest|WebClient)\b");

            foreach (string file in ClientFiles())
            {
                string text = StripComments(File.ReadAllText(file));
                foreach (Match match in forbidden.Matches(text))
                {
                    offenders.Add($"{Relative(file)}: {match.Value}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }

        [Test]
        public void NothingInTheClientOrUiLayerIssuesARawUnityWebRequest()
        {
            // UnityWebRequestTransport is the one sanctioned adapter that issues a request.
            // Everything downstream of it - clients, the correspondence UI - must go through
            // BackendV2Runtime's typed clients instead, per issue #159's UI/app-layer boundary. This
            // targets request construction/sending specifically (new UnityWebRequest(...),
            // UnityWebRequest.Get/Post/Put/..., .SendWebRequest()), not the whole type name: static
            // helpers like UnityWebRequest.EscapeURL (used by PlayersApiClient to build a path
            // segment) are ordinary string utilities, not a bypass of the transport.
            List<string> offenders = new List<string>();
            Regex forbidden = new Regex(
                @"new\s+UnityWebRequest\s*\(|UnityWebRequest\.(Get|Post|Put|Delete|Head)\s*\(|\.SendWebRequest\s*\(");

            foreach (string file in ClientAndCorrespondenceUiFiles())
            {
                if (Path.GetFileName(file) == "UnityWebRequestTransport.cs")
                {
                    continue;
                }

                string text = StripComments(File.ReadAllText(file));
                foreach (Match match in forbidden.Matches(text))
                {
                    offenders.Add($"{Relative(file)}: {match.Value}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }

        [Test]
        public void NothingInTheClientOrUiLayerCallsLegacyApiHelper()
        {
            List<string> offenders = new List<string>();

            foreach (string file in ClientAndCorrespondenceUiFiles())
            {
                string text = StripComments(File.ReadAllText(file));
                if (text.Contains("APIHelper"))
                {
                    offenders.Add(Relative(file));
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "Backend V2 code must never call the legacy REST helper:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void TheClientNeverUploadsAWholeVersusSeries()
        {
            List<string> offenders = new List<string>();

            foreach (string file in ClientFiles())
            {
                string text = StripComments(File.ReadAllText(file));
                if (text.Contains("VersusSeriesDocument") || text.Contains(": VersusSeries")
                    || text.Contains("(VersusSeries "))
                {
                    offenders.Add(Relative(file));
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }

        [Test]
        public void OrdinaryResultOwnershipNeverReadsLegacyGameOptionsIdentity()
        {
            // Ownership of a pending general match result must come only from
            // BackendV2SessionStore.Current.PlayerId - never GameOptions.userid/userName, which
            // would let a player retroactively claim a match played without a Backend V2 identity,
            // or send an owner-mismatched result under the wrong local account.
            List<string> offenders = new List<string>();

            foreach (string file in ClientFiles())
            {
                CheckForGameOptionsIdentity(file, offenders);
            }

            foreach (string file in MatchResultDefaultAssemblyFiles)
            {
                CheckForGameOptionsIdentity(file, offenders);
            }

            Assert.That(
                offenders,
                Is.Empty,
                "Backend V2 general match-result ownership must never read GameOptions.userid/userName:\n"
                + string.Join("\n", offenders));
        }

        private static void CheckForGameOptionsIdentity(string file, List<string> offenders)
        {
            string text = StripComments(File.ReadAllText(file));
            if (text.Contains("GameOptions.userid") || text.Contains("GameOptions.userName"))
            {
                offenders.Add(Relative(file));
            }
        }

        [Test]
        public void OnlineAccountControllerNeverUsesLocalProfileIdentity()
        {
            // The online-account screen is a Backend V2 online-identity surface only - it must never
            // read or write local profile identity (GameOptions.userid/userName, which would blur it
            // with the player's local save-selection) or fall back to the legacy V1 account API
            // (APIHelper) or its UserModel, which would defeat the point of a dedicated V2 screen.
            List<string> offenders = new List<string>();

            foreach (string file in OnlineAccountControllerFiles)
            {
                string text = StripComments(File.ReadAllText(file));
                if (text.Contains("GameOptions.userid")
                    || text.Contains("GameOptions.userName")
                    || text.Contains("APIHelper")
                    || text.Contains("UserModel"))
                {
                    offenders.Add(Relative(file));
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "The online-account controller must never use local profile identity or the legacy "
                    + "account API:\n" + string.Join("\n", offenders));
        }

        private static IEnumerable<string> ClientFiles()
        {
            return Directory.EnumerateFiles(ClientRoot, "*.cs", SearchOption.AllDirectories);
        }

        private static IEnumerable<string> ClientAndCorrespondenceUiFiles()
        {
            foreach (string file in ClientFiles())
            {
                yield return file;
            }

            foreach (string file in Directory.EnumerateFiles(CorrespondenceUiRoot, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }

            foreach (string file in CorrespondenceLauncherFiles)
            {
                yield return file;
            }
        }

        private static string Relative(string path) => Level5TestSourceText.Relative(path);

        private static string StripComments(string text) => Level5TestSourceText.StripComments(text);
    }
}
