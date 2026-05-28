namespace Omodot.AgentOs;

public interface IWorkflowDefinition
{
    string Id { get; }

    string Name { get; }

    bool IsDefault { get; }
}
