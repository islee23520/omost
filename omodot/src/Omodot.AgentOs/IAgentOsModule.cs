namespace Omodot.AgentOs;

public interface IAgentOsModule
{
    string Id { get; }

    string? Version { get; }

    IReadOnlyList<string> Requires { get; }

    IReadOnlyList<string> ConflictsWith { get; }

    bool IsPreset { get; }
}
