using Lfe.AgentOs;
using Xunit;

namespace Lfe.CodexAdapter.Tests;

public sealed class CodexReferenceHostAdapterTests
{
    [Fact]
    public void UseCodexReferenceHost_RegistersExactlyOneHostAdapter()
    {
        var agentOs = new AgentOsBuilder()
            .AddModule(new TestModule("core"))
            .UseCodexReferenceHost()
            .Build();

        Assert.NotNull(agentOs.Host);
        Assert.IsType<CodexReferenceHostAdapter>(agentOs.Host);
        Assert.Equal("codex-reference-host", agentOs.Host.Id);
    }

    [Fact]
    public void CodexReferenceHostImplementsIHostAdapter()
    {
        IHostAdapter host = new CodexReferenceHostAdapter();

        Assert.Equal("codex-reference-host", host.Id);
        Assert.Equal("Codex Reference Host", host.DisplayName);
    }

    [Fact]
    public void UseCodexReferenceHost_IsInCodexAdapter_NotAgentOs()
    {
        var extensionType = typeof(AgentOsBuilderExtensions);

        Assert.Equal("Lfe.CodexAdapter", extensionType.Namespace);
    }

    private sealed record TestModule(
        string Id,
        string? Version,
        IReadOnlyList<string> Requires,
        IReadOnlyList<string> ConflictsWith,
        bool IsPreset) : IAgentOsModule
    {
        public TestModule(string id)
            : this(id, null, Array.Empty<string>(), Array.Empty<string>(), false)
        {
        }
    }
}
