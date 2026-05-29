namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class Notifications
{
    #region Notification Helpers

    public static string EscapeAppleScriptText(string input) =>
        input.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public static string EscapePowerShellSingleQuotedText(string input) =>
        input.Replace("'", "''");

    public static string BuildWindowsToastScript(string title, string message)
    {
        var psTitle = EscapePowerShellSingleQuotedText(title);
        var psMessage = EscapePowerShellSingleQuotedText(message);
        var lines = new[]
        {
            $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null",
            $"$Template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)",
            "$RawXml = [xml] $Template.GetXml()",
            $"($RawXml.toast.visual.binding.text | Where-Object {{$_.id -eq '1'}}).AppendChild($RawXml.CreateTextNode('{psTitle}')) | Out-Null",
            $"($RawXml.toast.visual.binding.text | Where-Object {{$_.id -eq '2'}}).AppendChild($RawXml.CreateTextNode('{psMessage}')) | Out-Null",
            "$SerializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument",
            "$SerializedXml.LoadXml($RawXml.OuterXml)",
            "$Toast = [Windows.UI.Notifications.ToastNotification]::new($SerializedXml)",
            "$Notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('OpenCode')",
            "$Notifier.Show($Toast)",
        };
        return string.Join("; ", lines);
    }

    public static NotificationPlatform NormalizeNotificationPlatform(string platform) =>
        platform switch { "darwin" => NotificationPlatform.Darwin, "linux" => NotificationPlatform.Linux,
            "win32" => NotificationPlatform.Win32, _ => NotificationPlatform.Unsupported };

    public static string GetDefaultNotificationSoundPath(NotificationPlatform platform) =>
        platform switch
        {
            NotificationPlatform.Darwin => "/System/Library/Sounds/Glass.aiff",
            NotificationPlatform.Linux => "/usr/share/sounds/freedesktop/stereo/complete.oga",
            NotificationPlatform.Win32 => "C:\\Windows\\Media\\notify.wav",
            _ => "",
        };

    private static readonly HashSet<string> BackgroundForwardedEventTypes =
        ["message.updated", "message.part.updated", "message.part.delta", "todo.updated",
         "session.idle", "session.error", "session.deleted", "session.status"];

    public static bool ShouldForwardBackgroundEvent(string eventType) =>
        BackgroundForwardedEventTypes.Contains(eventType);

    #endregion

    #region Session Notification

    public static string ExtractSessionNotificationText(SessionNotificationMessage? message) =>
        message?.Parts is null ? "" :
        string.Join("\n", message.Parts
            .Where(p => p.Type == "text" && p.Text is not null)
            .Select(p => p.Text!.Trim())
            .Where(t => t.Length > 0));

    public static SessionNotificationMessage? FindLastSessionNotificationMessage(
        IEnumerable<SessionNotificationMessage> messages, string role)
    {
        foreach (var message in messages.Reverse())
        {
            if (message.Info?.Role != role) continue;
            if (role == "assistant" && message.Info?.Error is not null) continue;
            if (!string.IsNullOrWhiteSpace(ExtractSessionNotificationText(message))) return message;
        }
        return null;
    }

    public static (string Title, string Message) BuildReadyNotificationContent(
        string sessionID, string? sessionTitle, string baseTitle, string baseMessage,
        IEnumerable<SessionNotificationMessage>? messages = null)
    {
        var msgs = messages ?? [];
        var lastUserText = CollapseWhitespace(ExtractSessionNotificationText(
            FindLastSessionNotificationMessage(msgs, "user")));
        var lastAssistantLine = GetLastNonEmptyLine(ExtractSessionNotificationText(
            FindLastSessionNotificationMessage(msgs, "assistant")));

        var detailLines = new List<string>();
        if (lastUserText.Length > 0) detailLines.Add($"User: {lastUserText}");
        if (lastAssistantLine.Length > 0) detailLines.Add($"Assistant: {lastAssistantLine}");

        var title = $"{baseTitle} · {sessionTitle?.Trim() ?? sessionID}";
        var msg = detailLines.Count > 0 ? $"{baseMessage}\n{string.Join("\n", detailLines)}" : baseMessage;
        return (title, msg);
    }

    #endregion

    #region Auto Update

    public static bool IsPrereleaseVersion(string version) => version.Contains("-");
    public static bool IsDistTag(string version) => !char.IsDigit(version[0]);
    public static bool IsPrereleaseOrDistTag(string? version) =>
        version is not null && (IsPrereleaseVersion(version) || IsDistTag(version));

    public static string ExtractChannel(string? version)
    {
        if (version is null) return "latest";
        if (IsDistTag(version)) return version;
        var parts = version.Split('-');
        if (parts.Length > 1)
        {
            var match = Regex.Match(parts[1], @"^(alpha|beta|rc|canary|next)");
            if (match.Success) return match.Groups[1].Value;
        }
        return "latest";
    }

    public static bool ShouldShowAutoUpdateToast(
        bool needsUpdate, bool isLocalDev, string? currentVersion, string? latestVersion,
        bool showStartupToast = true, bool autoUpdate = true) =>
        showStartupToast && autoUpdate && needsUpdate && !isLocalDev &&
        currentVersion is not null && latestVersion is not null;

    public static string[] ListClaudeCodeHookNames() =>
        ["experimental.session.compacting", "chat.message", "tool.execute.before",
         "tool.execute.after", "event", "dispose"];

    #endregion

    #region Idle Notification Scheduler

    public static IdleNotificationState CreateIdleNotificationState() => new();

    #endregion

    #region Agent Usage Reminder

    private static readonly HashSet<string> AgentUsageTargetTools =
        ["grep", "safe_grep", "glob", "safe_glob", "webfetch", "context7_resolve-library-id",
         "context7_query-docs", "websearch_web_search_exa", "context7_get-library-docs", "grep_app_searchgithub"];

    private static readonly HashSet<string> AgentUsageAgentTools = ["task", "call_lfe_agent"];

    public static bool IsOrchestratorAgentForReminder(string? agentName)
    {
        if (agentName is null) return true;
        var key = agentName.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
        return key is "sisyphus" or "sisyphus-junior" or "atlas" or "hephaestus" or "prometheus";
    }

    public static bool ShouldRemindAgentUsage(string tool, AgentUsageState state,
        string? agentName = null, int maxReminders = 3, Func<long>? now = null)
    {
        now ??= () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!IsOrchestratorAgentForReminder(agentName)) return false;
        var toolLower = tool.ToLowerInvariant();

        if (AgentUsageAgentTools.Contains(toolLower))
        {
            state.AgentUsed = true;
            state.UpdatedAt = now();
            return false;
        }

        if (!AgentUsageTargetTools.Contains(toolLower) || state.AgentUsed || state.ReminderCount >= maxReminders)
            return false;

        state.ReminderCount++;
        state.UpdatedAt = now();
        return true;
    }

    #endregion

    #region Helpers

    private static string CollapseWhitespace(string text) =>
        string.Join(" ", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));

    private static string GetLastNonEmptyLine(string text) =>
        text.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "";

    #endregion
}

public sealed class IdleNotificationState
{
    public HashSet<string> NotifiedSessions { get; } = [];
    public HashSet<string> PendingSessions { get; } = [];
    public HashSet<string> SessionActivitySinceIdle { get; } = [];
    public Dictionary<string, int> NotificationVersions { get; } = [];
    public HashSet<string> ExecutingNotifications { get; } = [];
    public Dictionary<string, long> ScheduledAt { get; } = [];
}
