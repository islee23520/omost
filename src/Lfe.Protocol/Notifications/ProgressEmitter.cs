using Lfe.Protocol.Types;

namespace Lfe.Protocol.Notifications;

public sealed class ProgressEmitter
{
    private readonly INotificationEmitter _notificationEmitter;

    public ProgressEmitter(INotificationEmitter notificationEmitter)
    {
        _notificationEmitter = notificationEmitter;
    }

    public Task EmitAsync(RunProgressParams parameters, CancellationToken cancellationToken = default)
    {
        return _notificationEmitter.EmitAsync(OmoNotificationNames.RunProgress, parameters, cancellationToken);
    }
}
