namespace Omodot.AgentOs;

/// <summary>
/// Defines a module that can be registered with the Agent OS.
/// </summary>
public interface IAgentOsModule
{
    /// <summary>
    /// Gets the unique identifier of the module.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the version of the module.
    /// </summary>
    string? Version { get; }

    /// <summary>
    /// Gets the list of module identifiers that this module depends on.
    /// </summary>
    IReadOnlyList<string> Requires { get; }

    /// <summary>
    /// Gets the list of module identifiers that this module conflicts with.
    /// </summary>
    IReadOnlyList<string> ConflictsWith { get; }

    /// <summary>
    /// Gets a value indicating whether this module is a preset that cannot be silently overridden.
    /// </summary>
    bool IsPreset { get; }
}
