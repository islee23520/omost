using Omodot.Protocol.Notifications;
using Omodot.Protocol.Types;

namespace Omodot.Protocol.Methods;

public sealed class RunCancelHandler : MethodHandlerBase<RunCancelRequestParams, RunCancelResult>
{
    private readonly OmodotServerState _serverState;

    public RunCancelHandler(
        OmodotServerState serverState)
        : base(OmoMethodNames.RunCancel)
    {
        _serverState = serverState;
    }

    protected override void Validate(RunCancelRequestParams parameters)
    {
        RequestValidator.RequireNonEmptyString(parameters.RunId, "runId");
    }

    protected override ValueTask<RunCancelResult> HandleTypedAsync(
        RunCancelRequestParams parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_serverState.TryGetRun(parameters.RunId, out var runRecord) &&
            runRecord is not null &&
            OmoRunStatusValues.IsTerminal(runRecord.Status))
        {
            return ValueTask.FromResult(new RunCancelResult
            {
                RunId = parameters.RunId,
                Status = runRecord.Status,
            });
        }

        _serverState.TryCancelRun(parameters.RunId, parameters.Reason, out _);

        return ValueTask.FromResult(new RunCancelResult
        {
            RunId = parameters.RunId,
            Status = OmoRunStatusValues.Cancelled,
        });
    }
}
