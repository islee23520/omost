namespace Lfe.Protocol.Notifications;

public interface INotificationEmitter
{
    Task EmitAsync<TParams>(string methodName, TParams parameters, CancellationToken cancellationToken = default);
}
