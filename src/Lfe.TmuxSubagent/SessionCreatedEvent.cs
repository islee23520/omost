namespace Lfe.TmuxSubagent;

public sealed record SessionCreatedEvent(
    string Type,
    SessionCreatedEventProperties? Properties = null);

public sealed record SessionCreatedEventProperties(
    SessionCreatedEventInfo? Info = null);

public sealed record SessionCreatedEventInfo(
    string? Id = null,
    string? ParentId = null,
    string? Title = null);

public static class SessionCreatedEventParser
{
    public static SessionCreatedEvent Coerce(string type, object? properties = null)
    {
        if (properties is not System.Collections.IDictionary props)
            return new SessionCreatedEvent(type);

        System.Collections.IDictionary? infoDict = null;
        if (props.Contains("info") && props["info"] is System.Collections.IDictionary i)
            infoDict = i;

        return new SessionCreatedEvent(type, new SessionCreatedEventProperties(
            Info: new SessionCreatedEventInfo(
                Id: infoDict?.Contains("id") == true ? infoDict["id"]?.ToString() : null,
                ParentId: infoDict?.Contains("parentID") == true ? infoDict["parentID"]?.ToString() : null,
                Title: infoDict?.Contains("title") == true ? infoDict["title"]?.ToString() : null)));
    }
}
