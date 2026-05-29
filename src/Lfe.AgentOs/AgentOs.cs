namespace Lfe.AgentOs;

/// <summary>
/// Represents a composed Agent OS instance.
/// </summary>
public sealed class AgentOs
{
    /// <summary>
    /// Gets the list of registered modules.
    /// </summary>
    public IReadOnlyList<IAgentOsModule> Modules { get; init; } = [];

    /// <summary>
    /// Gets the host adapter used by this instance.
    /// </summary>
    public IHostAdapter? Host { get; init; }

    /// <summary>
    /// Gets the list of registered agents.
    /// </summary>
    public IReadOnlyList<IAgentDefinition> Agents { get; init; } = [];

    /// <summary>
    /// Gets the selected workflow for this instance.
    /// </summary>
    public IWorkflowDefinition? SelectedWorkflow { get; init; }

    /// <summary>
    /// Gets the order in which modules should be initialized based on their dependencies.
    /// </summary>
    public IReadOnlyList<string> InitializationOrder { get; init; } = [];
}
