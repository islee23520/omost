namespace Omodot.AgentOs;

public interface IAgentDefinition
{
    string Id { get; }

    string Name { get; }

    IReadOnlyDictionary<string, string>? Configuration { get; }
}
