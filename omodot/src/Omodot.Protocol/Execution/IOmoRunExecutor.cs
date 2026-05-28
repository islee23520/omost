using Omodot.Protocol.Types;

namespace Omodot.Protocol.Execution;

public interface IOmoRunExecutor
{
    Task<OmoRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct);

    Task<OmoCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct);
}

public sealed record OmoRunAccepted
{
    public string RunId { get; init; } = string.Empty;

    public bool Accepted { get; init; }
}

public sealed record OmoCancelResult
{
    public string RunId { get; init; } = string.Empty;

    public string Status { get; init; } = OmoRunStatusValues.Cancelled;
}
