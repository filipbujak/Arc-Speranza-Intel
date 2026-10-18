using ArcSperanzaIntel.Core.Util;

namespace ArcSperanzaIntel.Tests;

public class IssueReportTests
{
    [Fact]
    public void RemovesWindowsUserName()
    {
        var text = @"at C:\Users\drkra\AppData\Roaming\ArcSperanzaIntel\state.json and /Users/drkra/x, user drkra";
        var clean = IssueReport.Sanitize(text, "drkra");
        Assert.DoesNotContain("drkra", clean);
        Assert.Contains(@"C:\Users\<user>\AppData", clean);
    }

    [Fact]
    public void TailKeepsLastLines()
    {
        var tail = IssueReport.Tail("a\n\nb\nc\nd\n", 2, 1000);
        Assert.Equal("c\nd", tail);
    }

    [Fact]
    public void TailRespectsMaxChars()
    {
        var tail = IssueReport.Tail(new string('x', 500), 10, 100);
        Assert.Equal(101, tail.Length);
        Assert.True(tail.StartsWith("…"));
    }

    [Fact]
    public void BuildsPrefilledIssueUrl()
    {
        var url = IssueReport.BuildUrl("https://github.com/o/r/", "bug.yml", "[Bug] ",
            new Dictionary<string, string> { ["version"] = "1.2.0", ["diagnostics"] = "a b&c" });
        Assert.Equal("https://github.com/o/r/issues/new?template=bug.yml&title=%5BBug%5D%20&version=1.2.0&diagnostics=a%20b%26c", url);
    }

    [Fact]
    public void LongFieldsAreShortened()
    {
        var url = IssueReport.BuildUrl("https://github.com/o/r", "bug.yml", "",
            new Dictionary<string, string> { ["diagnostics"] = new string('y', 20000) + "END" });
        Assert.True(url.Length <= IssueReport.MaxUrlLength);
        Assert.True(url.EndsWith("END"));
    }
}

public class TransientErrorTests
{
    [Fact]
    public void CanceledDownloadIsTransient() =>
        Assert.True(TransientErrors.IsTransient(new TaskCanceledException("x", new IOException("read failed", new ObjectDisposedException("SslStream")))));

    [Fact]
    public void NetworkErrorInsideIsTransient() =>
        Assert.True(TransientErrors.IsTransient(new InvalidOperationException("wrap", new HttpRequestException("offline"))));

    [Fact]
    public void RealBugIsNotTransient()
    {
        Assert.False(TransientErrors.IsTransient(new NullReferenceException()));
        Assert.False(TransientErrors.IsTransient(new IOException("disk full")));
        Assert.False(TransientErrors.IsTransient(null));
    }
}
