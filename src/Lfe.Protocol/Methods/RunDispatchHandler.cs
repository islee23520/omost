using Lfe.Protocol.Execution;
using Lfe.Protocol.Types;

namespace Lfe.Protocol.Methods;

public sealed class RunDispatchHandler : MethodHandlerBase<RunDispatchRequestParams, RunDispatchResult>
{
    private readonly ILfeRunExecutor _runExecutor;

    public RunDispatchHandler(ILfeRunExecutor runExecutor)
        : base(LfeMethodNames.RunDispatch)
    {
        _runExecutor = runExecutor;
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
        var accepted = await _runExecutor.DispatchAsync(parameters, cancellationToken).ConfigureAwait(false);

        return new RunDispatchResult
        {
            Accepted = accepted.Accepted,
            RunId = accepted.RunId,
        };
    }
}
