using Omodot.Protocol.Types;

namespace Omodot.Protocol.Methods;

public sealed class SessionStartHandler : MethodHandlerBase<SessionStartRequestParams, SessionStartResult>
{
    private readonly OmodotServerState _serverState;

    public SessionStartHandler(OmodotServerState serverState)
        : base(OmoMethodNames.SessionStart)
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
