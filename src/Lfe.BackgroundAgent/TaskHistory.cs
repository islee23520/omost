namespace Lfe.BackgroundAgent;

public sealed record TaskHistoryEntry
{
    public string Id { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string Agent { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = BackgroundTaskStatuses.Pending;
    public string? Category { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class TaskHistory
{
    private readonly Dictionary<string, List<TaskHistoryEntry>> entries = new(StringComparer.Ordinal);

    public void Record(string? parentSessionId, TaskHistoryEntry entry)
    {
        if (string.IsNullOrEmpty(parentSessionId))
        {
            return;
        }

        if (!entries.TryGetValue(parentSessionId, out var list))
        {
            list = [];
            entries[parentSessionId] = list;
        }

        var existingIndex = list.FindIndex(candidate => string.Equals(candidate.Id, entry.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            var current = list[existingIndex];
            list[existingIndex] = current with
            {
                SessionId = entry.SessionId ?? current.SessionId,
                Agent = entry.Agent,
                Description = entry.Description,
                Status = entry.Status,
                Category = entry.Category ?? current.Category,
                StartedAt = entry.StartedAt ?? current.StartedAt,
                CompletedAt = entry.CompletedAt ?? current.CompletedAt,
            };
            return;
        }

        if (list.Count >= BackgroundTaskHistoryConstants.MaxTaskHistoryEntriesPerParent)
        {
            list.RemoveAt(0);
        }

        list.Add(entry with { });
    }

    public IReadOnlyList<TaskHistoryEntry> GetByParentSession(string parentSessionId)
    {
        return entries.TryGetValue(parentSessionId, out var list)
            ? list.Select(entry => entry with { }).ToArray()
            : Array.Empty<TaskHistoryEntry>();
    }

    public void ClearSession(string parentSessionId)
    {
        entries.Remove(parentSessionId);
    }

    public void ClearAll()
    {
        entries.Clear();
    }

    public string? FormatForCompaction(string parentSessionId)
    {
        var list = GetByParentSession(parentSessionId);
        if (list.Count == 0)
        {
            return null;
        }

        return string.Join(
            "\n",
            list.Select(entry =>
            {
                var description = entry.Description.Replace("\r", " ").Replace("\n", " ").Trim();
                var parts = new List<string>
                {
                    $"- **{entry.Agent}**",
                };

                if (!string.IsNullOrEmpty(entry.Category))
                {
                    parts.Add($"[{entry.Category}]");
                }

                parts.Add($"({entry.Status})");
                parts.Add($": {description}");

                if (!string.IsNullOrEmpty(entry.SessionId))
                {
                    parts.Add($" | session: `{entry.SessionId}`");
                }

                return string.Concat(parts);
            }));
    }
}
