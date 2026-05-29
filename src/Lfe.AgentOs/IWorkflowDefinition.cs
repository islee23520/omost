namespace Lfe.AgentOs;

/// <summary>
/// Defines a workflow within the Agent OS.
/// </summary>
public interface IWorkflowDefinition
{
    /// <summary>
    /// Gets the unique identifier of the workflow.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the workflow.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this workflow is the default entrypoint.
    /// </summary>
    bool IsDefault { get; }
}
