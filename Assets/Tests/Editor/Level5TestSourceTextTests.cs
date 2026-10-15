using NUnit.Framework;

/// <summary>
/// Covers <see cref="Level5TestSourceText.StripCommentsAndLiterals"/>, the source normalizer the
/// asmdef-free architecture guards scan through. A defect here does not fail those guards - it makes
/// them quietly stop seeing part of a file, so the behaviour is pinned directly rather than left to
/// be inferred from a passing guard.
///
/// The first two tests are the regression pair: each asserts the failure mode of one regex ordering,
/// and no ordering of the two regex passes this replaced could satisfy both at once.
/// </summary>
public class Level5TestSourceTextTests
{
    [Test]
    public void ApostropheInACommentDoesNotSwallowFollowingCode()
    {
        // The live shape: prose apostrophes in doc comments, with real code between them. Stripping
        // literals before comments paired these two apostrophes and deleted MatchRuntime with them.
        string source =
            "/// The controller's rim vector.\n"
            + "private MatchRuntime runtime;\n"
            + "/// It doesn't bind before Start.\n"
            + "private SniperManager sniper;\n";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Contain("MatchRuntime"));
        Assert.That(stripped, Does.Contain("SniperManager"));
        Assert.That(stripped, Does.Not.Contain("controller"));
    }

    [Test]
    public void UrlInsideAStringLiteralDoesNotTruncateTheRestOfTheFile()
    {
        // The hazard the old ordering existed to avoid (Constants.cs's API addresses): the // inside
        // the string must not be read as the start of a comment.
        string source =
            "public const string Api = \"https://example.com/path\";\n"
            + "private GameLevelManager level;\n";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Contain("GameLevelManager"));
        Assert.That(stripped, Does.Not.Contain("example.com"));
    }

    [Test]
    public void StringAndCharLiteralContentsAreRemoved()
    {
        string source = "Debug.LogError(\"AutoPlayerController missing\"); char c = 'x'; int keep = 1;";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Not.Contain("AutoPlayerController"));
        Assert.That(stripped, Does.Contain("LogError"));
        Assert.That(stripped, Does.Contain("keep"));
    }

    [Test]
    public void EscapedQuoteDoesNotEndAStringLiteral()
    {
        string source = "string s = \"a \\\" PlayerHealth\"; int keep = 1;";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Not.Contain("PlayerHealth"));
        Assert.That(stripped, Does.Contain("keep"));
    }

    [Test]
    public void VerbatimStringTreatsDoubledQuoteAsContentNotTerminator()
    {
        string source = "string s = @\"a \"\" PlayerDunk\"; int keep = 1;";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Not.Contain("PlayerDunk"));
        Assert.That(stripped, Does.Contain("keep"));
    }

    [Test]
    public void BlockCommentIsRemovedWithoutFusingTheTokensAroundIt()
    {
        string source = "int a;/* CharacterProfile */int b;";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Not.Contain("CharacterProfile"));
        Assert.That(stripped, Does.Contain("int a; int b;"));
    }

    [Test]
    public void UnterminatedQuoteStopsAtTheLineRatherThanEatingTheFile()
    {
        string source = "int broken = 'a;\nprivate PlayerIdentifier id;\n";

        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);

        Assert.That(stripped, Does.Contain("PlayerIdentifier"));
    }

    [TestCase("var s = $\"{GameOptions.numPlayers}\";")]
    [TestCase("var s = $@\"{GameOptions.numPlayers}\";")]
    [TestCase("var s = @$\"{GameOptions.numPlayers}\";")]
    [TestCase("var s = $\"{{literal}} {GameOptions.numPlayers} {GameOptions.userid}\";")]
    [TestCase("var s = $\"{Format(\"GameOptions.fake\", GameOptions.numPlayers)}\";")]
    [TestCase("var s = $\"{new { Value = GameOptions.numPlayers }.Value}\";")]
    [TestCase("var s = $\"{(ready ? GameOptions.numPlayers : 0):D2}\";")]
    [TestCase("var s = $\"{global::GameOptions.numPlayers}\";")]
    [TestCase("var s = $\"{$@\"{GameOptions.numPlayers}\"}\";")]
    [TestCase("var s = $\"{ /* GameOptions.fake */ GameOptions.numPlayers}\";")]
    public void InterpolationAwareModeRetainsExecutableExpressions(string source)
    {
        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source, true);
        Assert.That(stripped, Does.Contain("GameOptions.numPlayers"));
        Assert.That(stripped, Does.Not.Contain("GameOptions.fake"));
    }

    [TestCase("var s = $\"GameOptions.fake {{GameOptions.fake}} {value:D2}\"; int keep = 1;")]
    [TestCase("var s = $@\"GameOptions.fake \"\"quote\"\" {{GameOptions.fake}}\"; int keep = 1;")]
    [TestCase("var s = @$\"{value:GameOptions.fake}\"; int keep = 1;")]
    [TestCase("var s = $\"{Format(\"GameOptions.fake\")}\"; int keep = 1;")]
    [TestCase("var s = $\"{value:GameOptions.fake} {other:GameOptions.fake}\"; int keep = 1;")]
    public void InterpolationAwareModeRemovesLiteralAndFormatText(string source)
    {
        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source, true);
        Assert.That(stripped, Does.Not.Contain("GameOptions."));
        Assert.That(stripped, Does.Contain("int keep = 1;"));
    }

    [Test]
    public void ExistingCallersRetainTheirLiteralOnlyPolicy()
    {
        string source = "var s = $\"{GameOptions.numPlayers}\"; int keep = 1;";
        string stripped = Level5TestSourceText.StripCommentsAndLiterals(source);
        Assert.That(stripped, Does.Not.Contain("GameOptions."));
        Assert.That(stripped, Does.Contain("int keep = 1;"));
    }
}
