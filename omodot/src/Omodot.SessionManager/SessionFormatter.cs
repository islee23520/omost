namespace Omodot.SessionManager;

public static class SessionFormatter
{
    public static string FormatSessionList(IReadOnlyList<SessionRecord> records)
    {
        if (records.Count == 0) return "No sessions found.";

        var headers = new[] { "Session ID", "Messages", "First", "Last", "Agents" };
        var rows = records.Select(r => new[] {
            r.Info.Id,
            r.Info.MessageCount.ToString(),
            r.Info.FirstMessage?.ToString("yyyy-MM-dd") ?? "N/A",
            r.Info.LastMessage?.ToString("yyyy-MM-dd") ?? "N/A",
            r.Info.AgentsUsed is { Length: > 0 } arr ? string.Join(", ", arr) : "none",
        }).ToArray();

        var widths = headers.Select((h, i) => Math.Max(h.Length, rows.Max(r => r[i].Length))).ToArray();
        string FormatRow(string[] cells) => "| " + string.Join(" | ", cells.Select((c, i) => c.PadRight(widths[i]))) + " |";
        var separator = "|" + string.Join("|", widths.Select(w => new string('-', w + 2))) + "|";

        return string.Join("\n", [FormatRow(headers), separator, .. rows.Select(FormatRow)]);
    }

    public static string FormatSessionMessages(IReadOnlyList<SessionMessage> messages, bool includeTodos = false, TodoItem[]? todos = null)
    {
        if (messages.Count == 0) return "No messages found in this session.";
        var lines = new List<string>();
        foreach (var msg in messages)
        {
            var ts = msg.CreatedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(msg.CreatedAt.Value).ToString("o") : "Unknown time";
            var agent = msg.Agent is not null ? $" ({msg.Agent})" : "";
            lines.Add($"\n[{msg.Role}{agent}] {ts}");
            lines.Add(Clip(msg.Text.Trim(), 400));
        }
        if (includeTodos && todos is { Length: > 0 })
        {
            lines.Add("\n\n=== Todos ===");
            foreach (var todo in todos)
            {
                var status = todo.Status == "completed" ? "[x]" : todo.Status == "in_progress" ? "[-]" : "[ ]";
                lines.Add($"{status} [{todo.Status}] {todo.Content}");
            }
        }
        return string.Join("\n", lines);
    }

    public static string FormatSessionInfo(SessionInfo info)
    {
        var lines = new List<string>
        {
            $"Session ID: {info.Id}",
            $"Messages: {info.MessageCount}",
            $"Date Range: {info.FirstMessage?.ToString("o") ?? "N/A"} to {info.LastMessage?.ToString("o") ?? "N/A"}",
            $"Agents Used: {(info.AgentsUsed is { Length: > 0 } arr ? string.Join(", ", arr) : "none")}",
            $"Has Todos: {(info.HasTodos ? $"Yes ({info.Todos?.Length ?? 0} items)" : "No")}",
            $"Has Transcript: {(info.HasTranscript ? $"Yes ({info.TranscriptEntries ?? 0} entries)" : "No")}",
        };
        return string.Join("\n", lines);
    }

    public static string FormatSearchResults(IReadOnlyList<SearchResult> results)
    {
        if (results.Count == 0) return "No matches found.";
        var lines = new List<string> { $"Found {results.Count} matches:\n" };
        foreach (var r in results)
        {
            var ts = r.Timestamp.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(r.Timestamp.Value).ToString("o") : "";
            lines.Add($"[{r.SessionId}] {r.MessageId} ({r.Role}) {ts}");
            lines.Add($"  {r.Excerpt}");
            lines.Add($"  Matches: {r.MatchCount}\n");
        }
        return string.Join("\n", lines);
    }

    public static SessionInfo? BuildSessionInfo(string sessionId, IReadOnlyList<SessionMessage> messages, TodoItem[]? todos = null, int transcriptEntries = 0)
    {
        if (messages.Count == 0) return null;
        var agents = new HashSet<string>();
        DateTimeOffset? first = null, last = null;
        foreach (var m in messages)
        {
            if (m.Agent is not null) agents.Add(m.Agent);
            if (m.CreatedAt is not null)
            {
                var d = DateTimeOffset.FromUnixTimeMilliseconds(m.CreatedAt.Value);
                if (first is null || d < first) first = d;
                if (last is null || d > last) last = d;
            }
        }
        var todoArr = todos ?? [];
        return new SessionInfo(sessionId, messages.Count, first, last, agents.ToArray(), todoArr.Length > 0, transcriptEntries > 0, todoArr.Length > 0 ? todoArr : null, transcriptEntries);
    }

    public static List<SessionRecord> FilterSessionsByDate(IReadOnlyList<SessionRecord> records, string? fromDate = null, string? toDate = null)
    {
        if (fromDate is null && toDate is null) return records.ToList();
        var from = fromDate is not null ? DateTimeOffset.Parse(fromDate) : (DateTimeOffset?)null;
        var to = toDate is not null ? DateTimeOffset.Parse(toDate) : (DateTimeOffset?)null;
        return records.Where(r =>
        {
            if (r.Info.LastMessage is not { } last) return false;
            if (from.HasValue && last < from) return false;
            if (to.HasValue && last > to) return false;
            return true;
        }).ToList();
    }

    public static List<SearchResult> SearchInSession(string sessionId, IReadOnlyList<SessionMessage> messages, string query, bool caseSensitive = false, int? maxResults = null)
    {
        var searchQuery = caseSensitive ? query : query.ToLowerInvariant();
        var results = new List<SearchResult>();
        foreach (var m in messages)
        {
            if (maxResults.HasValue && results.Count >= maxResults) break;
            var haystack = caseSensitive ? m.Text : m.Text.ToLowerInvariant();
            var count = haystack.Split(searchQuery).Length - 1;
            if (count <= 0) continue;
            var idx = haystack.IndexOf(searchQuery, StringComparison.Ordinal);
            var start = Math.Max(0, idx - 50);
            var end = Math.Min(m.Text.Length, idx + searchQuery.Length + 50);
            var excerpt = m.Text[start..end];
            if (start > 0) excerpt = "..." + excerpt;
            if (end < m.Text.Length) excerpt += "...";
            results.Add(new SearchResult(sessionId, m.Id, m.Role, excerpt, count, m.CreatedAt));
        }
        return results;
    }

    public static List<SessionRecord> SelectSessionRecords(IReadOnlyList<SessionRecord> records, SessionListArgs? args = null)
    {
        args ??= new SessionListArgs();
        var filtered = FilterSessionsByDate(records, args.FromDate, args.ToDate);
        var offset = Math.Max(0, args.Offset ?? 0);
        return args.Limit is > 0 ? filtered.Skip(offset).Take(args.Limit.Value).ToList() : filtered.Skip(offset).ToList();
    }

    public static string ReadSessionRecord(SessionRecord record, SessionReadArgs args)
    {
        var messages = args.Limit is > 0 ? record.Messages.Skip(record.Messages.Count - args.Limit.Value).ToList() : record.Messages;
        return FormatSessionMessages(messages, args.IncludeTodos, args.IncludeTodos ? record.Todos : null);
    }

    public static List<SearchResult> SearchSessionRecords(IReadOnlyList<SessionRecord> records, SessionSearchArgs args)
    {
        var matching = args.SessionId is not null ? records.Where(r => r.Info.Id == args.SessionId).ToList() : records;
        var results = matching.SelectMany(r => SearchInSession(r.Info.Id, r.Messages, args.Query, args.CaseSensitive, args.Limit)).ToList();
        return args.Limit is > 0 ? results.Take(args.Limit.Value).ToList() : results;
    }

    private static string Clip(string text, int max = 200) => text.Length > max ? $"{text[..max]}..." : text;
}
