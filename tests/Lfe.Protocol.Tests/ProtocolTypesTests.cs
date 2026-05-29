using Lfe.Protocol.JsonRpc;
using Lfe.Protocol.Types;

namespace Lfe.Protocol.Tests;

public sealed class ProtocolTypesTests
{
    [Fact]
    public void ErrorCodesMatchTheFrozenSpecification()
    {
        Assert.Equal(-32600, ErrorCode.InvalidRequest);
        Assert.Equal(-32001, ErrorCode.VersionMismatch);
        Assert.Equal(-32010, ErrorCode.RunFailure);
        Assert.Equal("LFE_INVALID_REQUEST", LfeErrorCode.InvalidRequest);
        Assert.Equal("LFE_VERSION_MISMATCH", LfeErrorCode.VersionMismatch);
        Assert.Equal("LFE_RUN_FAILED", LfeErrorCode.RunFailed);
    }

    [Fact]
    public void MethodNamesMatchTheFrozenSpecification()
    {
        Assert.Equal(
            new[]
            {
                "lfe.initialize",
                "lfe.session.start",
                "lfe.run.dispatch",
                "lfe.run.cancel",
            },
            LfeMethodNames.All);
    }

    [Fact]
    public void NotificationNamesMatchTheFrozenSpecification()
    {
        Assert.Equal(
            new[]
            {
                "lfe.run.progress",
                "lfe.run.result",
                "lfe.run.error",
            },
            LfeNotificationNames.All);
    }

    [Fact]
    public void PhaseAndStatusValuesMatchTheFrozenSpecification()
    {
        Assert.Equal(
            new[] { "queued", "running", "tool", "completed", "failed", "cancelled" },
            LfeRunPhaseValues.All);
        Assert.Equal(
            new[] { "completed", "failed", "cancelled" },
            LfeRunStatusValues.All);
    }

    [Fact]
    public void PhaseOneCapabilitiesRemainFrozen()
    {
        Assert.Equal(
            new[]
            {
                "phase1.initialize",
                "phase1.session-start",
                "phase1.run-dispatch",
                "phase1.run-progress",
                "phase1.run-result",
                "phase1.run-cancel",
            },
            LfeCapabilityNames.All);
    }
}
