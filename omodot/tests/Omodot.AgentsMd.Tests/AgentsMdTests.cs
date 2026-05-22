using Omodot.AgentsMd;
using Xunit;

namespace Omodot.AgentsMd.Tests;

public class AgentsMdTests
{
    [Fact]
    public void FormatAgentsMdContextBlock_WithoutTruncation()
    {
        var result = AgentsMdFormatter.FormatAgentsMdContextBlock("/path/AGENTS.md", "content", false);
        Assert.Contains("[Directory Context: /path/AGENTS.md]", result);
        Assert.DoesNotContain("truncated", result);
    }

    [Fact]
    public void FormatAgentsMdContextBlock_WithTruncation()
    {
        var result = AgentsMdFormatter.FormatAgentsMdContextBlock("/path/AGENTS.md", "content", true);
        Assert.Contains("truncated", result);
    }

    [Fact]
    public void ResolveFilePath_AbsolutePath_ReturnsAsIs()
        => Assert.Equal("/abs/path", AgentsMdFormatter.ResolveFilePath("/root", "/abs/path"));

    [Fact]
    public void ResolveFilePath_RelativePath_Resolves()
    {
        var result = AgentsMdFormatter.ResolveFilePath("/root", "sub/file.md");
        Assert.Equal("/root/sub/file.md", result!.Replace('\\', '/'));
    }

    [Fact]
    public void ResolveFilePath_Empty_ReturnsNull()
        => Assert.Null(AgentsMdFormatter.ResolveFilePath("/root", ""));

    [Fact]
    public void ResolveFilePath_Null_ReturnsNull()
        => Assert.Null(AgentsMdFormatter.ResolveFilePath("/root", null!));
}
