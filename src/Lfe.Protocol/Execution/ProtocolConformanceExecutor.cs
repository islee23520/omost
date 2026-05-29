using System.Text.Json;

using Lfe.Protocol.JsonRpc;
using Lfe.Protocol.Methods;
using Lfe.Protocol.Notifications;
using Lfe.Protocol.Types;

namespace Lfe.Protocol.Execution;

/// <summary>
/// Implementation of <see cref="ILfeRunExecutor"/> that ensures protocol conformance.
/// </summary>
public sealed class ProtocolConformanceExecutor : ILfeRunExecutor
{
    private readonly ErrorEmitter _errorEmitter;
    private readonly ProgressEmitter _progressEmitter;
    private readonly ResultEmitter _resultEmitter;
    private readonly LfeServerState _serverState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolConformanceExecutor"/> class.
    /// </summary>
    /// <param name="serverState">The server state.</param>
    /// <param name="progressEmitter">The progress emitter.</param>
    /// <param name="resultEmitter">The result emitter.</param>
    /// <param name="errorEmitter">The error emitter.</param>
    public ProtocolConformanceExecutor(
        LfeServerState serverState,
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
    public Task<LfeRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_serverState.HasSession(request.SessionId))
        {
            throw LfeProtocolErrors.InvalidParams($"Unknown session '{request.SessionId}'.");
        }

        var runRecord = _serverState.RecordRun(request.RunId, request.SessionId, LfeRunPhaseValues.Queued);
        _ = ExecuteRunAsync(runRecord, request);

        return Task.FromResult(new LfeRunAccepted
        {
            Accepted = true,
            RunId = request.RunId,
        });
    }

    /// <inheritdoc />
    public Task<LfeCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_serverState.TryGetRun(runId, out var runRecord) &&
            runRecord is not null &&
            LfeRunStatusValues.IsTerminal(runRecord.Status))
        {
            return Task.FromResult(new LfeCancelResult
            {
                RunId = runId,
                Status = runRecord.Status,
            });
        }

        _serverState.TryCancelRun(runId, reason, out _);

        return Task.FromResult(new LfeCancelResult
        {
            RunId = runId,
            Status = LfeRunStatusValues.Cancelled,
        });
    }

    private async Task ExecuteRunAsync(LfeServerState.RunRecord runRecord, RunDispatchRequestParams parameters)
    {
        try
        {
            await Task.Delay(50, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = "Run accepted and queued",
                Phase = LfeRunPhaseValues.Queued,
                RunId = parameters.RunId,
                Completed = 0,
                Total = 3,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await Task.Delay(10, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);
            _serverState.UpdateRunStatus(parameters.RunId, LfeRunPhaseValues.Running);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = "Agent is processing the request",
                Phase = LfeRunPhaseValues.Running,
                RunId = parameters.RunId,
                Completed = 1,
                Total = 3,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);

            await Task.Delay(10, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);
            _serverState.UpdateRunStatus(parameters.RunId, LfeRunStatusValues.Completed);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = "Run completed successfully",
                Phase = LfeRunPhaseValues.Completed,
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
                Status = LfeRunStatusValues.Completed,
            }, runRecord.CancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runRecord.CancellationTokenSource.IsCancellationRequested)
        {
            _serverState.UpdateRunStatus(parameters.RunId, LfeRunStatusValues.Cancelled);

            var reason = string.IsNullOrWhiteSpace(runRecord.CancellationReason)
                ? "Cancellation requested"
                : runRecord.CancellationReason;

            await _resultEmitter.EmitAsync(new RunResultParams
            {
                FinalSessionId = parameters.SessionId,
                OutputText = $"Run cancelled: {reason}",
                OutputJson = JsonSerializer.SerializeToElement(new { reason }, JsonRpcProtocol.SerializerOptions),
                RunId = parameters.RunId,
                Status = LfeRunStatusValues.Cancelled,
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _serverState.UpdateRunStatus(parameters.RunId, LfeRunStatusValues.Failed);

            await _progressEmitter.EmitAsync(new RunProgressParams
            {
                Message = exception.Message,
                Phase = LfeRunPhaseValues.Failed,
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
