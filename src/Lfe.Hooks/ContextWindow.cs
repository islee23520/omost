namespace Lfe.Hooks;

using System.Text.Json;
using System.Text.RegularExpressions;

public static partial class ContextWindow
{
    #region Context Window Monitor

    public static string BuildContextWindowReminder(long actualLimit) =>
        $"""
        [SYSTEM DIRECTIVE: CONTEXT_WINDOW_MONITOR]

        You are using a {actualLimit:N0}-token context window.
        You still have context remaining - do NOT rush or skip tasks.
        Complete your work thoroughly and methodically.
        """;

    public static bool ShouldWarnContextWindow(ContextTokenInfo tokens, long actualLimit)
    {
        var totalInputTokens = (tokens.Input ?? 0) + (tokens.Cache?.Read ?? 0);
        return actualLimit > 0 && (double)totalInputTokens / actualLimit >= HookDefinitions.ContextWarningThreshold;
    }

    public static string AppendContextWindowStatus(string output, ContextTokenInfo tokens, long actualLimit)
    {
        var totalInputTokens = (tokens.Input ?? 0) + (tokens.Cache?.Read ?? 0);
        var usage = (double)totalInputTokens / actualLimit;
        var clampedPct = Math.Clamp(usage, 0, 1);
        var usedPct = (clampedPct * 100).ToString("F1");
        var remainingPct = ((1 - clampedPct) * 100).ToString("F1");
        return $"{output}\n\n{BuildContextWindowReminder(actualLimit)}\n[Context Status: {usedPct}% used ({totalInputTokens:N0}/{actualLimit:N0} tokens), {remainingPct}% remaining]";
    }

    #endregion

    #region Token Limit Error Parsing

    private static readonly Regex[] TokenLimitPatterns =
    [
        new Regex(@"(\d+)\s*tokens?\s*>\s*(\d+)\s*maximum", RegexOptions.IgnoreCase),
        new Regex(@"prompt.*?(\d+).*?tokens.*?exceeds.*?(\d+)", RegexOptions.IgnoreCase),
        new Regex(@"(\d+).*?tokens.*?limit.*?(\d+)", RegexOptions.IgnoreCase),
        new Regex(@"context.*?length.*?(\d+).*?maximum.*?(\d+)", RegexOptions.IgnoreCase),
        new Regex(@"max.*?context.*?(\d+).*?but.*?(\d+)", RegexOptions.IgnoreCase),
    ];

    private static readonly string[] TokenLimitKeywords =
        ["prompt is too long", "is too long", "context_length_exceeded", "max_tokens",
         "token limit", "context length", "too many tokens", "non-empty content"];

    private static readonly Regex[] ThinkingBlockErrorPatterns =
    [
        new Regex("thinking.*first block", RegexOptions.IgnoreCase),
        new Regex("first block.*thinking", RegexOptions.IgnoreCase),
        new Regex("must.*start.*thinking", RegexOptions.IgnoreCase),
        new Regex("thinking.*redacted_thinking", RegexOptions.IgnoreCase),
        new Regex("expected.*thinking.*found", RegexOptions.IgnoreCase),
        new Regex("thinking.*disabled.*cannot.*contain", RegexOptions.IgnoreCase),
    ];

    public static bool IsTokenLimitErrorText(string text)
    {
        if (ThinkingBlockErrorPatterns.Any(p => p.IsMatch(text))) return false;
        var lower = text.ToLowerInvariant();
        return TokenLimitKeywords.Any(kw => lower.Contains(kw));
    }

    public static ParsedTokenLimitError? ParseAnthropicTokenLimitError(object? error)
    {
        var textSources = CollectTokenLimitTextSources(error);
        if (textSources.Count == 0) return null;
        var combinedText = string.Join(" ", textSources);
        if (!IsTokenLimitErrorText(combinedText)) return null;
        if (combinedText.ToLowerInvariant().Contains("non-empty content"))
            return new ParsedTokenLimitError(0, 0, "non-empty content",
                MessageIndex: ExtractTokenLimitMessageIndex(combinedText));
        foreach (var text in textSources)
        {
            var tokens = ExtractTokensFromLimitMessage(text);
            if (tokens is not null)
                return new ParsedTokenLimitError(tokens.Value.Current, tokens.Value.Max, "token_limit_exceeded",
                    RequestId: ExtractRequestId(text));
        }
        return new ParsedTokenLimitError(0, 0, "token_limit_exceeded_unknown");
    }

    public static string FormatBytes(long bytes) =>
        bytes < 1024 ? $"{bytes}B" :
        bytes < 1024 * 1024 ? $"{(bytes / 1024.0):F1}KB" :
        $"{(bytes / (1024.0 * 1024.0)):F1}MB";

    #endregion

    #region Compaction

    public static string BuildCompactionContextPrompt(string? history = null)
    {
        var prompt = "<system-directive type=\"compaction_context\">\n\nWhen summarizing this session, you MUST include the following sections in your summary:\n\n## 1. User Requests (As-Is)\n## 2. Final Goal\n## 3. Work Completed\n## 4. Remaining Tasks\n## 5. Active Working Context (For Seamless Continuation)\n## 6. Explicit Constraints (Verbatim Only)\n## 7. Agent Verification State (Critical for Reviewers)\n## 8. Delegated Agent Sessions\n";
        return history is not null ? $"{prompt}\n### Active/Recent Delegated Sessions\n{history}\n" : prompt;
    }

    #endregion

    #region Tail Monitor

    public static TailMonitorState CreateTailMonitorState() => new();

    public static int FinalizeTrackedAssistantMessage(TailMonitorState state)
    {
        if (state.CurrentMessageID is null) return state.ConsecutiveNoTextMessages;
        state.ConsecutiveNoTextMessages = state.CurrentHasOutput ? 0 : state.ConsecutiveNoTextMessages + 1;
        state.CurrentMessageID = null;
        state.CurrentHasOutput = false;
        return state.ConsecutiveNoTextMessages;
    }

    public static bool ShouldTreatAssistantPartAsOutput(string? type, string? text) =>
        type == "text" ? !string.IsNullOrWhiteSpace(text?.Trim()) :
        type is "reasoning" or "tool" or "tool_use";

    public static void TrackAssistantOutput(TailMonitorState state, string? messageID = null)
    {
        if (messageID is not null && state.CurrentMessageID is null) state.CurrentMessageID = messageID;
        state.CurrentHasOutput = true;
        state.ConsecutiveNoTextMessages = 0;
    }

    #endregion

    #region Preemptive Compaction

    public static (bool ShouldRun, double UsageRatio) ShouldRunPreemptiveCompaction(
        ContextTokenInfoWithModel? cached, long? actualLimit, bool compacted,
        bool inProgress, long? lastCompactionTime, long now)
    {
        if (compacted || inProgress || cached is null || actualLimit is null || cached.ModelID is null)
            return (false, 0);
        if (lastCompactionTime.HasValue && now - lastCompactionTime.Value < HookDefinitions.PreemptiveCompactionCooldownMs)
            return (false, 0);
        var usageRatio = (double)((cached.Input ?? 0) + (cached.Cache?.Read ?? 0)) / actualLimit.Value;        return (usageRatio >= HookDefinitions.PreemptiveCompactionThreshold, usageRatio);
    }

    public static (string Title, string Message, string Variant, int Duration) BuildPreemptiveCompactionFailureToast(object? error) =>
        ("Preemptive compaction failed",
         $"Context window is above {Math.Round(HookDefinitions.PreemptiveCompactionThreshold * 100)}% and auto-compaction could not run. The session may grow large. Error: {error}",
         "warning", 10000);

    #endregion

    #region Todo Helpers

    public static List<TodoSnapshot> ExtractTodos(object? response)
    {
        if (response is JsonElement element) return ExtractTodos(element);
        if (response is Dictionary<string, object> dict && dict.TryGetValue("data", out var data) && data is List<TodoSnapshot> todoList)
            return todoList;
        if (response is List<TodoSnapshot> list) return list;
        return [];
    }

    public static List<TodoSnapshot> ExtractTodos(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<TodoSnapshot>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.TryGetProperty("content", out var content))
                {
                    list.Add(new TodoSnapshot(
                        item.TryGetProperty("id", out var id) ? id.GetString() : null,
                        content.GetString() ?? string.Empty,
                        item.TryGetProperty("status", out var status) ? status.GetString() : "pending",
                        item.TryGetProperty("priority", out var priority) ? priority.GetString() : null
                    ));
                }
            }
            return list;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("todos", out var todos)) return ExtractTodos(todos);
            if (element.TryGetProperty("items", out var items)) return ExtractTodos(items);
            if (element.TryGetProperty("data", out var data)) return ExtractTodos(data);
        }
        return [];
    }

    public static bool HasDetailedTodos(IEnumerable<TodoSnapshot> todos) =>
        todos.Any(t => !IsAtlasBootstrapTodo(t));

    public static bool IsAtlasBootstrapTodoList(IEnumerable<TodoSnapshot> todos)
    {
        var list = todos.ToList();
        return list.Count > 0 && list.All(IsAtlasBootstrapTodo);
    }

    public static bool ShouldRestoreOverCurrentTodos(List<TodoSnapshot> snapshot, List<TodoSnapshot> currentTodos)
    {
        if (currentTodos.Count == 0) return true;
        if (!IsAtlasBootstrapTodoList(currentTodos)) return false;
        return HasDetailedTodos(snapshot);
    }

    public static List<TodoSnapshot> ReplaceAtlasBootstrapTodos(List<TodoSnapshot> requestedTodos, List<TodoSnapshot> snapshot) =>
        IsAtlasBootstrapTodoList(requestedTodos) && HasDetailedTodos(snapshot) ? snapshot : requestedTodos;

    private static bool IsAtlasBootstrapTodo(TodoSnapshot todo) =>
        todo.Id == "orchestrate-plan" || todo.Id == "pass-final-wave" ||
        todo.Content == "Complete ALL implementation tasks" ||
        todo.Content == "Pass Final Verification Wave - ALL reviewers APPROVE";

    public static string GetTodoProgressSnapshot(IEnumerable<TodoSnapshot> todos) =>
        string.Join("|", todos
            .Select(t => (Key: t.Id ?? $"{t.Content}:{t.Priority}", Status: t.Status ?? ""))
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}={x.Status}"));

    public static (string? NextSnapshot, bool HasProgressed, int StagnationCount) TrackContinuationProgress(
        ContinuationState state, int incompleteCount, string? previousSnapshot = null, IEnumerable<TodoSnapshot>? todos = null)
    {
        var previousIncompleteCount = state.LastIncompleteCount;
        var nextSnapshot = todos is not null ? GetTodoProgressSnapshot(todos) : null;
        state.LastIncompleteCount = incompleteCount;

        var hasProgressed = previousIncompleteCount.HasValue &&
            (incompleteCount < previousIncompleteCount.Value ||
             (nextSnapshot is not null && previousSnapshot is not null && nextSnapshot != previousSnapshot));

        if (hasProgressed)
        {
            state.StagnationCount = 0;
            state.AwaitingPostInjectionProgressCheck = false;
        }
        else if (previousIncompleteCount.HasValue && state.AwaitingPostInjectionProgressCheck == true)
        {
            state.StagnationCount += 1;
            state.AwaitingPostInjectionProgressCheck = false;
        }

        return (nextSnapshot, hasProgressed, state.StagnationCount);
    }

    #endregion

    #region Image Resizer

    public static ImageDimensions? CalculateTargetDimensions(int width, int height, int maxLongEdge = 1568)
    {
        if (width <= 0 || height <= 0 || maxLongEdge <= 0) return null;
        var longEdge = Math.Max(width, height);
        if (longEdge <= maxLongEdge) return null;
        return width >= height
            ? new ImageDimensions(maxLongEdge, Math.Max(1, (int)Math.Floor((double)height * maxLongEdge / width)))
            : new ImageDimensions(Math.Max(1, (int)Math.Floor((double)width * maxLongEdge / height)), maxLongEdge);
    }

    public static int CalculateImageTokens(int width, int height) =>
        (int)Math.Ceiling((double)width * height / 750);

    public static string FormatImageResizeAppendix(
        IEnumerable<(string Filename, ImageDimensions? OriginalDims, ImageDimensions? ResizedDims, string Status)> entries)
    {
        var entryList = entries.ToList();
        var header = entryList.Any(e => e.Status == "resized") ? "[Image Resize Info]" : "[Image Info]";
        var lines = new List<string> { $"\n\n{header}" };

        foreach (var entry in entryList)
        {
            if (entry.Status == "unknown-dims" || entry.OriginalDims is null)
            {
                lines.Add($"- {entry.Filename}: dimensions could not be parsed");
                continue;
            }
            var origText = $"{entry.OriginalDims.Width}x{entry.OriginalDims.Height}";
            var origTokens = CalculateImageTokens(entry.OriginalDims.Width, entry.OriginalDims.Height);
            lines.Add(entry.Status switch
            {
                "within-limits" => $"- {entry.Filename}: {origText} (within limits, tokens: {origTokens})",
                "resize-skipped" => $"- {entry.Filename}: {origText} (exceeds provider limits, image removed to prevent API error)",
                _ when entry.ResizedDims is null => $"- {entry.Filename}: {origText} (resize skipped, tokens: {origTokens})",
                _ => $"- {entry.Filename}: {origText} -> {entry.ResizedDims!.Width}x{entry.ResizedDims.Height} (resized, tokens: {origTokens} -> {CalculateImageTokens(entry.ResizedDims.Width, entry.ResizedDims.Height)})",
            });
        }
        return string.Join("\n", lines);
    }

    #endregion

    #region Private Helpers

    private static List<string> CollectTokenLimitTextSources(object? error)
    {
        if (error is string s) return [s];
        if (error is null || error is not Dictionary<string, object> dict) return [];
        var candidates = new List<string>();
        if (dict.GetValueOrDefault("message") is string msg) candidates.Add(msg);
        if (dict.GetValueOrDefault("body") is string body) candidates.Add(body);
        if (dict.GetValueOrDefault("details") is string details) candidates.Add(details);
        if (dict.GetValueOrDefault("reason") is string reason) candidates.Add(reason);
        if (dict.GetValueOrDefault("description") is string desc) candidates.Add(desc);
        if (candidates.Count > 0) return candidates;
        try
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(dict);
            return IsTokenLimitErrorText(serialized) ? [serialized] : [];
        }
        catch { return []; }
    }

    private static (int Current, int Max)? ExtractTokensFromLimitMessage(string message)
    {
        foreach (var pattern in TokenLimitPatterns)
        {
            var match = pattern.Match(message);
            if (!match.Success) continue;
            var first = int.Parse(match.Groups[1].Value);
            var second = int.Parse(match.Groups[2].Value);
            return first > second ? (first, second) : (second, first);
        }
        return null;
    }

    private static int? ExtractTokenLimitMessageIndex(string text)
    {
        var match = Regex.Match(text, @"messages\.(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string? ExtractRequestId(string text)
    {
        var match = Regex.Match(text, @"""request_id""\s*:\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion
}

public sealed record ContextTokenInfoWithModel(
    int? Input, int? Output = null, int? Reasoning = null,
    CacheInfo? Cache = null, string? ModelID = null);
