using Lfe.Protocol.Execution;
using Lfe.Protocol.Types;

namespace Lfe.Protocol.Methods;

public sealed class RunCancelHandler : MethodHandlerBase<RunCancelRequestParams, RunCancelResult>
{
    private readonly ILfeRunExecutor _runExecutor;

    public RunCancelHandler(ILfeRunExecutor runExecutor)
        : base(LfeMethodNames.RunCancel)
    {
        _runExecutor = runExecutor;
    }

    protected override void Validate(RunCancelRequestParams parameters)
    {
        RequestValidator.RequireNonEmptyString(parameters.RunId, "runId");
    }

    protected override async ValueTask<RunCancelResult> HandleTypedAsync(
        RunCancelRequestParams parameters,
        CancellationToken cancellationToken)
    {
        var result = await _runExecutor.CancelAsync(parameters.RunId, parameters.Reason, cancellationToken).ConfigureAwait(false);

        return new RunCancelResult
        {
            RunId = result.RunId,
            Status = result.Status,
        };
    }
}
