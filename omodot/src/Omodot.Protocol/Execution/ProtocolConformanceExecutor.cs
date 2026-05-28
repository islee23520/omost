using System.Text.Json;

using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Methods;
using Omodot.Protocol.Notifications;
using Omodot.Protocol.Types;

namespace Omodot.Protocol.Execution;

/// <summary>
/// Implementation of <see cref="IOmoRunExecutor"/> that ensures protocol conformance.
/// </summary>
public sealed class ProtocolConformanceExecutor : IOmoRunExecutor
{
    private readonly ErrorEmitter _errorEmitter;
    private readonly ProgressEmitter _progressEmitter;
    private readonly ResultEmitter _resultEmitter;
    private readonly OmodotServerState _serverState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolConformanceExecutor"/> class.
    /// </summary>
    /// <param name="serverState">The server state.</param>
    /// <param name="progressEmitter">The progress emitter.</param>
    /// <param name="resultEmitter">The result emitter.</param>
    /// <param name="errorEmitter">The error emitter.</param>
    public ProtocolConformanceExecutor(
        OmodotServerState serverState,
        ProgressEmitter progressEmitter,
        ResultEmitter resultEmitter,
        ErrorEmitter errorEmitter)
    {
        _serverState = serverState;
        _progressEmitter = progressEmitter;
        _resultEmitter = resultEmitter;
        _errorEmitter = errorEmitter;
    }

    /// <inheritdoc />
    public Task<OmoRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_serverState.HasSession(request.SessionId))
        {
            throw OmoProtocolErrors.InvalidParams($"Unknown session '{request.SessionId}'.");
        }

        var runRecord = _serverState.RecordRun(request.RunId, request.SessionId, OmoRunPhaseValues.Queued);
        _ = ExecuteRunAsync(runRecord, request);

        return Task.FromResult(new OmoRunAccepted
        {
            Accepted = true,
            RunId = request.RunId,
        });
    }

    /// <inheritdoc />
    public Task<OmoCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_serverState.TryGetRun(runId, out var runRecord) &&
            runRecord is not null &&
            OmoRunStatusValues.IsTerminal(runRecord.Status))
        {
            return Task.FromResult(new OmoCancelResult
            {
                RunId = runId,
                Status = runRecord.Status,
            });
        }

        _serverState.TryCancelRun(runId, reason, out _);

        return Task.FromResult(new OmoCancelResult
        {
            RunId = runId,
            Status = OmoRunStatusValues.Cancelled,
        });
    }

    private async Task ExecuteRunAsync(OmodotServerState.RunRecord runRecord, RunDispatchRequestParams parameters)
    {
        try
        {
            await Task.Delay(50, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = "Run accepted and queued",
                Phase = OmoRunPhaseValues.Queued,
                RunId = parameters.RunId,
                Completed = 0,
                Total = 3,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await Task.Delay(10, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);
            _serverState.UpdateRunStatus(parameters.RunId, OmoRunPhaseValues.Running);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = "Agent is processing the request",
                Phase = OmoRunPhaseValues.Running,
                RunId = parameters.RunId,
                Completed = 1,
                Total = 3,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await Task.Delay(10, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);
            _serverState.UpdateRunStatus(parameters.RunId, OmoRunStatusValues.Completed);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = "Run completed successfully",
                Phase = OmoRunPhaseValues.Completed,
                RunId = parameters.RunId,
                Completed = 3,
                Total = 3,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await _resultEmitter.EmitAsync(new RunResultParams
            {
                FinalSessionId = parameters.SessionId,
                OutputText = $"Completed Phase 1 run: {parameters.Prompt}",
                OutputJson = CreateOutputJson(parameters),
                RunId = parameters.RunId,
                Status = OmoRunStatusValues.Completed,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runRecord.CancellationTokenSource.IsCancellationRequested)
        {
            _serverState.UpdateRunStatus(parameters.RunId, OmoRunStatusValues.Cancelled);

            var reason = string.IsNullOrWhiteSpace(runRecord.CancellationReason)
                ? "Cancellation requested"
                : runRecord.CancellationReason;

            await _resultEmitter.EmitAsync(new RunResultParams
            {
                FinalSessionId = parameters.SessionId,
                OutputText = $"Run cancelled: {reason}",
                OutputJson = JsonSerializer.SerializeToElement(new { reason }, JsonRpcProtocol.SerializerOptions),
                RunId = parameters.RunId,
                Status = OmoRunStatusValues.Cancelled,
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _serverState.UpdateRunStatus(parameters.RunId, OmoRunStatusValues.Failed);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = exception.Message,
                Phase = OmoRunPhaseValues.Failed,
                RunId = parameters.RunId,
            }).ConfigureAwait(false);

            await _errorEmitter.EmitAsync(new RunErrorParams
            {
                RunId = parameters.RunId,
                Code = ErrorCode.RunFailure,
                Message = exception.Message,
                Retryable = false,
            }).ConfigureAwait(false);
        }
    }

    private static JsonElement? CreateOutputJson(RunDispatchRequestParams parameters)
    {
        if (parameters.Agent is null && parameters.Model is null && parameters.ContinuationToken is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(new
        {
            agent = parameters.Agent,
            model = parameters.Model,
            continuationToken = parameters.ContinuationToken,
        }, JsonRpcProtocol.SerializerOptions);
    }
}
