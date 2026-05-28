using System.Collections.Concurrent;

using Omodot.Protocol.Execution;
using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Notifications;
using Omodot.Protocol.Types;
using Omodot.UlwHostContract;

using ProtocolRunErrorParams = Omodot.Protocol.Types.RunErrorParams;
using ProtocolRunResultParams = Omodot.Protocol.Types.RunResultParams;
using UlwHostPromptRequest = Omodot.UlwHostContract.UlwPromptRequest;

namespace Omodot.Sidecar.Execution;

public sealed class AgentOsRuntimeExecutor : IOmoRunExecutor
{
    private readonly ConcurrentDictionary<string, string> _runSessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _terminalRuns = new(StringComparer.Ordinal);
    private readonly ErrorEmitter? _errorEmitter;
    private readonly ResultEmitter? _resultEmitter;
    private readonly IUlwHost _ulwHost;

    public AgentOsRuntimeExecutor(IUlwHost ulwHost, ResultEmitter? resultEmitter = null, ErrorEmitter? errorEmitter = null)
    {
        _ulwHost = ulwHost;
        _resultEmitter = resultEmitter;
        _errorEmitter = errorEmitter;
    }

    public async Task<OmoRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateDispatchRequest(request);

        try
        {
            var receipt = await _ulwHost.DispatchPromptAsync(new UlwHostPromptRequest(
                SessionId: request.SessionId,
                Message: request.Prompt,
                AgentName: request.Agent,
                ModelId: request.Model,
                ContinuationToken: request.ContinuationToken)).ConfigureAwait(false);

            var finalSessionId = string.IsNullOrWhiteSpace(receipt.SessionId)
                ? request.SessionId
                : receipt.SessionId;

            _runSessions[request.RunId] = finalSessionId;

            if (receipt.Accepted)
            {
                await TryEmitResultAsync(new ProtocolRunResultParams
                {
                    FinalSessionId = finalSessionId,
                    RunId = request.RunId,
                    Status = OmoRunStatusValues.Completed,
                }, ct).ConfigureAwait(false);
            }

            return new OmoRunAccepted
            {
                Accepted = receipt.Accepted,
                RunId = request.RunId,
            };
        }
        catch (OperationCanceledException)
        {
            await TryEmitResultAsync(new ProtocolRunResultParams
            {
                FinalSessionId = request.SessionId,
                OutputText = "Run cancelled: Cancellation requested",
                RunId = request.RunId,
                Status = OmoRunStatusValues.Cancelled,
            }).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await TryEmitErrorAsync(new ProtocolRunErrorParams
            {
                Code = ErrorCode.RunFailure,
                Message = exception.Message,
                Retryable = false,
                RunId = request.RunId,
            }).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<OmoCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_runSessions.TryGetValue(runId, out var sessionId))
        {
            await _ulwHost.AbortAsync(sessionId).ConfigureAwait(false);
        }

        await TryEmitResultAsync(new ProtocolRunResultParams
        {
            FinalSessionId = sessionId ?? string.Empty,
            OutputText = string.IsNullOrWhiteSpace(reason) ? "Run cancelled" : $"Run cancelled: {reason}",
            RunId = runId,
            Status = OmoRunStatusValues.Cancelled,
        }).ConfigureAwait(false);

        return new OmoCancelResult
        {
            RunId = runId,
            Status = OmoRunStatusValues.Cancelled,
        };
    }

    private static void ValidateDispatchRequest(RunDispatchRequestParams request)
    {
        if (string.IsNullOrWhiteSpace(request.RunId))
        {
            throw OmoProtocolErrors.InvalidParams("runId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw OmoProtocolErrors.InvalidParams("sessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw OmoProtocolErrors.InvalidParams("prompt is required.");
        }
    }

    private async Task TryEmitResultAsync(ProtocolRunResultParams parameters, CancellationToken cancellationToken = default)
    {
        if (_resultEmitter is null || !_terminalRuns.TryAdd(parameters.RunId, 0))
        {
            return;
        }

        await _resultEmitter.EmitAsync(parameters, cancellationToken).ConfigureAwait(false);
    }

    private async Task TryEmitErrorAsync(ProtocolRunErrorParams parameters, CancellationToken cancellationToken = default)
    {
        if (_errorEmitter is null || !_terminalRuns.TryAdd(parameters.RunId, 0))
        {
            return;
        }

        await _errorEmitter.EmitAsync(parameters, cancellationToken).ConfigureAwait(false);
    }
}
