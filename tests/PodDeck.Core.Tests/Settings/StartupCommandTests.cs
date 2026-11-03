using PodDeck.Core.Settings;

namespace PodDeck.Core.Tests.Settings;

public sealed class StartupCommandTests
{
    private const string ExePath = @"C:\Users\Ana María\AppData\Local\Programs\PodDeck\PodDeck.exe";

    [Fact]
    public void For_QuotesPathWithSpaces()
    {
        Assert.Equal($"\"{ExePath}\"", StartupCommand.For(ExePath));
    }

    [Fact]
    public void For_RejectsQuotesInPath()
    {
        Assert.Throws<ArgumentException>(() => StartupCommand.For("C:\\a\"b.exe"));
    }

    [Fact]
    public void Launches_OwnCommand()
    {
        Assert.True(StartupCommand.Launches(StartupCommand.For(ExePath), ExePath));
    }

    [Fact]
    public void Launches_IgnoresCaseAndArguments()
    {
        Assert.True(StartupCommand.Launches($"\"{ExePath.ToUpperInvariant()}\" --minimized", ExePath));
    }

    [Fact]
    public void Launches_UnquotedPath()
    {
        Assert.True(StartupCommand.Launches(@"C:\Tools\PodDeck.exe", @"C:\Tools\PodDeck.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"""C:\Old\PodDeck.exe""")]
    [InlineData(@"C:\Users\Ana")]
    public void Launches_OtherOrMissingCommand_False(string? stored)
    {
        Assert.False(StartupCommand.Launches(stored, ExePath));
    }

    [Fact]
    public void Launches_UnclosedQuote_ReadsRestAsPath()
    {
        Assert.True(StartupCommand.Launches($"\"{ExePath}", ExePath));
    }
}
