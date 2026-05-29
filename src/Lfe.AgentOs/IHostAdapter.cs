namespace Lfe.AgentOs;

/// <summary>
/// Defines a host adapter for the Agent OS.
/// </summary>
public interface IHostAdapter
{
    /// <summary>
    /// Gets the unique identifier of the host adapter.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the host adapter.
    /// </summary>
    string? DisplayName { get; }
}
