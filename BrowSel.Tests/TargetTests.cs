using Xunit;

public class TargetTests
{
    [Theory]
    [InlineData("https://example.com/a?b=c", "https://example.com/a?b=c")]
    [InlineData("HTTPS://EXAMPLE.COM", "HTTPS://EXAMPLE.COM")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("BrowSelURL:https://example.com", "https://example.com")]
    [InlineData("BrowSelURL:http://example.com", "http://example.com")]
    [InlineData(@"C:\Users\me\page.html", @"C:\Users\me\page.html")]
    [InlineData(@"BrowSelURL:C:\Users\me\page.html", @"C:\Users\me\page.html")]
    [InlineData(@"\\server\share\page.html", @"\\server\share\page.html")]
    [InlineData("file:///C:/Users/me/page.html", "file:///C:/Users/me/page.html")]
    [InlineData("FILE:///C:/Users/me/page.html", "FILE:///C:/Users/me/page.html")]
    public void AcceptsSupportedTargets(string input, string expected) =>
        Assert.Equal(expected, Target.FromArgument(input));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-target")]
    [InlineData("/foo")]
    [InlineData("ftp://example.com")]
    [InlineData("mailto:me@example.com")]
    [InlineData("--register")]
    [InlineData("BrowSelURL:not-a-target")]
    public void RejectsUnsupportedTargets(string input) =>
        Assert.Null(Target.FromArgument(input));
}
