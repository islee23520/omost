using Lfe.Protocol.Types;

namespace Lfe.Protocol.Methods;

public sealed class SessionStartHandler : MethodHandlerBase<SessionStartRequestParams, SessionStartResult>
{
    private readonly LfeServerState _serverState;

    public SessionStartHandler(LfeServerState serverState)
        : base(LfeMethodNames.SessionStart)
    {
        _serverState = serverState;
    }

    protected override void Validate(SessionStartRequestParams parameters)
    {
        RequestValidator.RequireNonEmptyString(parameters.SessionId, "sessionId");
        RequestValidator.RequireNonEmptyString(parameters.Cwd, "cwd");
    }

    protected override ValueTask<SessionStartResult> HandleTypedAsync(
        SessionStartRequestParams parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _serverState.StartSession(parameters.SessionId);

        return ValueTask.FromResult(new SessionStartResult
        {
            Accepted = true,
            SessionId = parameters.SessionId,
        });
    }
}
