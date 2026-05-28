using Omodot.AgentOs;

namespace Omodot.CodexAdapter;

/// <summary>
/// Codex adapter extension methods for Agent OS builder.
/// These MUST live in Omodot.CodexAdapter, NOT in the host-neutral SDK.
/// </summary>
public static class AgentOsBuilderExtensions
{
    /// <summary>
    /// Registers the Codex reference host adapter with the Agent OS builder.
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The builder instance.</returns>
    public static AgentOsBuilder UseCodexReferenceHost(this AgentOsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseHost(new CodexReferenceHostAdapter());
    }
}
