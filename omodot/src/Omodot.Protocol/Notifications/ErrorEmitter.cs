using Omodot.Protocol.Types;

namespace Omodot.Protocol.Notifications;

public sealed class ErrorEmitter
{
    private readonly INotificationEmitter _notificationEmitter;

    public ErrorEmitter(INotificationEmitter notificationEmitter)
    {
        _notificationEmitter = notificationEmitter;
    }

    public Task EmitAsync(RunErrorParams parameters, CancellationToken cancellationToken = default)
    {
        return _notificationEmitter.EmitAsync(OmoNotificationNames.RunError, parameters, cancellationToken);
    }
}
