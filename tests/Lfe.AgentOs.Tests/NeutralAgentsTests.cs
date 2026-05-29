using System.Text.Json;
using Lfe.AgentOs;

namespace Lfe.AgentOs.Tests;

public sealed class NeutralAgentsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void NeutralAgents_RoleGraphRepresentsHiddenPlannerWorkerAndReviewers()
    {
        var graph = new AgentRoleGraph(
        [
            new AgentRoleDefinition("orchestrator", AgentRoleKinds.Orchestrator, false),
            new AgentRoleDefinition("planner", AgentRoleKinds.Planner, true),
            new AgentRoleDefinition("worker", AgentRoleKinds.Worker, true),
            new AgentRoleDefinition("reviewer.positive", AgentRoleKinds.Reviewer, true, AgentReviewStances.Positive),
            new AgentRoleDefinition("reviewer.negative", AgentRoleKinds.Reviewer, true, AgentReviewStances.Negative),
        ]);

        Assert.Equal(5, graph.Roles.Count);
        Assert.All(graph.Roles.Where(role => role.Id != "orchestrator"), role => Assert.True(role.Hidden));
        Assert.DoesNotContain(graph.Roles, role => role.Id.Contains("Lina", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(graph.Roles, role => role.Id.Contains("LazyCodex", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(graph.Roles, role => role.ReviewStance == AgentReviewStances.Positive);
        Assert.Contains(graph.Roles, role => role.ReviewStance == AgentReviewStances.Negative);
    }

    [Fact]
    public void NeutralAgents_ReviewSynthesisCreatesMitigationWhenNegativeBlocks()
    {
        var synthesis = AgentReviewSynthesis.Synthesize(
        [
            new AgentReviewSignal(AgentReviewStances.Positive, true, false, "Coherent", [], ["Proceed"], 0.8),
            new AgentReviewSignal(AgentReviewStances.Negative, true, true, "Rollback missing", ["rollback"], ["Add rollback task"], 0.9),
        ]);

        Assert.Equal(AgentReviewSynthesisActions.MitigateBeforeWorker, synthesis.Action);
        Assert.Equal(DecisionVerdictStatuses.Review, synthesis.Verdict.Status);
        Assert.Contains("Add rollback task", synthesis.RecommendedActions);
    }

    [Fact]
    public void NeutralAgents_DecisionVerdictAndContractsSerializeWithoutHostRuntime()
    {
        var envelope = new AgentExecutionContracts(
            new AgentContinuationState("run-1", "plan-1", "task-2", true),
            [new SharedSkillReference("skill://review", true)],
            [new LazyCapabilityRequest("lsp", "Need symbol lookup")],
            new DecisionVerdict(DecisionVerdictStatuses.Pass, 91, false, ["safe"]));

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<AgentExecutionContracts>(json, JsonOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal("run-1", roundTrip.Continuation.RunId);
        Assert.Equal("skill://review", Assert.Single(roundTrip.SharedSkills).Id);
        Assert.Equal("lsp", Assert.Single(roundTrip.LazyCapabilities).CapabilityId);
        Assert.Equal(DecisionVerdictStatuses.Pass, roundTrip.Verdict.Status);
        Assert.DoesNotContain("Codex", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LazyCodex", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lina", json, StringComparison.OrdinalIgnoreCase);
    }
}
