namespace Omodot.Hooks;

using System.Text.RegularExpressions;

public static partial class OutputRecovery
{
    #region Empty Task Response

    public static string RecoverEmptyTaskOutput(string tool, string output) =>
        (tool is "Task" or "task") && string.IsNullOrWhiteSpace(output)
            ? HookDefinitions.EmptyTaskResponseWarning
            : output;

    #endregion

    #region JSON Error Recovery

    private static readonly HashSet<string> JsonErrorExcludedTools =
        new(HookDefinitions.JsonErrorToolExcludeList, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("json parse error", RegexOptions.IgnoreCase)]
    private static partial Regex JsonParseErrorPattern();
    [GeneratedRegex("failed to parse json", RegexOptions.IgnoreCase)]
    private static partial Regex FailedToParseJsonPattern();
    [GeneratedRegex("invalid json", RegexOptions.IgnoreCase)]
    private static partial Regex InvalidJsonPattern();
    [GeneratedRegex("malformed json", RegexOptions.IgnoreCase)]
    private static partial Regex MalformedJsonPattern();
    [GeneratedRegex("unexpected end of json input", RegexOptions.IgnoreCase)]
    private static partial Regex UnexpectedEndJsonPattern();
    [GeneratedRegex(@"syntaxerror:\s*unexpected token.*json", RegexOptions.IgnoreCase)]
    private static partial Regex UnexpectedTokenJsonPattern();
    [GeneratedRegex(@"json[^\n]*expected '\}'" , RegexOptions.IgnoreCase)]
    private static partial Regex JsonExpectedBracePattern();
    [GeneratedRegex("json[^\n]*unexpected eof", RegexOptions.IgnoreCase)]
    private static partial Regex JsonUnexpectedEofPattern();

    private static bool IsJsonError(string output) =>
        JsonParseErrorPattern().IsMatch(output) || FailedToParseJsonPattern().IsMatch(output) ||
        InvalidJsonPattern().IsMatch(output) || MalformedJsonPattern().IsMatch(output) ||
        UnexpectedEndJsonPattern().IsMatch(output) || UnexpectedTokenJsonPattern().IsMatch(output) ||
        JsonExpectedBracePattern().IsMatch(output) || JsonUnexpectedEofPattern().IsMatch(output);

    public static string RecoverJsonErrorOutput(string tool, string output)
    {
        if (JsonErrorExcludedTools.Contains(tool) || output.Contains(HookDefinitions.JsonErrorReminderMarker))
            return output;
        return IsJsonError(output) ? $"{output}\n{HookDefinitions.JsonErrorReminder}" : output;
    }

    #endregion

    #region Edit Error Recovery

    public static string RecoverEditErrorOutput(string tool, string output)
    {
        if (!tool.Equals("edit", StringComparison.OrdinalIgnoreCase)) return output;
        var lowered = output.ToLowerInvariant();
        return HookDefinitions.EditErrorPatterns.Any(p => lowered.Contains(p.ToLowerInvariant()))
            ? $"{output}\n{HookDefinitions.EditErrorReminder}"
            : output;
    }

    #endregion

    #region Tool Output Truncation

    private static readonly HashSet<string> TruncatableTools =
        ["grep", "Grep", "safe_grep", "glob", "Glob", "safe_glob",
         "lsp_diagnostics", "ast_grep_search", "interactive_bash", "Interactive_bash",
         "skill_mcp", "webfetch", "WebFetch"];

    private const int DefaultMaxTokens = 50_000;
    private const int WebfetchMaxTokens = 10_000;

    public static (string Output, bool Truncated) TruncateToolOutput(
        string tool, string output, bool truncateAll = false, int? maxTokens = null)
    {
        if (!truncateAll && !TruncatableTools.Contains(tool))
            return (output, false);

        var effectiveMax = maxTokens ??
            (tool is "webfetch" or "WebFetch" ? WebfetchMaxTokens : DefaultMaxTokens);
        var maxChars = effectiveMax * 4;

        if (output.Length <= maxChars) return (output, false);

        return ($"{output[..maxChars]}\n\n[Tool output truncated to {effectiveMax} tokens]", true);
    }

    #endregion

    #region Delegate Task Error

    private static readonly (string Pattern, string ErrorType, string FixHint)[] DelegateTaskErrorPatterns =
    [
        ("run_in_background", "missing_run_in_background",
            "Add run_in_background=false (for delegation) or run_in_background=true (for parallel exploration)"),
        ("load_skills", "missing_load_skills",
            "Add load_skills=[] parameter (empty array if no skills needed)"),
        ("category OR subagent_type", "mutual_exclusion",
            "Provide ONLY one of: category OR subagent_type"),
        ("Must provide either category or subagent_type", "missing_category_or_agent",
            "Add either category='general' OR subagent_type='explore'"),
        ("Unknown category", "unknown_category",
            "Use a valid category from the Available list in the error message"),
        ("Agent name cannot be empty", "empty_agent",
            "Provide a non-empty subagent_type value"),
        ("Unknown agent", "unknown_agent",
            "Use a valid agent from the Available agents list"),
        ("Cannot call primary agent", "primary_agent",
            "Primary agents cannot be called via task. Use a subagent"),
        ("Skills not found", "unknown_skills",
            "Use valid skill names from the Available list"),
    ];

    public static DelegateTaskErrorInfo? DetectDelegateTaskError(string output)
    {
        if (!output.Contains("[ERROR]") && !output.Contains("Invalid arguments")) return null;
        var match = DelegateTaskErrorPatterns.FirstOrDefault(p => output.Contains(p.Pattern));
        return match != default ? new DelegateTaskErrorInfo(match.ErrorType, output) : null;
    }

    public static string AddDelegateTaskRetryGuidance(string tool, string output)
    {
        if (!tool.Equals("task", StringComparison.OrdinalIgnoreCase)) return output;
        var error = DetectDelegateTaskError(output);
        if (error is null) return output;

        var pattern = DelegateTaskErrorPatterns.FirstOrDefault(p => p.ErrorType == error.ErrorType);
        return $"""
            {output}

             [task CALL FAILED - IMMEDIATE RETRY REQUIRED]

             **Error Type**: {error.ErrorType}
             **Fix**: {pattern.FixHint}
             **Action**: Retry task NOW with corrected parameters.
            """;
    }

    #endregion

    #region Task Resume Info

    public static string AppendTaskResumeInfo(string tool, string output, object? metadata)
    {
        if (!new[] { "task", "Task", "task_tool", "call_omo_agent" }.Contains(tool)) return output;
        if (output.StartsWith("Error:") || output.StartsWith("Failed") || output.Contains("\nto continue:"))
            return output;

        var taskID = ExtractTaskId(metadata) ?? ExtractTaskIdFromText(output);
        return taskID is not null
            ? $"{output.TrimEnd()}\n\nto continue: task(task_id=\"{taskID}\", load_skills=[], run_in_background=false, prompt=\"...\")"
            : output;
    }

    private static string? ExtractTaskId(object? metadata)
    {
        if (metadata is null || metadata is not Dictionary<string, object> dict) return null;
        foreach (var key in new[] { "taskId", "taskID", "task_id", "sessionId", "sessionID", "session_id" })
        {
            if (dict.TryGetValue(key, out var value) && value is string s && s.Trim().Length > 0)
                return s.Trim();
        }
        return null;
    }

    [GeneratedRegex(@"(?:task_id|session_id):\s*([a-zA-Z0-9_-]+)")]
    private static partial Regex TaskIdFromTextPattern();
    [GeneratedRegex(@"Session ID:\s*(ses_[a-zA-Z0-9_-]+)")]
    private static partial Regex SessionIdFromTextPattern();

    private static string? ExtractTaskIdFromText(string output)
    {
        var match = TaskIdFromTextPattern().Match(output);
        if (match.Success) return match.Groups[1].Value;
        match = SessionIdFromTextPattern().Match(output);
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion

    #region Session Recovery

    public static string? DetectErrorType(object? error)
    {
        var message = GetRecoveryErrorMessage(error);
        if (message.Contains("assistant message prefill") || message.Contains("conversation must end with a user message"))
            return RecoveryErrorType.AssistantPrefillUnsupported;
        if (message.Contains("thinking") && (message.Contains("first block") || message.Contains("must start with") ||
            message.Contains("preceeding") || message.Contains("final block") ||
            message.Contains("cannot be thinking") || (message.Contains("expected") && message.Contains("found"))))
            return RecoveryErrorType.ThinkingBlockOrder;
        if (message.Contains("thinking") && message.Contains("cannot be modified"))
            return RecoveryErrorType.ThinkingBlockModified;
        if (message.Contains("thinking is disabled") && message.Contains("cannot contain"))
            return RecoveryErrorType.ThinkingDisabledViolation;
        if (message.Contains("tool_use") && message.Contains("tool_result"))
            return RecoveryErrorType.ToolResultMissing;
        if (message.Contains("dummy_tool") || message.Contains("unavailable tool") ||
            message.Contains("model tried to call unavailable") || message.Contains("nosuchtoolerror") ||
            message.Contains("no such tool"))
            return RecoveryErrorType.UnavailableTool;
        return null;
    }

    [GeneratedRegex(@"messages\.(\d+)")]
    private static partial Regex MessageIndexPattern();

    public static int? ExtractMessageIndex(object? error)
    {
        var match = MessageIndexPattern().Match(GetRecoveryErrorMessage(error));
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"(?:unavailable tool|no such tool)[:\s'""]+([^'"".\s]+)")]
    private static partial Regex UnavailableToolPattern();

    public static string? ExtractUnavailableToolName(object? error)
    {
        var match = UnavailableToolPattern().Match(GetRecoveryErrorMessage(error));
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string GetRecoveryErrorMessage(object? error)
    {
        if (error is null) return "";
        if (error is string s) return s.ToLowerInvariant();
        if (error is not Dictionary<string, object> dict) return "";

        foreach (var obj in new object?[] { dict.GetValueOrDefault("data"), dict.GetValueOrDefault("error"), dict, (dict.GetValueOrDefault("data") as Dictionary<string, object>)?.GetValueOrDefault("error") })
        {
            if (obj is Dictionary<string, object> d && d.TryGetValue("message", out var msg) && msg is string ms && ms.Length > 0)
                return ms.ToLowerInvariant();
        }

        try { return (dict.TryGetValue("message", out var m) && m is string ? m.ToString() : "")?.ToLowerInvariant() ?? ""; }
        catch { return ""; }
    }

    #endregion

    #region Runtime Fallback

    private static readonly Regex[] RuntimeRetryableErrorPatterns =
    [
        new(@"rate.?limit", RegexOptions.IgnoreCase),
        new(@"too.?many.?requests", RegexOptions.IgnoreCase),
        new(@"quota\s+will\s+reset\s+after", RegexOptions.IgnoreCase),
        new(@"quota.?exceeded", RegexOptions.IgnoreCase),
        new(@"service.?unavailable", RegexOptions.IgnoreCase),
        new(@"overloaded", RegexOptions.IgnoreCase),
        new(@"temporarily.?unavailable", RegexOptions.IgnoreCase),
        new(@"try.?again", RegexOptions.IgnoreCase),
        new(@"(?:^|\s)429(?:\s|$)"),
        new(@"(?:^|\s)503(?:\s|$)"),
        new(@"(?:^|\s)529(?:\s|$)"),
    ];

    public static string GetRuntimeFallbackErrorMessage(object? error) => GetRecoveryErrorMessage(error);

    public static int? ExtractRuntimeFallbackStatusCode(object? error, int[]? retryOnErrors = null)
    {
        retryOnErrors ??= [429, 500, 502, 503, 504];
        var direct = ExtractStatusCodeFromObject(error);
        if (direct is not null) return direct;
        var message = GetRuntimeFallbackErrorMessage(error);
        var pattern = new Regex($@"\b({string.Join("|", retryOnErrors)})\b");
        var match = pattern.Match(message);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    public static string? ClassifyRuntimeFallbackErrorType(object? error)
    {
        var message = GetRuntimeFallbackErrorMessage(error);
        var name = ExtractRuntimeFallbackErrorName(error)?.ToLowerInvariant().Replace("_", "").Replace("-", "");

        if (name?.Contains("loadapi") == true ||
            (Regex.IsMatch(message, @"api.?key.?is.?missing", RegexOptions.IgnoreCase) &&
             Regex.IsMatch(message, @"environment variable", RegexOptions.IgnoreCase)))
            return "missing_api_key";
        if (Regex.IsMatch(message, @"api.?key", RegexOptions.IgnoreCase) && Regex.IsMatch(message, @"must be a string", RegexOptions.IgnoreCase))
            return "invalid_api_key";
        if (name?.Contains("modelnotfound") == true || Regex.IsMatch(message, @"model\s+not\s+found", RegexOptions.IgnoreCase))
            return "model_not_found";
        if (name?.Contains("quotaexceeded") == true || name?.Contains("resourceexhausted") == true ||
            Regex.IsMatch(message, @"quota.?exceeded|insufficient.?quota|out\s+of\s+credits?|payment.?required", RegexOptions.IgnoreCase))
            return "quota_exceeded";
        return null;
    }

    public static bool IsRuntimeFallbackRetryableError(object? error, int[]? retryOnErrors = null)
    {
        retryOnErrors ??= [429, 500, 502, 503, 504];
        var type = ClassifyRuntimeFallbackErrorType(error);
        if (type is "missing_api_key" or "model_not_found" or "quota_exceeded") return true;
        var statusCode = ExtractRuntimeFallbackStatusCode(error, retryOnErrors);
        if (statusCode is not null && retryOnErrors.Contains(statusCode.Value)) return true;
        var message = GetRuntimeFallbackErrorMessage(error);
        return RuntimeRetryableErrorPatterns.Any(p => p.IsMatch(message));
    }

    public static (bool HasError, string? ErrorMessage) ContainsRuntimeFallbackErrorContent(
        IEnumerable<(string? Type, string? Text)>? parts)
    {
        var errors = (parts ?? [])
            .Where(p => p.Type == "error")
            .Select(p => p.Text)
            .Where(t => t is not null)
            .Cast<string>()
            .ToList();
        return errors.Count > 0 ? (true, string.Join("\n", errors)) : (false, null);
    }

    private static int? ExtractStatusCodeFromObject(object? error)
    {
        if (error is null || error is not Dictionary<string, object> dict) return null;
        foreach (var key in new[] { "statusCode", "status" })
        {
            if (dict.TryGetValue(key, out var v) && v is int i) return i;
        }
        if (dict.TryGetValue("data", out var data) && data is Dictionary<string, object> dd && dd.TryGetValue("statusCode", out var ds) && ds is int dsi) return dsi;
        if (dict.TryGetValue("error", out var err) && err is Dictionary<string, object> ed && ed.TryGetValue("statusCode", out var es) && es is int esi) return esi;
        if (dict.TryGetValue("cause", out var cause) && cause is Dictionary<string, object> cd && cd.TryGetValue("statusCode", out var cs) && cs is int csi) return csi;
        return null;
    }

    private static string? ExtractRuntimeFallbackErrorName(object? error)
    {
        if (error is null || error is not Dictionary<string, object> dict) return null;
        foreach (var obj in new object?[] { dict.GetValueOrDefault("name"),
            (dict.GetValueOrDefault("data") as Dictionary<string, object>)?.GetValueOrDefault("name"),
            (dict.GetValueOrDefault("error") as Dictionary<string, object>)?.GetValueOrDefault("name"),
            ((dict.GetValueOrDefault("data") as Dictionary<string, object>)?.GetValueOrDefault("error") as Dictionary<string, object>)?.GetValueOrDefault("name") })
        {
            if (obj is string s && s.Length > 0) return s;
        }
        return null;
    }

    #endregion
}
