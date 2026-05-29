namespace Lfe.AgentOs;

/// <summary>
/// Builder for creating <see cref="AgentOs"/> instances.
/// </summary>
public sealed class AgentOsBuilder
{
    private readonly Dictionary<string, IAgentOsModule> modules = new(StringComparer.Ordinal);
    private readonly List<IHostAdapter> hosts = [];
    private readonly List<IAgentDefinition> agents = [];
    private readonly List<IWorkflowDefinition> workflows = [];
    private string? selectedWorkflowId;

    /// <summary>
    /// Adds a module to the Agent OS.
    /// </summary>
    /// <param name="module">The module to add.</param>
    /// <returns>The builder instance.</returns>
    public AgentOsBuilder AddModule(IAgentOsModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ValidateId(module.Id, nameof(module));

        if (modules.ContainsKey(module.Id))
        {
            throw new InvalidOperationException($"Duplicate module Id '{module.Id}' rejected. Use ReplaceModule for explicit replacement.");
        }

        modules.Add(module.Id, module);
        return this;
    }

    /// <summary>
    /// Replaces an existing module with a new one.
    /// </summary>
    /// <param name="moduleId">The identifier of the module to replace.</param>
    /// <param name="replacement">The replacement module.</param>
    /// <returns>The builder instance.</returns>
    public AgentOsBuilder ReplaceModule(string moduleId, IAgentOsModule replacement)
    {
        ValidateId(moduleId, nameof(moduleId));
        ArgumentNullException.ThrowIfNull(replacement);
        ValidateId(replacement.Id, nameof(replacement));

        if (!modules.ContainsKey(moduleId))
        {
            throw new InvalidOperationException($"Cannot replace module '{moduleId}' because it is not registered.");
        }

        if (!StringComparer.Ordinal.Equals(moduleId, replacement.Id) && modules.ContainsKey(replacement.Id))
        {
            throw new InvalidOperationException($"Replacement module Id '{replacement.Id}' already exists.");
        }

        modules.Remove(moduleId);
        modules.Add(replacement.Id, replacement);
        return this;
    }

    /// <summary>
    /// Sets the host adapter for the Agent OS.
    /// </summary>
    /// <param name="host">The host adapter to use.</param>
    /// <returns>The builder instance.</returns>
    public AgentOsBuilder UseHost(IHostAdapter host)
    {
        ArgumentNullException.ThrowIfNull(host);
        ValidateId(host.Id, nameof(host));
        hosts.Add(host);
        return this;
    }

    /// <summary>
    /// Adds an agent definition to the Agent OS.
    /// </summary>
    /// <param name="agent">The agent definition to add.</param>
    /// <returns>The builder instance.</returns>
    public AgentOsBuilder AddAgent(IAgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ValidateId(agent.Id, nameof(agent));
        agents.Add(agent);
        return this;
    }

    /// <summary>
    /// Adds a workflow definition to the Agent OS.
    /// </summary>
    /// <param name="workflow">The workflow definition to add.</param>
    /// <returns>The builder instance.</returns>
    public AgentOsBuilder AddWorkflow(IWorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ValidateId(workflow.Id, nameof(workflow));
        workflows.Add(workflow);
        return this;
    }

    /// <summary>
    /// Selects a workflow to be used by the Agent OS.
    /// </summary>
    /// <param name="workflowId">The identifier of the workflow to use.</param>
    /// <returns>The builder instance.</returns>
    public AgentOsBuilder UseWorkflow(string workflowId)
    {
        ValidateId(workflowId, nameof(workflowId));
        selectedWorkflowId = workflowId;
        return this;
    }

    /// <summary>
    /// Builds a runnable <see cref="AgentOs"/> instance. Requires a host adapter.
    /// </summary>
    /// <returns>The built Agent OS instance.</returns>
    public AgentOs Build() => BuildCore(requireHost: true);

    /// <summary>
    /// Builds a design-time <see cref="AgentOs"/> instance. Does not require a host adapter.
    /// </summary>
    /// <returns>The built Agent OS instance.</returns>
    public AgentOs BuildDesignTime() => BuildCore(requireHost: false);

    private AgentOs BuildCore(bool requireHost)
    {
        if (hosts.Count > 1)
        {
            throw new InvalidOperationException($"Phase A supports exactly one host adapter; registered hosts: {string.Join(", ", hosts.Select(host => host.Id))}.");
        }

        if (requireHost && hosts.Count == 0)
        {
            throw new InvalidOperationException("Runnable Agent OS build requires exactly one host adapter.");
        }

        ValidateDependencies();
        ValidateConflicts();
        var orderedModules = TopologicalSort();
        var selectedWorkflow = ResolveSelectedWorkflow();

        return new AgentOs
        {
            Modules = orderedModules,
            Host = hosts.SingleOrDefault(),
            Agents = agents.ToArray(),
            SelectedWorkflow = selectedWorkflow,
            InitializationOrder = orderedModules.Select(module => module.Id).ToArray()
        };
    }

    private void ValidateDependencies()
    {
        var missing = modules.Values
            .SelectMany(module => module.Requires.Select(required => new { Module = module.Id, Required = required }))
            .Where(edge => !modules.ContainsKey(edge.Required))
            .Select(edge => $"{edge.Module} requires missing dependency {edge.Required}")
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Missing module dependencies: {string.Join("; ", missing)}.");
        }
    }

    private void ValidateConflicts()
    {
        var conflicts = modules.Values
            .SelectMany(module => module.ConflictsWith.Select(conflict => new { Module = module.Id, Conflict = conflict }))
            .Where(edge => modules.ContainsKey(edge.Conflict))
            .Select(edge => $"{edge.Module} conflicts with {edge.Conflict}")
            .ToArray();

        if (conflicts.Length > 0)
        {
            throw new InvalidOperationException($"Conflicting modules registered: {string.Join("; ", conflicts)}.");
        }
    }

    private IReadOnlyList<IAgentOsModule> TopologicalSort()
    {
        var state = new Dictionary<string, VisitState>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var result = new List<IAgentOsModule>(modules.Count);

        foreach (var moduleId in modules.Keys.Order(StringComparer.Ordinal))
        {
            Visit(moduleId, state, stack, result);
        }

        return result;
    }

    private void Visit(
        string moduleId,
        Dictionary<string, VisitState> state,
        Stack<string> stack,
        List<IAgentOsModule> result)
    {
        if (state.TryGetValue(moduleId, out var currentState))
        {
            if (currentState == VisitState.Visited)
            {
                return;
            }

            var cycle = stack.Reverse().SkipWhile(id => !StringComparer.Ordinal.Equals(id, moduleId)).Append(moduleId);
            throw new InvalidOperationException($"Dependency cycle detected: {string.Join(" -> ", cycle)}.");
        }

        state[moduleId] = VisitState.Visiting;
        stack.Push(moduleId);

        foreach (var dependencyId in modules[moduleId].Requires.Order(StringComparer.Ordinal))
        {
            Visit(dependencyId, state, stack, result);
        }

        stack.Pop();
        state[moduleId] = VisitState.Visited;
        result.Add(modules[moduleId]);
    }

    private IWorkflowDefinition? ResolveSelectedWorkflow()
    {
        if (selectedWorkflowId is not null)
        {
            var selected = workflows.Where(workflow => StringComparer.Ordinal.Equals(workflow.Id, selectedWorkflowId)).ToArray();
            if (selected.Length != 1)
            {
                throw new InvalidOperationException($"Selected workflow '{selectedWorkflowId}' was not registered exactly once.");
            }

            return selected[0];
        }

        if (workflows.Count == 0)
        {
            return null;
        }

        var defaults = workflows.Where(workflow => workflow.IsDefault).OrderBy(workflow => workflow.Id, StringComparer.Ordinal).ToArray();
        if (defaults.Length == 1)
        {
            return defaults[0];
        }

        if (defaults.Length > 1)
        {
            throw new InvalidOperationException($"Multiple default workflow entrypoints registered: {string.Join(", ", defaults.Select(workflow => workflow.Id))}.");
        }

        if (workflows.Count == 1)
        {
            return workflows[0];
        }

        throw new InvalidOperationException("Ambiguous workflow entrypoint. Use UseWorkflow or mark exactly one workflow as default.");
    }

    private static void ValidateId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id must be non-empty.", parameterName);
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
