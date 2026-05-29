using Lfe.UlwHostContract;
using Xunit;

namespace Lfe.UlwHostContract.Tests;

public sealed class AgentWorkerHostContractTests
{
    [Fact]
    public void UlwPromptReceipt_CanCarryArtifactReferencesForWorkerAndReviewerDispatch()
    {
        var receipt = new UlwPromptReceipt(
            Accepted: true,
            SessionId: "session-1",
            DispatchId: "dispatch-1",
            ResponseId: "response-1",
            ContinuationToken: "agent-os:run-1:worker-1",
            AgenticStatePreserved: true,
            ArtifactReferences: ["artifact://worker-1/result.md"]);

        Assert.True(receipt.Accepted);
        Assert.Equal("session-1", receipt.SessionId);
        Assert.Equal("dispatch-1", receipt.DispatchId);
        Assert.Equal("agent-os:run-1:worker-1", receipt.ContinuationToken);
        Assert.NotNull(receipt.ArtifactReferences);
        Assert.Equal("artifact://worker-1/result.md", Assert.Single(receipt.ArtifactReferences));
    }
}
