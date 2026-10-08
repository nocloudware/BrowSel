using Xunit;

public class UpdateParsingTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("V2", "2.0.0")]
    [InlineData("1.2.3-beta", "1.2.3")]
    public void ParsesVersion(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateParsing.ParseVersion(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void RejectsBadTag(string? tag) => Assert.Null(UpdateParsing.ParseVersion(tag));

    [Fact]
    public void ParsesBareAndSha256sumHash()
    {
        var h = new string('A', 64);
        Assert.Equal(h.ToLowerInvariant(), UpdateParsing.ParseHash(h));
        Assert.Equal(h.ToLowerInvariant(), UpdateParsing.ParseHash($"{h}  BrowSelSetup-1.2.0.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void RejectsBadHash(string? text) => Assert.Null(UpdateParsing.ParseHash(text));
}
