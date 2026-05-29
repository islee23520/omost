using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Lfe.CodexAdapter;

public sealed class CodexJsonlParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parse a single JSONL line into a normalized adapter event.
    /// Returns null for empty/whitespace lines.
    /// Returns an error event for malformed JSON.
    /// </summary>
    public CodexAdapterEvent? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        CodexJsonlLine raw;
        try
        {
            raw = JsonSerializer.Deserialize<CodexJsonlLine>(line, JsonOptions)
                  ?? new CodexJsonlLine { Type = "null" };
        }
        catch (JsonException ex)
        {
            return new CodexAdapterEvent
            {
                EventType = CodexAdapterEventType.Error,
                Error = $"JSONL parse error: {ex.Message}",
            };
        }

        return NormalizeEvent(raw);
    }

    /// <summary>
    /// Parse an entire JSONL stream (e.g., process stdout) into events.
    /// </summary>
    public IReadOnlyList<CodexAdapterEvent> ParseStream(string jsonlText)
    {
        var events = new List<CodexAdapterEvent>();
        foreach (var line in jsonlText.Split('\n'))
        {
            var evt = ParseLine(line);
            if (evt is not null)
                events.Add(evt);
        }
        return events;
    }

    /// <summary>
    /// Parse JSONL lines incrementally from a text reader.
    /// </summary>
    public async IAsyncEnumerable<CodexAdapterEvent> ParseStreamAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                yield break;

            var evt = ParseLine(line);
            if (evt is not null)
                yield return evt;
        }
    }

    private static CodexAdapterEvent NormalizeEvent(CodexJsonlLine raw)
    {
        var eventType = MapEventType(raw.Type, raw.Status);

        var content = raw.Content;
        var role = NormalizeRole(raw.Role);
        var itemId = raw.Id;
        var sessionId = raw.SessionId;

        if (raw.Item is not null)
        {
            content ??= raw.Item.Text;
            itemId ??= raw.Item.Id;

            if (role is null && raw.Item.Type is not null)
            {
                role = raw.Item.Type.ToLowerInvariant() switch
                {
                    "agent_message" => "assistant",
                    "tool_call" or "function_call" => "tool",
                    _ => null,
                };
            }
        }

        sessionId ??= raw.ThreadId;

        return new CodexAdapterEvent
        {
            EventType = eventType,
            SessionId = sessionId,
            ThreadId = raw.ThreadId,
            Role = role,
            Content = content,
            Status = raw.Status,
            Error = raw.Error,
            ItemId = itemId,
            RawData = raw.ExtensionData is not null && raw.ExtensionData.Count > 0
                ? JsonSerializer.SerializeToElement(raw.ExtensionData)
                : null,
        };
    }

    private static CodexAdapterEventType MapEventType(string type, string? status)
    {
        return type.ToLowerInvariant() switch
        {
            "message" or "thread.message" => CodexAdapterEventType.Message,
            "status" or "thread.status" => CodexAdapterEventType.Status,
            "error" => CodexAdapterEventType.Error,
            "idle" => CodexAdapterEventType.Idle,
            "completed" or "done" => CodexAdapterEventType.Completed,
            "thread.started" or "turn.started" => CodexAdapterEventType.Status,
            "item.completed" => CodexAdapterEventType.Message,
            "turn.completed" => CodexAdapterEventType.Completed,
            _ when status is "idle" => CodexAdapterEventType.Idle,
            _ when status is "completed" or "done" => CodexAdapterEventType.Completed,
            _ when status is "error" or "failed" => CodexAdapterEventType.Error,
            _ => CodexAdapterEventType.Unknown,
        };
    }

    private static string? NormalizeRole(string? role)
        => role is "user" or "assistant" or "system" or "tool" ? role : null;
}
