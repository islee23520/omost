namespace Omodot.TmuxSubagent;

public static class SessionMessageCount
{
    public static int GetMessageCount(object? data) =>
        data is System.Collections.ICollection col ? col.Count : 0;
}
