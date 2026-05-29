using Lfe.Protocol.Types;

namespace Lfe.Protocol.Notifications;

public sealed class ResultEmitter
{
    private readonly INotificationEmitter _notificationEmitter;

    public ResultEmitter(INotificationEmitter notificationEmitter)
    {
        _notificationEmitter = notificationEmitter;
    }

    public Task EmitAsync(RunResultParams parameters, CancellationToken cancellationToken = default)
    {
        return _notificationEmitter.EmitAsync(LfeNotificationNames.RunResult, parameters, cancellationToken);
    }
}
