using System.Text.Json;

namespace Lfe.AgentOs.Tests;

public sealed class AgentOsCoreContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AgentOsCoreContract_DefinesNeutralWorkerDispatchEnvelope()
    {
        var request = new AgentExecutionDispatchRequest(
            SessionId: "session-1",
            RunId: "run-1",
            TaskId: "worker-1",
            Role: AgentRoleKinds.Worker,
            Hidden: true,
            Message: "execute task",
            SharedSkills: [new SharedSkillReference("skill://review", true)],
            LazyCapabilities: [new LazyCapabilityRequest("lsp", "Need symbols")],
            ContinuationToken: "agent-os:run-1:worker-1");

        Assert.Equal("session-1", request.SessionId);
        Assert.Equal("run-1", request.RunId);
        Assert.Equal("worker-1", request.TaskId);
        Assert.Equal(AgentRoleKinds.Worker, request.Role);
        Assert.True(request.Hidden);
        Assert.Equal("skill://review", Assert.Single(request.SharedSkills).Id);
        Assert.Equal("lsp", Assert.Single(request.LazyCapabilities).CapabilityId);
        Assert.Equal("agent-os:run-1:worker-1", request.ContinuationToken);
    }

    [Fact]
    public void AgentOsCoreContract_DefinesNeutralDispatchReceiptWithArtifacts()
    {
        var receipt = new AgentExecutionDispatchReceipt(
            Accepted: true,
            SessionId: "session-1",
            DispatchId: "dispatch-1",
            TaskId: "reviewer-negative-1",
            ArtifactReferences: ["artifact://worker-1/result.md"],
            ContinuationToken: "agent-os:run-1:reviewer-negative-1");

        Assert.True(receipt.Accepted);
        Assert.Equal("session-1", receipt.SessionId);
        Assert.Equal("dispatch-1", receipt.DispatchId);
        Assert.Equal("reviewer-negative-1", receipt.TaskId);
        Assert.Equal("artifact://worker-1/result.md", Assert.Single(receipt.ArtifactReferences));
        Assert.Equal("agent-os:run-1:reviewer-negative-1", receipt.ContinuationToken);
    }

    [Fact]
    public void AgentOsCoreContract_SerializesWithoutLfpRuntimeNames()
    {
        var envelope = new AgentExecutionDispatchRequest(
            "session-1",
            "run-1",
            "planner-1",
            AgentRoleKinds.Planner,
            true,
            "plan task",
            [],
            [],
            null);

        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        Assert.DoesNotContain("Lfp.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Lina", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LazyCodex", json, StringComparison.OrdinalIgnoreCase);
    }
}
