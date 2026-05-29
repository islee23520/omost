namespace Lfe.BackgroundAgent;

public abstract record SessionActivityLookup
{
    public sealed record Activity(DateTime Value) : SessionActivityLookup;
    public sealed record Missing : SessionActivityLookup;
    public sealed record Unavailable : SessionActivityLookup;
}

public delegate Task<SessionActivityLookup> SessionActivityResolver(string sessionId);

public static class SessionActivity
{
    public static DateTime? ExtractSessionActivityDate(object? sessionInfo)
    {
        var record = ObjectRecord.AsRecord(sessionInfo);
        if (record is null)
        {
            return null;
        }

        var time = ObjectRecord.GetRecord(record, "time");
        return ToDateTime(ObjectRecord.GetInt64(time, "updated")) ?? ToDateTime(ObjectRecord.GetInt64(record, "time_updated"));
    }

    public static SessionActivityLookup SessionActivityLookupFromInfo(object? sessionInfo)
    {
        var activity = ExtractSessionActivityDate(sessionInfo);
        return activity is null ? new SessionActivityLookup.Missing() : new SessionActivityLookup.Activity(activity.Value);
    }

    private static DateTime? ToDateTime(long? milliseconds)
    {
        return milliseconds is >= 0 ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value).UtcDateTime : null;
    }
}
