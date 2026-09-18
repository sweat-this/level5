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

        private static IEnumerable<string> ClientFiles()
        {
            return Directory.EnumerateFiles(ClientRoot, "*.cs", SearchOption.AllDirectories);
        }

        private static string Relative(string path) => Level5TestSourceText.Relative(path);

        private static string StripComments(string text) => Level5TestSourceText.StripComments(text);
    }
}
