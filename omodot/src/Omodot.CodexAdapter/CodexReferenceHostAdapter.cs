using Omodot.AgentOs;

namespace Omodot.CodexAdapter;

/// <summary>
/// Codex-specific IHostAdapter for Agent OS builder composition.
/// This is the first reference host adapter implementation.
/// </summary>
public sealed class CodexReferenceHostAdapter : IHostAdapter
{
    public string Id => "codex-reference-host";

    public string? DisplayName => "Codex Reference Host";
}
