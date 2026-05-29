namespace Lfe.SessionManager;

public sealed record SessionMessage(string Id, string Role, string Text, string? Agent = null, long? CreatedAt = null);

public sealed record TodoItem(string? Id, string Content, string Status, string? Priority = null);

public sealed record SessionInfo(
    string Id,
    int MessageCount,
    DateTimeOffset? FirstMessage = null,
    DateTimeOffset? LastMessage = null,
    string[]? AgentsUsed = null,
    bool HasTodos = false,
    bool HasTranscript = false,
    TodoItem[]? Todos = null,
    int? TranscriptEntries = null);

public sealed record SearchResult(string SessionId, string MessageId, string Role, string Excerpt, int MatchCount, long? Timestamp = null);

public sealed record SessionListArgs(int? Limit = null, int? Offset = null, string? FromDate = null, string? ToDate = null, string? ProjectPath = null);

public sealed record SessionReadArgs(string SessionId, bool IncludeTodos = false, bool IncludeTranscript = false, int? Limit = null);

public sealed record SessionSearchArgs(string Query, string? SessionId = null, bool CaseSensitive = false, int? Limit = null);

public sealed record SessionRecord(SessionInfo Info, IReadOnlyList<SessionMessage> Messages, TodoItem[]? Todos = null, int? TranscriptEntries = null);
