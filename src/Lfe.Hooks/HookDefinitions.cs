namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class HookDefinitions
{
    private static readonly string HashlineNibbleStr = "ZPMQVRWSNKTXJBYH";
    private static readonly string[] HashlineDict = Enumerable.Range(0, 256)
        .Select(i => $"{HashlineNibbleStr[i >> 4]}{HashlineNibbleStr[i & 0x0f]}")
        .ToArray();

    public const int QuestionLabelMaxLength = 30;
    public const string AutoSlashCommandTagOpen = "<auto-slash-command>";
    public const string AutoSlashCommandTagClose = "</auto-slash-command>";
    public const int ThinkingSummaryMaxChars = 500;
    public const int MaxWebfetchRedirects = 10;
    public const double ContextWarningThreshold = 0.70;
    public const double PreemptiveCompactionThreshold = 0.78;
    public const long PreemptiveCompactionCooldownMs = 60_000;

    public static readonly string BashFileReadWarningMessage =
        "Prefer the Read tool over `cat`/`head`/`tail` for reading file contents. The Read tool provides line numbers and hash anchors for precise editing.";

    public static readonly string EmptyTaskResponseWarning = """
        [Task Empty Response Warning]

        Task invocation completed but returned no response. This indicates the agent either:
        - Failed to execute properly
        - Did not terminate correctly
        - Returned an empty result

        Note: The call has already completed - you are NOT waiting for a response. Proceed accordingly.
        """;

    public static readonly string[] JsonErrorToolExcludeList =
        ["bash", "read", "glob", "grep", "webfetch", "look_at", "grep_app_searchgithub", "websearch_web_search_exa", "todowrite", "todoread"];

    public const string JsonErrorReminderMarker = "[JSON PARSE ERROR - IMMEDIATE ACTION REQUIRED]";

    public static readonly string JsonErrorReminder = """

        [JSON PARSE ERROR - IMMEDIATE ACTION REQUIRED]

        You sent invalid JSON arguments. The system could not parse your tool call.
        STOP and do this NOW:

        1. LOOK at the error message above to see what was expected vs what you sent.
        2. CORRECT your JSON syntax (missing braces, unescaped quotes, trailing commas, etc).
        3. RETRY the tool call with valid JSON.

        DO NOT repeat the exact same invalid call.
        """;

    public static readonly string[] EditErrorPatterns =
        ["oldString and newString must be different", "oldString not found", "oldString found multiple times"];

    public static readonly string EditErrorReminder = """

        [EDIT ERROR - IMMEDIATE ACTION REQUIRED]

        You made an Edit mistake. STOP and do this NOW:

        1. READ the file immediately to see its ACTUAL current state
        2. VERIFY what the content really looks like (your assumption was wrong)
        3. APOLOGIZE briefly to the user for the error
        4. CONTINUE with corrected action based on the real file content

        DO NOT attempt another edit until you've read and verified the file state.
        """;

    public static readonly string[] TaskTodowriteBlockedTools = ["TodoWrite", "TodoRead"];

    public static readonly string TaskTodowriteReplacementMessage = """
        TodoRead/TodoWrite are DISABLED because experimental.task_system is enabled.

        **ACTION REQUIRED**: RE-REGISTER what you were about to write as Todo using Task tools NOW. Then ASSIGN yourself and START WORKING immediately.

        **Use these tools instead:**
        - TaskCreate: Create new task with auto-generated ID
        - TaskUpdate: Update status, assign owner, add dependencies
        - TaskList: List active tasks with dependency info
        - TaskGet: Get full task details
        """;

    public static readonly string AgentUsageReminderMessage = """

        [Agent Usage Reminder]

        You called a search/fetch tool directly without leveraging specialized agents.

        RECOMMENDED: Use task with explore/librarian agents for better results.
        ALWAYS prefer: Multiple parallel task calls > Direct tool calls
        """;

    public const string ToolResultPlaceholder = "Tool output unavailable (context compacted)";

    public const string WriteSuccessMarker = "File written successfully.";

    #region Hook Registry

    private static readonly OmoHookDefinition[] Hooks = BuildHookRegistry();

    public static List<OmoHookDefinition> ListOmoHooks() => Hooks.Select(CloneHook).ToList();

    public static OmoHookDefinition? GetOmoHook(string name) =>
        Hooks.FirstOrDefault(h => h.Name == name) is { } hook ? CloneHook(hook) : null;

    public static List<OmoHookDefinition> ListOmoHooksByStatus(OmoHookStatus status) =>
        Hooks.Where(h => h.Status == status).Select(CloneHook).ToList();

    public static List<OmoHookDefinition> ListOmoHooksByWave(OmoHookWave wave) =>
        Hooks.Where(h => h.Wave == wave).Select(CloneHook).ToList();

    public static List<OmoHookDefinition> ListOmoHooksByExitPath(OmoHookExitPath exitPath) =>
        Hooks.Where(h => h.ExitPath == exitPath).Select(CloneHook).ToList();

    public static Dictionary<OmoHookStatus, int> SummarizeOmoHookPorting() =>
        Hooks.Aggregate(
            new Dictionary<OmoHookStatus, int>
            {
                [OmoHookStatus.BehaviorMapped] = 0,
                [OmoHookStatus.AdapterBound] = 0,
                [OmoHookStatus.Missing] = 0,
            },
            (summary, h) => { summary[h.Status]++; return summary; }
        );

    private static OmoHookDefinition CloneHook(OmoHookDefinition hook) =>
        hook with { TestTypes = [.. hook.TestTypes] };

    #endregion

    #region Hook Resolution Functions

    public static OmoHookExitPath ResolveOmoHookExitPath(OmoHookStatus status, string domain) =>
        (status, domain) switch
        {
            (OmoHookStatus.AdapterBound, _) => OmoHookExitPath.AdapterBound,
            (OmoHookStatus.BehaviorMapped, _) => OmoHookExitPath.PureDomainPort,
            (_, "workflow") => OmoHookExitPath.LimitedRedesign,
            (_, "plugin-loader") => OmoHookExitPath.LimitedRedesign,
            (_, "terminal") => OmoHookExitPath.LimitedRedesign,
            (_, "environment") => OmoHookExitPath.LimitedRedesign,
            (_, "notification") => OmoHookExitPath.LimitedRedesign,
            (_, "maintenance") => OmoHookExitPath.LimitedRedesign,
            _ => OmoHookExitPath.PureDomainPort,
        };

    public static string ResolveOmoHookTargetPackage(OmoHookStatus status, string domain) =>
        (status, domain) switch
        {
            (OmoHookStatus.AdapterBound, _) => "@oh-my-opencode/hooks-core",
            (_, "model") => "@oh-my-opencode/model-core",
            (_, "context") => "@oh-my-opencode/agents-md-core",
            (_, "loop") => "@oh-my-opencode/ulw-kernel",
            _ => "@oh-my-opencode/hooks-core",
        };

    public static OmoHookWave ResolveWave(string domain) =>
        domain switch
        {
            "guard" or "prompting" or "model" or "validation" or "quality" => OmoHookWave.Phase1Safety,
            "context-window" or "recovery" or "tool-output" or "runtime" => OmoHookWave.Phase2Recovery,
            "loop" or "task" or "team" or "workflow" or "todo" or "commands" => OmoHookWave.Phase3Orchestration,
            "notification" or "environment" or "terminal" or "maintenance" or "agent" => OmoHookWave.Phase4Host,
            _ => OmoHookWave.Phase5AdapterConvergence,
        };

    public static List<OmoHookTestType> ResolveOmoHookTestTypes(OmoHookStatus status) =>
        status switch
        {
            OmoHookStatus.AdapterBound => [OmoHookTestType.Adapter, OmoHookTestType.Integration, OmoHookTestType.ManualQa],
            OmoHookStatus.BehaviorMapped => [OmoHookTestType.Unit, OmoHookTestType.Parity, OmoHookTestType.ManualQa],
            _ => [OmoHookTestType.Unit, OmoHookTestType.Parity],
        };

    public static OmoHookAdapterImpact ResolveAdapterImpact(OmoHookStatus status, string domain) =>
        (status, domain) switch
        {
            (OmoHookStatus.AdapterBound, _) => OmoHookAdapterImpact.High,
            (_, "notification" or "terminal" or "environment" or "workflow" or "plugin-loader") => OmoHookAdapterImpact.High,
            (_, "team" or "task" or "loop" or "runtime") => OmoHookAdapterImpact.Medium,
            (OmoHookStatus.BehaviorMapped, _) => OmoHookAdapterImpact.None,
            _ => OmoHookAdapterImpact.Low,
        };

    #endregion

    #region Hook Registry Builder

    private static OmoHookDefinition[] BuildHookRegistry()
    {
        var hooks = new (string name, string export, string domain, OmoHookStatus status, string? pkg, string? src)[]
        {
            ("todo-continuation-enforcer", "createTodoContinuationEnforcer", "loop", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("context-window-monitor", "createContextWindowMonitorHook", "context-window", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("session-notification", "createSessionNotification", "notification", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("comment-checker", "createCommentCheckerHooks", "quality", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/comment-checker-core", null),
            ("tool-output-truncator", "createToolOutputTruncatorHook", "tool-output", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("directory-agents-injector", "createDirectoryAgentsInjectorHook", "context", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/agents-md-core", null),
            ("session-recovery", "createSessionRecoveryHook", "recovery", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("anthropic-context-window-limit-recovery", "createAnthropicContextWindowLimitRecoveryHook", "context-window", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("think-mode", "createThinkModeHook", "model", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("anthropic-effort", "createAnthropicEffortHook", "model", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("model-fallback", "createModelFallbackHook", "model", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/model-core", null),
            ("rules-injector", "createRulesInjectorHook", "context", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/rules-engine", null),
            ("background-notification", "createBackgroundNotificationHook", "notification", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("keyword-detector", "createKeywordDetectorHook", "prompting", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("thinking-block-validator", "createThinkingBlockValidatorHook", "validation", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("team-mailbox-injector", "createTeamMailboxInjector", "team", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("ralph-loop", "createRalphLoopHook", "loop", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/ulw-kernel", null),
            ("question-label-truncator", "createQuestionLabelTruncatorHook", "question", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("bash-file-read-guard", "createBashFileReadGuardHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("hashline-read-enhancer", "createHashlineReadEnhancerHook", "tool-output", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("write-existing-file-guard", "createWriteExistingFileGuardHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("empty-task-response-detector", "createEmptyTaskResponseDetectorHook", "recovery", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("json-error-recovery", "createJsonErrorRecoveryHook", "recovery", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("edit-error-recovery", "createEditErrorRecoveryHook", "recovery", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("auto-slash-command", "createAutoSlashCommandHook", "commands", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("runtime-fallback", "createRuntimeFallbackHook", "runtime", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("start-work", "createStartWorkHook", "workflow", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("atlas", "createAtlasHook", "workflow", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("plan-format-validator", "createPlanFormatValidatorHook", "workflow", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("interactive-bash-session", "createInteractiveBashSessionHook", "terminal", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("read-image-resizer", "createReadImageResizerHook", "tool-output", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("non-interactive-env", "createNonInteractiveEnvHook", "environment", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("webfetch-redirect-guard", "createWebFetchRedirectGuardHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("team-tool-gating", "createTeamToolGating", "team", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("team-mode-status-injector", "createTeamModeStatusInjector", "team", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("delegate-task-retry", "createDelegateTaskRetryHook", "task", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("task-resume-info", "createTaskResumeInfoHook", "task", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("stop-continuation-guard", "createStopContinuationGuardHook", "loop", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("compaction-context-injector", "createCompactionContextInjector", "context-window", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("compaction-todo-preserver", "createCompactionTodoPreserverHook", "context-window", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("unstable-agent-babysitter", "createUnstableAgentBabysitterHook", "agent", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("preemptive-compaction", "createPreemptiveCompactionHook", "context-window", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("tasks-todowrite-disabler", "createTasksTodowriteDisablerHook", "task", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("agent-usage-reminder", "createAgentUsageReminderHook", "prompting", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("category-skill-reminder", "createCategorySkillReminderHook", "skills", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("claude-code-hooks", "createClaudeCodeHooksHook", "plugin-loader", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("auto-update-checker", "createAutoUpdateCheckerHook", "maintenance", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("no-sisyphus-gpt", "createNoSisyphusGptHook", "model", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("no-hephaestus-non-gpt", "createNoHephaestusNonGptHook", "model", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("prometheus-md-only", "createPrometheusMdOnlyHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("sisyphus-junior-notepad", "createSisyphusJuniorNotepadHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("tool-pair-validator", "createToolPairValidatorHook", "validation", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("notepad-write-guard", "createNotepadWriteGuardHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("fsync-skip-warning", "createFsyncSkipWarningHook", "guard", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("legacy-plugin-toast", "createLegacyPluginToastHook", "notification", OmoHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
        };

        return hooks.Select(h =>
        {
            var originalSource = h.src ?? $"src/hooks/{h.name}/hook.ts";
            return new OmoHookDefinition(
                Name: h.name,
                OriginalExport: h.export,
                Domain: h.domain,
                Status: h.status,
                StandalonePackage: h.pkg,
                OriginalSource: originalSource,
                ExitPath: ResolveOmoHookExitPath(h.status, h.domain),
                TargetPackage: h.pkg ?? ResolveOmoHookTargetPackage(h.status, h.domain),
                Wave: ResolveWave(h.domain),
                TestTypes: ResolveOmoHookTestTypes(h.status),
                AdapterImpact: ResolveAdapterImpact(h.status, h.domain)
            );
        }).ToArray();
    }

    #endregion
}
