namespace Omodot.AgentOs;

public sealed class AgentOs
{
    public IReadOnlyList<IAgentOsModule> Modules { get; init; } = [];

    public IHostAdapter? Host { get; init; }

    public IReadOnlyList<IAgentDefinition> Agents { get; init; } = [];

    public IWorkflowDefinition? SelectedWorkflow { get; init; }

    public IReadOnlyList<string> InitializationOrder { get; init; } = [];
}
