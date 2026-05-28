using System.Collections.Concurrent;

using Omodot.Protocol.Execution;
using Omodot.Protocol.Types;
using Omodot.UlwHostContract;

using UlwHostPromptRequest = Omodot.UlwHostContract.UlwPromptRequest;

namespace Omodot.Sidecar.Execution;

public sealed class AgentOsRuntimeExecutor : IOmoRunExecutor
{
    private readonly ConcurrentDictionary<string, string> _runSessions = new(StringComparer.Ordinal);
    private readonly IUlwHost _ulwHost;

    public AgentOsRuntimeExecutor(IUlwHost ulwHost)
    {
        _ulwHost = ulwHost;
    }

    public async Task<OmoRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var receipt = await _ulwHost.DispatchPromptAsync(new UlwHostPromptRequest(
            SessionId: request.SessionId,
            Message: request.Prompt,
            AgentName: request.Agent,
            ModelId: request.Model,
            ContinuationToken: request.ContinuationToken)).ConfigureAwait(false);

        _runSessions[request.RunId] = string.IsNullOrWhiteSpace(receipt.SessionId)
            ? request.SessionId
            : receipt.SessionId;

        return new OmoRunAccepted
        {
            Accepted = receipt.Accepted,
            RunId = request.RunId,
        };
    }

    public async Task<OmoCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_runSessions.TryGetValue(runId, out var sessionId))
        {
            await _ulwHost.AbortAsync(sessionId).ConfigureAwait(false);
        }

        return new OmoCancelResult
        {
            RunId = runId,
            Status = OmoRunStatusValues.Cancelled,
        };
    }
}
