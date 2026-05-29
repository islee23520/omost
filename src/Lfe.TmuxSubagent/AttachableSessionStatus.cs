namespace Lfe.TmuxSubagent;

public static class AttachableSessionStatus
{
    private static readonly HashSet<string> AttachableStatuses = ["idle", "running", "busy"];

    public static bool IsAttachable(string? status) =>
        status is not null && AttachableStatuses.Contains(status);
}
