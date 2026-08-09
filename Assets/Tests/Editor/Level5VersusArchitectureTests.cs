using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core.Versus;
using NUnit.Framework;

/// <summary>
/// Guard rails for the versus architecture.
///
/// The same idea as <c>Level5MatchArchitectureTests</c>: the properties that make this design work
/// are invisible in any single file, so they are asserted here rather than trusted to review. Each
/// one below is a boundary that would be cheap to cross by accident and expensive to notice.
/// </summary>
public class Level5VersusArchitectureTests
{
    private static readonly string DomainRoot =
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Level5", "Core", "Versus");

    private static readonly string ScriptsRoot =
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts");

    /// <summary>
    /// The only files allowed to touch the serialization documents.
    ///
    /// The documents are the one road round <see cref="VersusGame.ViewFor"/>: a screen holding a
    /// <c>VersusSeriesDocument</c> can read the opponent's score straight out of it. Storage needs
    /// them; nothing else does.
    /// </summary>
    private static readonly HashSet<string> PersistenceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "FileVersusSeriesRepository.cs"
    };

    [Test]
    public void TheDomainHasNoSceneDependencies()
    {
        // A series has to be constructible, playable and resolvable with no scene loaded at all -
        // that is what lets a correspondence turn be taken on a document read off disk. A single
        // MonoBehaviour or GameObject in here would end that.
        List<string> offenders = new List<string>();
        Regex sceneTypes = new Regex(@"\b(MonoBehaviour|GameObject|Transform|Component|Coroutine|SceneManager)\b");

        foreach (string file in DomainFiles())
        {
            string text = StripComments(File.ReadAllText(file));
            foreach (Match match in sceneTypes.Matches(text))
            {
                offenders.Add($"{Relative(file)}: {match.Value}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "the versus domain must stay constructible without a scene:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void TheDomainHasNoNetworkingAndNoFileSystem()
    {
        // Networking becomes an implementation of IVersusSeriesRepository, never something the
        // domain reaches for. The same goes for the file system: a domain that knows where it is
        // stored cannot be stored anywhere else.
        List<string> offenders = new List<string>();
        Regex infrastructure = new Regex(
            @"\b(UnityWebRequest|HttpClient|WebClient|System\.Net|System\.IO|File\.|Directory\.|Application\.persistentDataPath)\b");

        foreach (string file in DomainFiles())
        {
            // The serializer is the boundary: it turns a series into text. It still must not decide
            // where that text goes, which the pattern above checks.
            foreach (Match match in infrastructure.Matches(StripComments(File.ReadAllText(file))))
            {
                offenders.Add($"{Relative(file)}: {match.Value}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "the versus domain must not know where or how it is stored:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void TheDomainDoesNotReachIntoTheLegacyGlobals()
    {
        List<string> offenders = DomainFiles()
            .Where(file => StripComments(File.ReadAllText(file)).Contains("GameOptions."))
            .Select(Relative)
            .ToList();

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    [Test]
    public void OnlyStorageTouchesTheSerializationDocuments()
    {
        // ViewFor is the sealed-attempt guarantee. It only holds if nothing outside storage can
        // pick up the raw document and read the opponent's result out of it.
        List<string> offenders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains("~") || PersistenceFiles.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            string text = StripComments(File.ReadAllText(file));
            if (text.Contains("VersusSeriesDocument") || text.Contains("VersusAttemptResultDocument"))
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "read a series through VersusSeries.ViewFor, not through its stored document:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void AGameNeverHandsOutItsAttempts()
    {
        // If a public member returned the attempts, every sealed-attempt test above would be
        // checking a convention rather than a property of the type.
        foreach (PropertyInfo property in typeof(VersusGame).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.That(
                typeof(Attempt).IsAssignableFrom(property.PropertyType),
                Is.False,
                $"VersusGame.{property.Name} exposes an attempt");

            Assert.That(
                property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericArguments().Any(argument => argument == typeof(Attempt)),
                Is.False,
                $"VersusGame.{property.Name} exposes a collection of attempts");
        }

        foreach (MethodInfo method in typeof(VersusGame).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.That(
                typeof(Attempt).IsAssignableFrom(method.ReturnType),
                Is.False,
                $"VersusGame.{method.Name} returns an attempt");
        }
    }

    [Test]
    public void TheParticipantViewCannotBeAskedToShowEverything()
    {
        // A "reveal anyway" parameter would move the guarantee back into whoever calls it.
        MethodInfo viewFor = typeof(VersusGame).GetMethod(nameof(VersusGame.ViewFor));

        Assert.That(viewFor, Is.Not.Null);
        Assert.That(
            viewFor.GetParameters().Any(parameter => parameter.ParameterType == typeof(bool)),
            Is.False,
            "ViewFor must not take a flag that changes what it is willing to show");
    }

    [Test]
    public void CompetitiveStateHasNoPublicSetters()
    {
        // series.Status = SeriesStatus.Completed from an unrelated system is exactly the failure
        // this architecture exists to prevent. Every change goes through an operation that checks
        // its own invariants.
        AssertNoPublicSetters(typeof(VersusSeries));
        AssertNoPublicSetters(typeof(VersusGame));
        AssertNoPublicSetters(typeof(Attempt));
        AssertNoPublicSetters(typeof(SeriesResult));
        AssertNoPublicSetters(typeof(GameResult));
        AssertNoPublicSetters(typeof(AttemptResult));
        AssertNoPublicSetters(typeof(CompetitiveRuleset));
        AssertNoPublicSetters(typeof(SeriesSnapshot));
    }

    [Test]
    public void TheGameplayFootprintIsOneCall()
    {
        // The whole of versus inside gameplay is one line in the match-end retry loop. If this grows,
        // the competitive system has started leaking into the modes it is supposed to sit above.
        string gameRules = StripComments(
            File.ReadAllText(Path.Combine(ScriptsRoot, "game manager", "GameRules.cs")));

        Assert.That(
            Regex.Matches(gameRules, @"\bVersus\w*\.").Count,
            Is.EqualTo(1),
            "GameRules should touch the versus system exactly once, through VersusMatchReporter");

        Assert.That(gameRules, Does.Contain("VersusMatchReporter.TryReport"));
        Assert.That(gameRules, Does.Not.Contain("VersusSeries"), "gameplay must not know what a series is");
        Assert.That(gameRules, Does.Not.Contain("SeriesId"));
    }

    [Test]
    public void NoModeSpecificBranchingLivesInTheDomain()
    {
        // Adding a versus-capable mode must never mean editing a switch in the coordinator. The one
        // place that names modes is the ruleset registry, which is authored data.
        List<string> offenders = new List<string>();

        foreach (string file in DomainFiles())
        {
            string text = StripComments(File.ReadAllText(file));
            foreach (Match match in Regex.Matches(text, @"GameModeId\.(?<mode>\w+)"))
            {
                // GameModeId.None is the "unknown mode" case, not a decision about a mode.
                if (match.Groups["mode"].Value != "None")
                {
                    offenders.Add($"{Relative(file)}: {match.Value}");
                }
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "the versus domain must not name individual game modes:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void TheDomainDoesNotInventItsOwnClockOrIds()
    {
        // Both are injected so a correspondence delay can be tested and so a server can later issue
        // ids. A direct DateTime.UtcNow or Guid.NewGuid would quietly opt out of both.
        List<string> offenders = new List<string>();

        foreach (string file in DomainFiles())
        {
            // VersusServices.cs is where the real implementations live; that is the point of it.
            if (Path.GetFileName(file) == "VersusServices.cs")
            {
                continue;
            }

            string text = StripComments(File.ReadAllText(file));
            foreach (Match match in Regex.Matches(text, @"DateTime\.(UtcNow|Now)|Guid\.NewGuid"))
            {
                offenders.Add($"{Relative(file)}: {match.Value}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "take the time and new ids from IVersusClock / IVersusIdSource:\n" + string.Join("\n", offenders));
    }

    private static void AssertNoPublicSetters(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            MethodInfo setter = property.GetSetMethod(false);
            Assert.That(
                setter,
                Is.Null,
                $"{type.Name}.{property.Name} can be set from anywhere; competitive state must change "
                + "through an operation that enforces its own invariants");
        }
    }

    private static IEnumerable<string> DomainFiles()
    {
        return Directory.EnumerateFiles(DomainRoot, "*.cs", SearchOption.AllDirectories);
    }

    private static string Relative(string path)
    {
        return path.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }
}
