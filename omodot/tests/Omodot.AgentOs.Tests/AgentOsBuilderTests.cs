using Omodot.AgentOs;

namespace Omodot.AgentOs.Tests;

public sealed class AgentOsBuilderTests
{
    [Fact]
    public void DuplicateModuleIdRejected()
    {
        var builder = new AgentOsBuilder().AddModule(Module("core"));

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddModule(Module("core")));

        Assert.Contains("core", error.Message, StringComparison.Ordinal);
        Assert.Contains("Duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplicitReplacementSucceeds()
    {
        var original = Module("core", version: "1");
        var replacement = Module("core", version: "2");

        var agentOs = new AgentOsBuilder()
            .AddModule(original)
            .ReplaceModule("core", replacement)
            .BuildDesignTime();

        Assert.Same(replacement, Assert.Single(agentOs.Modules));
        Assert.Equal(["core"], agentOs.InitializationOrder);
    }

    [Fact]
    public void MissingDependencyRejectedWithDiagnostic()
    {
        var builder = new AgentOsBuilder().AddModule(Module("agent", requires: ["kernel"]));

        var error = Assert.Throws<InvalidOperationException>(builder.BuildDesignTime);

        Assert.Contains("agent", error.Message, StringComparison.Ordinal);
        Assert.Contains("kernel", error.Message, StringComparison.Ordinal);
        Assert.Contains("Missing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DependencyCycleRejectedWithCycleDiagnostic()
    {
        var builder = new AgentOsBuilder()
            .AddModule(Module("a", requires: ["b"]))
            .AddModule(Module("b", requires: ["c"]))
            .AddModule(Module("c", requires: ["a"]));

        var error = Assert.Throws<InvalidOperationException>(builder.BuildDesignTime);

        Assert.Contains("cycle", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a", error.Message, StringComparison.Ordinal);
        Assert.Contains("b", error.Message, StringComparison.Ordinal);
        Assert.Contains("c", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingModulesRejected()
    {
        var builder = new AgentOsBuilder()
            .AddModule(Module("left", conflictsWith: ["right"]))
            .AddModule(Module("right"));

        var error = Assert.Throws<InvalidOperationException>(builder.BuildDesignTime);

        Assert.Contains("left", error.Message, StringComparison.Ordinal);
        Assert.Contains("right", error.Message, StringComparison.Ordinal);
        Assert.Contains("conflict", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZeroHostRejectedForRunnableBuild()
    {
        var builder = new AgentOsBuilder().AddModule(Module("core"));

        var error = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("host", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZeroHostAllowedOnlyInDesignTimeValidationMode()
    {
        var agentOs = new AgentOsBuilder()
            .AddModule(Module("core"))
            .BuildDesignTime();

        Assert.Null(agentOs.Host);
        Assert.Equal(["core"], agentOs.InitializationOrder);
    }

    [Fact]
    public void MultipleHostsRejectedInPhaseA()
    {
        var builder = new AgentOsBuilder()
            .AddModule(Module("core"))
            .UseHost(Host("host-a"))
            .UseHost(Host("host-b"));

        var error = Assert.Throws<InvalidOperationException>(builder.Build);
        Assert.Contains("exactly one host", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host-a", error.Message, StringComparison.Ordinal);
        Assert.Contains("host-b", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PresetModulesCannotBeSilentlyOverridden()
    {
        var builder = new AgentOsBuilder().AddModule(Module("preset", isPreset: true));

        var error = Assert.Throws<InvalidOperationException>(() => builder.AddModule(Module("preset")));

        Assert.Contains("preset", error.Message, StringComparison.Ordinal);
        Assert.Contains("ReplaceModule", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleInitializationOrderIsDeterministicAndObservable()
    {
        var agentOs = new AgentOsBuilder()
            .AddModule(Module("workflow", requires: ["agent", "tools"]))
            .AddModule(Module("tools", requires: ["kernel"]))
            .AddModule(Module("agent", requires: ["kernel"]))
            .AddModule(Module("kernel"))
            .BuildDesignTime();

        Assert.Equal(["kernel", "agent", "tools", "workflow"], agentOs.InitializationOrder);
        Assert.Equal(agentOs.InitializationOrder, agentOs.Modules.Select(module => module.Id));
    }

    [Fact]
    public void SelectedWorkflowUsesExplicitEntrypoint()
    {
        var workflow = Workflow("ship");

        var agentOs = new AgentOsBuilder()
            .AddWorkflow(Workflow("draft"))
            .AddWorkflow(workflow)
            .UseWorkflow("ship")
            .BuildDesignTime();

        Assert.Same(workflow, agentOs.SelectedWorkflow);
    }

    [Fact]
    public void SingleDefaultWorkflowIsSelectedDeterministically()
    {
        var workflow = Workflow("default", isDefault: true);

        var agentOs = new AgentOsBuilder()
            .AddWorkflow(Workflow("other"))
            .AddWorkflow(workflow)
            .BuildDesignTime();

        Assert.Same(workflow, agentOs.SelectedWorkflow);
    }

    [Fact]
    public void AmbiguousWorkflowEntrypointsRejected()
    {
        var builder = new AgentOsBuilder()
            .AddWorkflow(Workflow("a"))
            .AddWorkflow(Workflow("b"));

        var error = Assert.Throws<InvalidOperationException>(builder.BuildDesignTime);
        Assert.Contains("Ambiguous", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TestModule Module(
        string id,
        string? version = null,
        IReadOnlyList<string>? requires = null,
        IReadOnlyList<string>? conflictsWith = null,
        bool isPreset = false) => new(id, version, requires ?? [], conflictsWith ?? [], isPreset);

    private static TestHost Host(string id) => new(id, id);

    private static TestWorkflow Workflow(string id, bool isDefault = false) => new(id, id, isDefault);

    private sealed record TestModule(
        string Id,
        string? Version,
        IReadOnlyList<string> Requires,
        IReadOnlyList<string> ConflictsWith,
        bool IsPreset) : IAgentOsModule;

    private sealed record TestHost(string Id, string? DisplayName) : IHostAdapter;

    private sealed record TestWorkflow(string Id, string Name, bool IsDefault) : IWorkflowDefinition;
}
