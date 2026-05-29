using Lfe.AgentOs;

namespace Lfe.CodexAdapter;

/// <summary>
/// Codex-specific IHostAdapter for Agent OS builder composition.
/// This is the first reference host adapter implementation.
/// </summary>
public sealed class CodexReferenceHostAdapter : IHostAdapter
{
    /// <inheritdoc />
    public string Id => "codex-reference-host";

    /// <inheritdoc />
    public string? DisplayName => "Codex Reference Host";
}
