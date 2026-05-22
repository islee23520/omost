using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Notifications;
using Omodot.Protocol.Types;
using System.Text.Json;

namespace Omodot.Protocol.Methods;

public sealed class RunDispatchHandler : MethodHandlerBase<RunDispatchRequestParams, RunDispatchResult>
{
    private readonly ErrorEmitter _errorEmitter;
    private readonly ProgressEmitter _progressEmitter;
    private readonly ResultEmitter _resultEmitter;
    private readonly OmodotServerState _serverState;

    public RunDispatchHandler(
        OmodotServerState serverState,
        ProgressEmitter progressEmitter,
        ResultEmitter resultEmitter,
        ErrorEmitter errorEmitter)
        : base(OmoMethodNames.RunDispatch)
    {
        _serverState = serverState;
        _progressEmitter = progressEmitter;
        _resultEmitter = resultEmitter;
        _errorEmitter = errorEmitter;
    }

    protected override void Validate(RunDispatchRequestParams parameters)
    {
        RequestValidator.RequireNonEmptyString(parameters.RunId, "runId");
        RequestValidator.RequireNonEmptyString(parameters.SessionId, "sessionId");
        RequestValidator.RequireNonEmptyString(parameters.Prompt, "prompt");
    }

    protected override async ValueTask<RunDispatchResult> HandleTypedAsync(
        RunDispatchRequestParams parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_serverState.HasSession(parameters.SessionId))
        {
            throw OmoProtocolErrors.InvalidParams($"Unknown session '{parameters.SessionId}'.");
        }

        var runRecord = _serverState.RecordRun(parameters.RunId, parameters.SessionId, OmoRunPhaseValues.Queued);
        _ = ExecuteRunAsync(runRecord, parameters);

        return new RunDispatchResult
        {
            Accepted = true,
            RunId = parameters.RunId,
        };
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
