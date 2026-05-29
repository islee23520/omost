namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class Workflow
{
    #region Start Work

    [GeneratedRegex(@"<user-request>\s*([\s\S]*?)\s*<\/user-request>", RegexOptions.IgnoreCase)]
    private static partial Regex UserRequestPattern();

    [GeneratedRegex(@"--worktree(?:\s+(\S+))?")]
    private static partial Regex WorktreeFlagPattern();

    [GeneratedRegex(@"\b(ultrawork|ulw)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StartWorkKeywordPattern();

    [GeneratedRegex(@"^(['""`])([\s\S]*)\1$")]
    private static partial Regex WrappingQuotesPattern();

    public static ParsedUserRequest ParseUserRequest(string promptText)
    {
        var match = UserRequestPattern().Match(promptText);
        if (!match.Success) return new ParsedUserRequest(null, null);

        var rawArg = match.Groups[1].Value.Trim();
        if (rawArg.Length == 0) return new ParsedUserRequest(null, null);

        var worktreeMatch = WorktreeFlagPattern().Match(rawArg);
        var explicitWorktreePath = worktreeMatch.Success ? worktreeMatch.Groups[1].Value : null;
        if (worktreeMatch.Success) rawArg = WorktreeFlagPattern().Replace(rawArg, "").Trim();

        var cleanedArg = StartWorkKeywordPattern().Replace(rawArg, "").Trim();
        var quotedMatch = WrappingQuotesPattern().Match(cleanedArg);
        var planName = quotedMatch.Success ? quotedMatch.Groups[2].Value.Trim() : cleanedArg;
        return new ParsedUserRequest(string.IsNullOrEmpty(planName) ? null : planName, explicitWorktreePath);
    }

    public static List<WorktreeEntry> ParseWorktreeListPorcelain(string output)
    {
        var entries = new List<WorktreeEntry>();
        string? currentPath = null;
        string? currentBranch = null;
        var currentBare = false;

        foreach (var line in output.Split('\n').Select(l => l.Trim()))
        {
            if (line.Length == 0)
            {
                if (currentPath is not null)
                    entries.Add(new WorktreeEntry(currentPath, currentBranch, currentBare));
                currentPath = null; currentBranch = null; currentBare = false;
            }
            else if (line.StartsWith("worktree "))
            {
                currentPath = line["worktree ".Length..].Trim();
            }
            else if (currentPath is not null && line.StartsWith("branch "))
            {
                currentBranch = line["branch ".Length..].Trim().Replace("refs/heads/", "");
            }
            else if (currentPath is not null && line == "bare")
            {
                currentBare = true;
            }
        }
        if (currentPath is not null)
            entries.Add(new WorktreeEntry(currentPath, currentBranch, currentBare));
        return entries;
    }

    public static string? ResolveStartWorkTemplate(string promptText, string sessionID, string timestamp, string contextInfo)
    {
        if (!promptText.Contains("<session-context>") || !promptText.Contains("You are starting a Sisyphus work session."))
            return null;
        return $"{promptText.Replace("$SESSION_ID", sessionID).Replace("$TIMESTAMP", timestamp)}\n\n---\n{contextInfo}";
    }

    #endregion

    #region Atlas

    [GeneratedRegex(@"^##\s*1\.\s*TASK\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TaskSectionHeaderPattern();

    [GeneratedRegex(@"^(?:[-*]\s*\[\s*\]\s*)?(\d+)\.\s+(.+)$")]
    private static partial Regex TodoTaskLinePattern();

    [GeneratedRegex(@"^(?:[-*]\s*\[\s*\]\s*)?(F\d+)\.\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex FinalWaveTaskLinePattern();

    private const string AtlasSingleTaskDirective = "Complete exactly one assigned task. Do not broaden scope.";

    public static TrackedTaskRef? ParseTrackedTaskFromPrompt(string prompt)
    {
        var lines = prompt.Split("\n");
        var taskHeaderIndex = Array.FindIndex(lines, l => TaskSectionHeaderPattern().IsMatch(l.Trim()));
        if (taskHeaderIndex < 0) return null;

        for (var i = taskHeaderIndex + 1; i < Math.Min(lines.Length, taskHeaderIndex + 6); i++)
        {
            var candidate = lines[i].Trim();
            if (candidate.Length == 0) continue;

            var finalMatch = FinalWaveTaskLinePattern().Match(candidate);
            if (finalMatch.Success && finalMatch.Groups[1].Value.Length > 0 && finalMatch.Groups[2].Value.Length > 0)
                return new TrackedTaskRef($"final-wave:{finalMatch.Groups[1].Value.ToLowerInvariant()}",
                    finalMatch.Groups[1].Value.ToUpperInvariant(), finalMatch.Groups[2].Value.Trim());

            var todoMatch = TodoTaskLinePattern().Match(candidate);
            if (todoMatch.Success && todoMatch.Groups[1].Value.Length > 0 && todoMatch.Groups[2].Value.Length > 0)
                return new TrackedTaskRef($"todo:{todoMatch.Groups[1].Value}", todoMatch.Groups[1].Value,
                    todoMatch.Groups[2].Value.Trim());
        }
        return null;
    }

    public static string BuildAtlasSingleTaskPrompt(string prompt) =>
        prompt.Contains("<system-") ? prompt :
        $"<system-reminder>{AtlasSingleTaskDirective}</system-reminder>\n{prompt}";

    public static bool ShouldWarnAtlasDirectModification(string tool, string? filePath, bool isOmoPath = false) =>
        new[] { "write", "edit", "multiedit" }.Contains(tool.ToLowerInvariant()) &&
        filePath is not null && !isOmoPath;

    public static (string Kind, TrackedTaskRef? Task, string? Reason)? ResolveAtlasPendingTaskRef(
        string? callID, string? prompt, string? requestedSessionId = null, string[]? existingKeys = null)
    {
        if (callID is null) return null;
        if (requestedSessionId is not null) return ("skip", null, "explicit_resume");

        var task = prompt is not null ? ParseTrackedTaskFromPrompt(prompt) : null;
        if (task is null) return null;

        if (existingKeys?.Contains(task.Key) == true)
            return ("skip", task, "ambiguous_task_key");
        return ("track", task, null);
    }

    #endregion

    #region Plan Format Validator

    private static readonly HashSet<string> PlanFormatWriteTools = ["Write", "Edit", "write", "edit"];

    [GeneratedRegex(@"^[-*]\s*\[[ xX]\]", RegexOptions.Multiline)]
    private static partial Regex PlanFormatCheckboxPattern();

    [GeneratedRegex(@"^##\s+")]
    private static partial Regex PlanFormatHeadingSecondLevel();

    [GeneratedRegex(@"^##\s+TODOs\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlanFormatHeadingTodos();

    [GeneratedRegex(@"^##\s+Final Verification Wave\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlanFormatHeadingFinalWave();

    [GeneratedRegex(@"^[-*]\s*\[[ xX]?\]")]
    private static partial Regex PlanFormatToplevelCheckbox();

    public static int CountRawTopLevelPlanCheckboxes(string content)
    {
        var section = "other";
        var count = 0;
        foreach (var line in content.Split("\n"))
        {
            if (PlanFormatHeadingSecondLevel().IsMatch(line))
            {
                section = PlanFormatHeadingTodos().IsMatch(line) ? "todo" :
                    PlanFormatHeadingFinalWave().IsMatch(line) ? "final-wave" : "other";
                continue;
            }
            if (section != "other" && PlanFormatToplevelCheckbox().IsMatch(line)) count++;
        }
        return count;
    }

    public static string BuildPlanFormatWarning(int rawCount, int parsedCount)
    {
        var skipped = rawCount - parsedCount;
        if (parsedCount == 0)
        {
            return $"""

                <plan-format-warning>
                Plan has **{rawCount} task checkbox(es)** but `getPlanProgress()` parsed **0**.
                This means `/start-work` will show **"Progress: 0/0"** for this plan.

                **Fix**: Every task checkbox under `## TODOs` MUST start with a bare number
                followed by dot + space: `1.`, `2.`, `3.` — NOT `T1.`, `Phase 1:`, `Task-1.` etc.
                Every Final Verification Wave checkbox MUST start with `F` + number:
                `F1.`, `F2.` — NOT `T-F1.`, `F-1.`, `Final-1.` etc.
                </plan-format-warning>
                """;
        }
        return $"""

            <plan-format-warning>
            Plan has **{rawCount} task checkbox(es)** but `getPlanProgress()` only parsed **{parsedCount}**.
            **{skipped} task(s)** have malformed labels and will be SKIPPED by the progress counter.
            `/start-work` will show "Progress: {parsedCount} tasks" — missing {skipped} task(s).

            **Fix**: Ensure every skipped task checkbox uses bare-number format:
              `## TODOs` → `1.`, `2.`, `3.` (NOT `T1.`, `Phase 1:`, `Task-1.`)
              `## Final Verification Wave` → `F1.`, `F2.`, `F3.` (NOT `T-F1.`, `F-1.`, `Final-1.`)
            </plan-format-warning>
            """;
    }

    public static bool IsPlanFilePath(string filePath)
    {
        var normalized = filePath.ToLowerInvariant().Replace('\\', '/');
        return normalized.Contains(".omo/plans/") && normalized.EndsWith(".md");
    }

    public static PlanFormatValidationResult ValidatePlanFormat(string content, int parsedCount)
    {
        if (!PlanFormatCheckboxPattern().IsMatch(content))
            return new PlanFormatValidationResult(0, parsedCount);
        var rawCount = CountRawTopLevelPlanCheckboxes(content);
        if (rawCount == 0 || rawCount == parsedCount)
            return new PlanFormatValidationResult(rawCount, parsedCount);
        return new PlanFormatValidationResult(rawCount, parsedCount, BuildPlanFormatWarning(rawCount, parsedCount));
    }

    #endregion

    #region Fsync Skip Warning

    public static string DescribePathClassification(PathClassification classification) =>
        classification switch
        {
            PathClassification.Icloud => "iCloud Drive",
            PathClassification.Onedrive => "OneDrive",
            PathClassification.DesktopSync => "Desktop sync (macOS)",
            PathClassification.NetworkDrive => "Network drive",
            _ => "filesystem that does not support fsync",
        };

    public static string FormatFsyncSkipWarning(FsyncSkipEntry[] entries)
    {
        if (entries.Length == 0) return "";
        var classification = SelectMostCommonPathClassification(entries);
        var shown = entries.Take(5).ToList();
        var hidden = entries.Length - shown.Count;
        var pathLines = shown.Select(e => $"  - {e.FilePath} (code: {e.ErrorCode})").ToList();
        if (hidden > 0) pathLines.Add($"  ... and {hidden} more");

        var envLines = classification == PathClassification.Unknown
            ? Array.Empty<string>()
            : [$"Detected environment: {DescribePathClassification(classification)}"];

        var durabilityLine = classification == PathClassification.Unknown
            ? "  - Crash durability is best-effort because this filesystem does not support fsync."
            : "  - Crash durability is best-effort on this filesystem (this is normal for iCloud, OneDrive, network drives, antivirus-locked paths).";

        return string.Join("\n",
        [
            "---",
            $"[fsync-skipped] {entries.Length} write(s) bypassed fsync because the underlying filesystem rejected the syscall.",
            "",
            .. envLines,
            "Affected paths:",
            .. pathLines,
            "",
            "What this means:",
            "  - The write+rename succeeded — the file is on disk, atomicity is preserved.",
            durabilityLine,
            "  - No action required. Operation completed successfully.",
        ]);
    }

    private static PathClassification SelectMostCommonPathClassification(FsyncSkipEntry[] entries) =>
        entries.GroupBy(e => e.PathClassification)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? PathClassification.Unknown;

    #endregion

    #region Legacy Plugin Toast

    public static LegacyPluginToastDecision? ResolveLegacyPluginToastDecision(LegacyPluginToastInput input)
    {
        if (!input.HasLegacyEntry) return null;
        if (input.Migration?.Migrated == true)
        {
            return new LegacyPluginToastDecision(
                "Plugin Entry Migrated",
                $"\"{input.Migration.From}\" has been renamed to \"{input.Migration.To}\" in your opencode.json.\nNo action needed.",
                "success", 8000);
        }
        return new LegacyPluginToastDecision(
            "Legacy Plugin Name Detected",
            "Update your opencode.json: \"oh-my-opencode\" has been renamed to \"oh-my-openagent\".\nRun: bunx oh-my-opencode install",
            "warning", 10000);
    }

    #endregion
}
