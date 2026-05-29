namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class TeamTools
{
    #region Team Tool Gating

    private static readonly HashSet<string> UniversalTeamToolNames =
        ["team_send_message", "team_task_create", "team_task_list",
         "team_task_update", "team_task_get", "team_status"];

    public static bool IsUniversalTeamTool(string toolName) =>
        UniversalTeamToolNames.Contains(toolName);

    public static string? ResolveTeamToolGate(string toolName, TeamParticipant participant, Dictionary<string, object> args)
    {
        if (!toolName.StartsWith("team_") && toolName != "delegate-task") return null;
        if (toolName == "delegate-task" || toolName == "team_list") return null;

        if (toolName == "team_create")
            return participant.Role == "neither" ? null
                : $"team_create denied: session is already a participant of team {participant.TeamRunId}";

        var teamRunId = args.GetValueOrDefault("teamRunId") as string;
        var memberName = args.GetValueOrDefault("memberName") as string;

        if (toolName == "team_delete" || toolName == "team_shutdown_request")
            return participant.Role == "lead" && participant.TeamRunId == teamRunId ? null
                : $"{toolName} is lead-only";

        if (toolName == "team_approve_shutdown" || toolName == "team_reject_shutdown")
        {
            var isLead = participant.Role == "lead" && participant.TeamRunId == teamRunId;
            var isTargetMember = participant.Role == "member" && participant.TeamRunId == teamRunId &&
                participant.MemberName == memberName;
            return isLead || isTargetMember ? null
                : $"{toolName}: caller must be target member or team lead";
        }

        if (IsUniversalTeamTool(toolName))
        {
            var participantInTeam = (participant.Role is "lead" or "member") && participant.TeamRunId == teamRunId;
            if (participantInTeam) return null;
            return teamRunId is null
                ? $"team-mode tool {toolName} requires teamRunId argument"
                : $"team-mode tool {toolName} denied: not a participant of team {teamRunId}";
        }

        return null;
    }

    #endregion

    #region Team Mailbox

    public static string BuildTeamMailboxTurnMarker(string sessionID, List<MessageWithParts> messages) =>
        $"{sessionID}#{messages.Count}";

    public static List<MessageWithParts> InjectTeamMailboxMessage(
        List<MessageWithParts> messages, string sessionID, string content)
    {
        var injected = CreateSyntheticUserMessage(sessionID, content);
        var lastUserIndex = FindLastUserMessageIndex(messages);
        var next = messages.ToList();
        if (lastUserIndex == -1) next.Insert(0, injected);
        else next.Insert(lastUserIndex, injected);
        return next;
    }

    #endregion

    #region Team Mode Status

    private const string TeamModeStatusMarker = "<team_mode_status enabled=\"true\">";

    public static string BuildTeamModeStatusContent() =>
        $"{TeamModeStatusMarker}\nTeam mode is ENABLED for this session.\n" +
        "If the team_* tools are present, that is authoritative proof that team mode is active.\n" +
        "Do not inspect ~/.config/opencode or project config files to verify team mode.\n" +
        "If you need usage guidance, load the team-mode skill. Otherwise use the team_* tools directly.\n</team_mode_status>";

    public static List<MessageWithParts> InjectTeamModeStatus(List<MessageWithParts> messages, string sessionID)
    {
        if (messages.Any(m => m.Parts.Any(p =>
            p.Synthetic == true && p.Type == "text" && p.Text?.Contains(TeamModeStatusMarker) == true)))
            return messages;

        var lastUserIndex = FindLastUserMessageIndex(messages);
        if (lastUserIndex == -1) return messages;

        var next = messages.ToList();
        next.Insert(lastUserIndex, CreateSyntheticUserMessage(sessionID, BuildTeamModeStatusContent()));
        return next;
    }

    #endregion

    #region Thinking Block Validator

    public static void RepairThinkingBlockMessages(List<MessageWithParts> messages)
    {
        if (messages.Count == 0 || !HasSignedThinkingBlocksInHistory(messages)) return;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (message.Info.Role != "assistant") continue;
            if (HasContentParts(message.Parts) && !StartsWithThinkingBlock(message.Parts))
            {
                var thinkingPart = FindPreviousThinkingPart(messages, i);
                if (thinkingPart is not null) message.Parts.Insert(0, thinkingPart);
            }
        }
    }

    public static bool HasSignedThinkingBlocksInHistory(List<MessageWithParts> messages) =>
        messages.Any(m => m.Info.Role == "assistant" && m.Parts.Any(IsSignedThinkingPart));

    public static void RepairMissingToolResults(List<MessageWithParts> messages)
    {
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (message.Info.Role != "assistant") continue;
            var toolUseIds = ExtractUniqueToolUseIds(message.Parts);
            if (toolUseIds.Count == 0) continue;

            var next = i + 1 < messages.Count ? messages[i + 1] : null;
            if (next?.Info.Role != "user")
            {
                var sessionID = message.Info.SessionID;
                var newMsg = new MessageWithParts(
                    new MessageInfo("user", sessionID is not null
                        ? new Dictionary<string, object> { ["sessionID"] = sessionID } : null),
                    toolUseIds.Select(CreateToolResultPart).ToList());
                messages.Insert(i + 1, newMsg);
                continue;
            }

            var existing = new HashSet<string?>(
                next.Parts.Select(GetToolResultId).Where(id => id is not null));
            var missing = toolUseIds.Where(id => !existing.Contains(id)).ToList();
            if (missing.Count > 0)
                next.Parts.InsertRange(FindToolResultInsertIndex(next.Parts), missing.Select(CreateToolResultPart));
        }
    }

    #endregion

    #region Background Task Helpers

    public static bool IsUnstableTask(BackgroundTaskLike task)
    {
        var modelID = task.Model?.ModelID?.ToLowerInvariant();
        return task.IsUnstableAgent || task.Description.Contains("unstable", StringComparison.OrdinalIgnoreCase) || (modelID is not null &&
            (modelID.Contains("gemini") || modelID.Contains("minimax")));
    }

    public static string BuildUnstableAgentReminder(BackgroundTaskLike task, string? summary, long idleMs) =>
        $"""
        Unstable background agent appears idle for {Math.Round(idleMs / 1000.0)}s.

        Task ID: {task.Id}
        Description: {task.Description}
        Agent: {task.Agent}
        Status: {task.Status}
        Session ID: {task.SessionId ?? "N/A"}

        Thinking summary (first {HookDefinitions.ThinkingSummaryMaxChars} chars):
        {summary ?? "(No thinking trace available)"}

        Suggested actions:
        - background_output task_id="{task.Id}" full_session=true include_thinking=true include_tool_results=true message_limit=50
        - background_cancel taskId="{task.Id}"

        This is a reminder only. No automatic action was taken.
        """;

    public static (string Role, string? Agent, ModelRef? Model) GetMessageInfo(object? value)
    {
        if (value is not Dictionary<string, object> dict) return default;
        if (!dict.TryGetValue("info", out var infoObj) || infoObj is not Dictionary<string, object> info)
            return default;

        string? role = info.GetValueOrDefault("role") as string;
        string? agent = info.GetValueOrDefault("agent") as string;
        ModelRef? model = null;
        if (info.GetValueOrDefault("model") is Dictionary<string, object> modelDict &&
            modelDict.GetValueOrDefault("providerID") is string pid &&
            modelDict.GetValueOrDefault("modelID") is string mid)
        {
            var variant = modelDict.GetValueOrDefault("variant") as string;
            model = new ModelRef(pid, mid);
        }
        return (role ?? "", agent, model);
    }

    #endregion

    #region Tmux Command Parsing

    public static (string SubCommand, string? SessionName) ParseTmuxCommand(string tmuxCommand)
    {
        var tokens = TokenizeTmuxCommand(tmuxCommand);
        var subCommand = FindTmuxSubcommand(tokens);
        var sessionName = ExtractTmuxSessionName(tokens, subCommand);
        return (subCommand, sessionName);
    }

    public static bool IsLfeTmuxSession(string? sessionName) =>
        sessionName is not null && sessionName.StartsWith("lfe-");

    public static string BuildInteractiveBashSessionReminder(IEnumerable<string> sessions)
    {
        var list = sessions.ToList();
        return list.Count == 0 ? "" :
            $"\n\n[System Reminder] Active lfe-* tmux sessions: {string.Join(", ", list)}";
    }

    #endregion

    #region Private Helpers

    private static bool IsSignedThinkingPart(MessagePart part) =>
        (part.Type == "thinking" || part.Type == "redacted_thinking") &&
        part.Signature is not null && part.Signature.Length > 0 && part.Synthetic != true;

    private static bool HasContentParts(List<MessagePart> parts) =>
        parts.Any(p => p.Type is "tool" or "tool_use" or "text");

    private static bool StartsWithThinkingBlock(List<MessagePart> parts) =>
        parts.Count > 0 && parts[0].Type is "thinking" or "redacted_thinking" or "reasoning";

    private static MessagePart? FindPreviousThinkingPart(List<MessageWithParts> messages, int currentIndex)
    {
        for (var i = currentIndex - 1; i >= 0; i--)
        {
            if (messages[i].Info.Role != "assistant") continue;
            var thinkingPart = messages[i].Parts.FirstOrDefault(IsSignedThinkingPart);
            if (thinkingPart is not null) return thinkingPart;
        }
        return null;
    }

    private static string? GetToolUseId(MessagePart part)
    {
        if (part.Type == "tool_use" && part.Id is string id && id.Length > 0) return id;
        if (part.Type == "tool" && part.CallID is string cid && cid.Length > 0) return cid;
        return null;
    }

    private static string? GetToolResultId(MessagePart part)
    {
        if (part.Type != "tool_result") return null;
        if (part.ToolUseId is string tui && tui.Length > 0) return tui;
        if (part.Extra?.TryGetValue("tool_use_id", out var v) == true && v is string s && s.Length > 0) return s;
        return null;
    }

    private static List<string> ExtractUniqueToolUseIds(List<MessagePart> parts)
    {
        var seen = new HashSet<string>();
        var ids = new List<string>();
        foreach (var part in parts)
        {
            var id = GetToolUseId(part);
            if (id is null || seen.Contains(id)) continue;
            seen.Add(id);
            ids.Add(id);
        }
        return ids;
    }

    private static MessagePart CreateToolResultPart(string toolUseId) =>
        new("tool_result", new Dictionary<string, object>
        {
            ["toolUseId"] = toolUseId,
            ["tool_use_id"] = toolUseId,
            ["isError"] = true,
            ["content"] = new[] { new Dictionary<string, object> { ["type"] = "text", ["text"] = HookDefinitions.ToolResultPlaceholder } }
        });

    private static int FindToolResultInsertIndex(List<MessagePart> parts)
    {
        var last = -1;
        for (var i = 0; i < parts.Count; i++)
        {
            if (GetToolResultId(parts[i]) is not null) last = i;
        }
        return last == -1 ? 0 : last + 1;
    }

    private static int FindLastUserMessageIndex(List<MessageWithParts> messages)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Info.Role == "user") return i;
        }
        return -1;
    }

    private static MessageWithParts CreateSyntheticUserMessage(string sessionID, string content) =>
        new(new MessageInfo("user", new Dictionary<string, object> { ["sessionID"] = sessionID }),
            [new MessagePart("text", new Dictionary<string, object> { ["text"] = content, ["synthetic"] = true })]);

    private static string? ResolveMessageSessionID(List<MessageWithParts> messages)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var sid = messages[i].Info.SessionID;
            if (sid is not null && sid.Length > 0) return sid;
        }
        return null;
    }

    private static string? ExtractSessionId(object? properties)
    {
        if (properties is null || properties is not Dictionary<string, object> dict) return null;
        if (dict.GetValueOrDefault("sessionID") is string sid) return sid;
        if (dict.GetValueOrDefault("id") is string id) return id;
        if (dict.GetValueOrDefault("session") is Dictionary<string, object> nested &&
            nested.GetValueOrDefault("id") is string nid) return nid;
        return null;
    }

    private static List<string> TokenizeTmuxCommand(string command)
    {
        var tokens = new List<string>();
        var current = "";
        char? quote = null;
        var escaped = false;
        foreach (var ch in command)
        {
            if (escaped) { current += ch; escaped = false; }
            else if (ch == '\\') { escaped = true; }
            else if ((ch == '\'' || ch == '"') && quote is null) { quote = ch; }
            else if (ch == quote) { quote = null; }
            else if (ch == ' ' && quote is null)
            {
                if (current.Length > 0) { tokens.Add(current); current = ""; }
            }
            else { current += ch; }
        }
        if (current.Length > 0) tokens.Add(current);
        return tokens;
    }

    private static string FindTmuxSubcommand(List<string> tokens)
    {
        var optionsWithArgs = new HashSet<string> { "-L", "-S", "-f", "-c", "-T" };
        for (var i = 0; i < tokens.Count;)
        {
            var token = tokens[i];
            if (token == "--") return i + 1 < tokens.Count ? tokens[i + 1] : "";
            if (optionsWithArgs.Contains(token)) { i += 2; }
            else if (token.StartsWith("-")) { i++; }
            else return token;
        }
        return "";
    }

    private static string? ExtractTmuxSessionName(List<string> tokens, string subCommand)
    {
        var flag = subCommand == "new-session"
            ? FindFlagValue(tokens, "-s") ?? FindFlagValue(tokens, "-t")
            : FindFlagValue(tokens, "-t");
        return flag?.Split(':')[0].Split('.')[0];
    }

    private static string? FindFlagValue(List<string> tokens, string flag)
    {
        var index = tokens.IndexOf(flag);
        return index >= 0 && index + 1 < tokens.Count ? tokens[index + 1] : null;
    }

    #endregion
}

public static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this Dictionary<TKey, TValue> dict, TKey key) where TKey : notnull =>
        dict.TryGetValue(key, out var value) ? value : default;
}
