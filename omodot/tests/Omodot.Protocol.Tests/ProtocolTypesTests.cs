using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Types;

namespace Omodot.Protocol.Tests;

public sealed class ProtocolTypesTests
{
    [Fact]
    public void ErrorCodesMatchTheFrozenSpecification()
    {
        Assert.Equal(-32600, ErrorCode.InvalidRequest);
        Assert.Equal(-32001, ErrorCode.VersionMismatch);
        Assert.Equal(-32010, ErrorCode.RunFailure);
        Assert.Equal("OMO_INVALID_REQUEST", OmoErrorCode.InvalidRequest);
        Assert.Equal("OMO_VERSION_MISMATCH", OmoErrorCode.VersionMismatch);
        Assert.Equal("OMO_RUN_FAILED", OmoErrorCode.RunFailed);
    }

    [Fact]
    public void MethodNamesMatchTheFrozenSpecification()
    {
        Assert.Equal(
            new[]
            {
                "omo.initialize",
                "omo.session.start",
                "omo.run.dispatch",
                "omo.run.cancel",
            },
            OmoMethodNames.All);
    }

    [Fact]
    public void NotificationNamesMatchTheFrozenSpecification()
    {
        Assert.Equal(
            new[]
            {
                "omo.run.progress",
                "omo.run.result",
                "omo.run.error",
            },
            OmoNotificationNames.All);
    }

    [Fact]
    public void PhaseAndStatusValuesMatchTheFrozenSpecification()
    {
        Assert.Equal(
            new[] { "queued", "running", "tool", "completed", "failed", "cancelled" },
            OmoRunPhaseValues.All);
        Assert.Equal(
            new[] { "completed", "failed", "cancelled" },
            OmoRunStatusValues.All);
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
            OmoCapabilityNames.All);
    }
}
