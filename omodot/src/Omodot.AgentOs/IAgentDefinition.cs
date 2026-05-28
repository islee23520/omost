namespace Omodot.AgentOs;

/// <summary>
/// Defines an agent within the Agent OS.
/// </summary>
public interface IAgentDefinition
{
    /// <summary>
    /// Gets the unique identifier of the agent.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the optional configuration for the agent.
    /// </summary>
    IReadOnlyDictionary<string, string>? Configuration { get; }
}
