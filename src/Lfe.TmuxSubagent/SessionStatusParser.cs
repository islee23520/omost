namespace Lfe.TmuxSubagent;

public sealed record SessionStatus(string Type);

public static class SessionStatusParser
{
    public static Dictionary<string, SessionStatus> ParseSessionStatusMap(object? data)
    {
        if (data is not System.Collections.IDictionary dict) return [];

        var result = new Dictionary<string, SessionStatus>();
        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            var key = entry.Key?.ToString();
            if (key is null) continue;

            if (entry.Value is System.Collections.IDictionary valueDict)
            {
                foreach (System.Collections.DictionaryEntry valueEntry in valueDict)
                {
                    if (valueEntry.Key?.ToString() == "type" && valueEntry.Value is string type)
                    {
                        result[key] = new SessionStatus(type);
                        break;
                    }
                }
            }
        }
        return result;
    }
}
