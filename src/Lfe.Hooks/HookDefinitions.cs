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

    private static readonly LfeHookDefinition[] Hooks = BuildHookRegistry();

    public static List<LfeHookDefinition> ListLfeHooks() => Hooks.Select(CloneHook).ToList();

    public static LfeHookDefinition? GetLfeHook(string name) =>
        Hooks.FirstOrDefault(h => h.Name == name) is { } hook ? CloneHook(hook) : null;

    public static List<LfeHookDefinition> ListLfeHooksByStatus(LfeHookStatus status) =>
        Hooks.Where(h => h.Status == status).Select(CloneHook).ToList();

    public static List<LfeHookDefinition> ListLfeHooksByWave(LfeHookWave wave) =>
        Hooks.Where(h => h.Wave == wave).Select(CloneHook).ToList();

    public static List<LfeHookDefinition> ListLfeHooksByExitPath(LfeHookExitPath exitPath) =>
        Hooks.Where(h => h.ExitPath == exitPath).Select(CloneHook).ToList();

    public static Dictionary<LfeHookStatus, int> SummarizeLfeHookPorting() =>
        Hooks.Aggregate(
            new Dictionary<LfeHookStatus, int>
            {
                [LfeHookStatus.BehaviorMapped] = 0,
                [LfeHookStatus.AdapterBound] = 0,
                [LfeHookStatus.Missing] = 0,
            },
            (summary, h) => { summary[h.Status]++; return summary; }
        );

    private static LfeHookDefinition CloneHook(LfeHookDefinition hook) =>
        hook with { TestTypes = [.. hook.TestTypes] };

    #endregion

    #region Hook Resolution Functions

    public static LfeHookExitPath ResolveLfeHookExitPath(LfeHookStatus status, string domain) =>
        (status, domain) switch
        {
            (LfeHookStatus.AdapterBound, _) => LfeHookExitPath.AdapterBound,
            (LfeHookStatus.BehaviorMapped, _) => LfeHookExitPath.PureDomainPort,
            (_, "workflow") => LfeHookExitPath.LimitedRedesign,
            (_, "plugin-loader") => LfeHookExitPath.LimitedRedesign,
            (_, "terminal") => LfeHookExitPath.LimitedRedesign,
            (_, "environment") => LfeHookExitPath.LimitedRedesign,
            (_, "notification") => LfeHookExitPath.LimitedRedesign,
            (_, "maintenance") => LfeHookExitPath.LimitedRedesign,
            _ => LfeHookExitPath.PureDomainPort,
        };

    public static string ResolveLfeHookTargetPackage(LfeHookStatus status, string domain) =>
        (status, domain) switch
        {
            (LfeHookStatus.AdapterBound, _) => "@oh-my-opencode/hooks-core",
            (_, "model") => "@oh-my-opencode/model-core",
            (_, "context") => "@oh-my-opencode/agents-md-core",
            (_, "loop") => "@oh-my-opencode/ulw-kernel",
            _ => "@oh-my-opencode/hooks-core",
        };

    public static LfeHookWave ResolveWave(string domain) =>
        domain switch
        {
            "guard" or "prompting" or "model" or "validation" or "quality" => LfeHookWave.Phase1Safety,
            "context-window" or "recovery" or "tool-output" or "runtime" => LfeHookWave.Phase2Recovery,
            "loop" or "task" or "team" or "workflow" or "todo" or "commands" => LfeHookWave.Phase3Orchestration,
            "notification" or "environment" or "terminal" or "maintenance" or "agent" => LfeHookWave.Phase4Host,
            _ => LfeHookWave.Phase5AdapterConvergence,
        };

    public static List<LfeHookTestType> ResolveLfeHookTestTypes(LfeHookStatus status) =>
        status switch
        {
            LfeHookStatus.AdapterBound => [LfeHookTestType.Adapter, LfeHookTestType.Integration, LfeHookTestType.ManualQa],
            LfeHookStatus.BehaviorMapped => [LfeHookTestType.Unit, LfeHookTestType.Parity, LfeHookTestType.ManualQa],
            _ => [LfeHookTestType.Unit, LfeHookTestType.Parity],
        };

    public static LfeHookAdapterImpact ResolveAdapterImpact(LfeHookStatus status, string domain) =>
        (status, domain) switch
        {
            (LfeHookStatus.AdapterBound, _) => LfeHookAdapterImpact.High,
            (_, "notification" or "terminal" or "environment" or "workflow" or "plugin-loader") => LfeHookAdapterImpact.High,
            (_, "team" or "task" or "loop" or "runtime") => LfeHookAdapterImpact.Medium,
            (LfeHookStatus.BehaviorMapped, _) => LfeHookAdapterImpact.None,
            _ => LfeHookAdapterImpact.Low,
        };

    #endregion

    #region Hook Registry Builder

    private static LfeHookDefinition[] BuildHookRegistry()
    {
        var hooks = new (string name, string export, string domain, LfeHookStatus status, string? pkg, string? src)[]
        {
            ("todo-continuation-enforcer", "createTodoContinuationEnforcer", "loop", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("context-window-monitor", "createContextWindowMonitorHook", "context-window", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("session-notification", "createSessionNotification", "notification", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("comment-checker", "createCommentCheckerHooks", "quality", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/comment-checker-core", null),
            ("tool-output-truncator", "createToolOutputTruncatorHook", "tool-output", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("directory-agents-injector", "createDirectoryAgentsInjectorHook", "context", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/agents-md-core", null),
            ("session-recovery", "createSessionRecoveryHook", "recovery", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("anthropic-context-window-limit-recovery", "createAnthropicContextWindowLimitRecoveryHook", "context-window", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("think-mode", "createThinkModeHook", "model", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("anthropic-effort", "createAnthropicEffortHook", "model", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("model-fallback", "createModelFallbackHook", "model", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/model-core", null),
            ("rules-injector", "createRulesInjectorHook", "context", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/rules-engine", null),
            ("background-notification", "createBackgroundNotificationHook", "notification", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("keyword-detector", "createKeywordDetectorHook", "prompting", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("thinking-block-validator", "createThinkingBlockValidatorHook", "validation", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("team-mailbox-injector", "createTeamMailboxInjector", "team", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("ralph-loop", "createRalphLoopHook", "loop", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/ulw-kernel", null),
            ("question-label-truncator", "createQuestionLabelTruncatorHook", "question", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("bash-file-read-guard", "createBashFileReadGuardHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("hashline-read-enhancer", "createHashlineReadEnhancerHook", "tool-output", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("write-existing-file-guard", "createWriteExistingFileGuardHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("empty-task-response-detector", "createEmptyTaskResponseDetectorHook", "recovery", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("json-error-recovery", "createJsonErrorRecoveryHook", "recovery", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("edit-error-recovery", "createEditErrorRecoveryHook", "recovery", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("auto-slash-command", "createAutoSlashCommandHook", "commands", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("runtime-fallback", "createRuntimeFallbackHook", "runtime", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("start-work", "createStartWorkHook", "workflow", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("atlas", "createAtlasHook", "workflow", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("plan-format-validator", "createPlanFormatValidatorHook", "workflow", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("interactive-bash-session", "createInteractiveBashSessionHook", "terminal", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("read-image-resizer", "createReadImageResizerHook", "tool-output", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("non-interactive-env", "createNonInteractiveEnvHook", "environment", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("webfetch-redirect-guard", "createWebFetchRedirectGuardHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("team-tool-gating", "createTeamToolGating", "team", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("team-mode-status-injector", "createTeamModeStatusInjector", "team", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("delegate-task-retry", "createDelegateTaskRetryHook", "task", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("task-resume-info", "createTaskResumeInfoHook", "task", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("stop-continuation-guard", "createStopContinuationGuardHook", "loop", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("compaction-context-injector", "createCompactionContextInjector", "context-window", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("compaction-todo-preserver", "createCompactionTodoPreserverHook", "context-window", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("unstable-agent-babysitter", "createUnstableAgentBabysitterHook", "agent", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("preemptive-compaction", "createPreemptiveCompactionHook", "context-window", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("tasks-todowrite-disabler", "createTasksTodowriteDisablerHook", "task", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("agent-usage-reminder", "createAgentUsageReminderHook", "prompting", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("category-skill-reminder", "createCategorySkillReminderHook", "skills", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("claude-code-hooks", "createClaudeCodeHooksHook", "plugin-loader", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("auto-update-checker", "createAutoUpdateCheckerHook", "maintenance", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("no-sisyphus-gpt", "createNoSisyphusGptHook", "model", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("no-hephaestus-non-gpt", "createNoHephaestusNonGptHook", "model", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("prometheus-md-only", "createPrometheusMdOnlyHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("sisyphus-junior-notepad", "createSisyphusJuniorNotepadHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("tool-pair-validator", "createToolPairValidatorHook", "validation", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("notepad-write-guard", "createNotepadWriteGuardHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("fsync-skip-warning", "createFsyncSkipWarningHook", "guard", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
            ("legacy-plugin-toast", "createLegacyPluginToastHook", "notification", LfeHookStatus.BehaviorMapped, "@oh-my-opencode/hooks-core", null),
        };

        return hooks.Select(h =>
        {
            var originalSource = h.src ?? $"src/hooks/{h.name}/hook.ts";
            return new LfeHookDefinition(
                Name: h.name,
                OriginalExport: h.export,
                Domain: h.domain,
                Status: h.status,
                StandalonePackage: h.pkg,
                OriginalSource: originalSource,
                ExitPath: ResolveLfeHookExitPath(h.status, h.domain),
                TargetPackage: h.pkg ?? ResolveLfeHookTargetPackage(h.status, h.domain),
                Wave: ResolveWave(h.domain),
                TestTypes: ResolveLfeHookTestTypes(h.status),
                AdapterImpact: ResolveAdapterImpact(h.status, h.domain)
            );
        }).ToArray();
    }

    #endregion
}
