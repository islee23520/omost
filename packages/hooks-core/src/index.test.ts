import { describe, expect, test } from "bun:test"
import { AGENT_USAGE_REMINDER_MESSAGE, BASH_FILE_READ_WARNING_MESSAGE, EDIT_ERROR_REMINDER, EMPTY_TASK_RESPONSE_WARNING, JSON_ERROR_REMINDER_MARKER, PLANNING_CONSULT_WARNING, PROMETHEUS_WORKFLOW_REMINDER, TASK_TODOWRITE_REPLACEMENT_MESSAGE, TODOWRITE_DESCRIPTION, TOOL_RESULT_PLACEHOLDER, appendContextWindowStatus, addDelegateTaskRetryGuidance, addSisyphusJuniorNotepadDirective, appendTaskResumeInfo, applyTodoDescriptionOverride, buildCategorySkillReminderMessage, buildContextWindowReminder, buildInteractiveBashSessionReminder, buildNonInteractiveEnvPrefix, buildNonInteractiveGitCommand, buildWebFetchRedirectLimitMessage, buildWindowsToastScript, calculateImageTokens, calculateTargetDimensions, createAgentUsageReminderHook, createBackgroundNotificationHook, createBashFileReadGuardHook, createCategorySkillReminderHook, createContextWindowMonitorHook, createDelegateTaskRetryHook, createEditErrorRecoveryHook, createEmptyTaskResponseDetectorHook, createInteractiveBashSessionHook, createJsonErrorRecoveryHook, createLegacyPluginToastDecisionHook, createModelAgentGuardHook, createNonInteractiveEnvHook, createNotepadWriteGuardHook, createPrometheusMdOnlyHook, createQuestionLabelTruncatorHook, createSisyphusJuniorNotepadHook, createStopContinuationGuardHook, createTaskResumeInfoHook, createTasksTodowriteDisablerHook, createTeamToolGating, createThinkModeHook, createThinkingBlockValidatorHook, createToolOutputTruncatorHook, createToolPairValidatorHook, createWebFetchRedirectGuardHook, createWriteExistingFileGuardHook, describePathClassification, detectBannedInteractiveCommand, detectThinkKeyword, escapeAppleScriptText, escapePowerShellSingleQuotedText, formatFsyncSkipWarning, formatImageResizeAppendix, getDefaultNotificationSoundPath, getOmoHook, getStandaloneHookBehavior, hasIncompleteTodos, hasSignedThinkingBlocksInHistory, isAlreadyHighReasoningVariant, isGptModel, isGptNativeSisyphusModel, isNotepadPath, isOmoTmuxSession, isOmoWorkspacePath, isOrchestratorAgentForReminder, isOverwriteEnabled, isPrometheusAgent, isPrometheusAllowedFile, isSimpleFileReadCommand, isUniversalTeamTool, listOmoHooks, listOmoHooksByExitPath, listOmoHooksByStatus, listOmoHooksByWave, normalizeNotificationPlatform, normalizeWebFetchRedirectOutput, parseTmuxCommand, recoverEditErrorOutput, recoverEmptyTaskOutput, recoverJsonErrorOutput, repairMissingToolResults, repairThinkingBlockMessages, resolveLegacyPluginToastDecision, resolveModelAgentGuard, resolveTeamToolGate, resolveThinkMode, resolveWriteExistingFileGuard, shouldBlockTaskTodoTool, shouldForwardBackgroundEvent, shouldRemindAgentUsage, shouldWarnContextWindow, standaloneHookBehaviors, summarizeOmoHookPorting, truncateQuestionLabel, truncateQuestionLabels, truncateToolOutput, type AgentUsageState, type MessageWithParts, type TeamParticipant } from "./index"
import { AUTO_SLASH_COMMAND_TAG_CLOSE, AUTO_SLASH_COMMAND_TAG_OPEN, THINKING_SUMMARY_MAX_CHARS, buildAtlasSingleTaskPrompt, buildCompactionContextPrompt, buildHashlineWriteSuccessOutput, buildPreemptiveCompactionFailureToast, buildReadyNotificationContent, buildTeamMailboxTurnMarker, buildTeamModeStatusContent, buildUnstableAgentReminder, classifyRuntimeFallbackErrorType, computeLineHash, containsRuntimeFallbackErrorContent, createAtlasHook, createAutoSlashCommandHook, createAutoUpdateCheckerHook, createClaudeCodeHooksHook, createCompactionContextInjector, createHashlineReadEnhancerHook, createIdleNotificationScheduler, createIdleNotificationState, createKeywordDetectorHook, createRuntimeFallbackHook, createSessionNotification, createSessionRecoveryHook, createStartWorkHook, createTailMonitorState, createTeamMailboxInjector, createTeamModeStatusInjector, createTodoContinuationEnforcer, detectErrorType, detectKeywordsWithType, detectSlashCommand, extractChannel, extractMessageIndex, extractMessages, extractRuntimeFallbackStatusCode, extractSessionNotificationText, extractTodos, extractUnavailableToolName, finalizeTrackedAssistantMessage, findLastSessionNotificationMessage, findSlashCommandPartIndex, formatBytes, formatHashLine, formatSlashCommandTemplate, getMessageInfo, getMessageParts, getRuntimeFallbackErrorMessage, getTodoProgressSnapshot, hasDetailedTodos, injectTeamMailboxMessage, injectTeamModeStatus, isAtlasBootstrapTodoList, isDistTag, isPrereleaseOrDistTag, isPrereleaseVersion, isRuntimeFallbackRetryableError, isTokenLimitErrorText, isUnstableTask, listClaudeCodeHookNames, looksLikeSlashCommand, parseAnthropicTokenLimitError, parseSlashCommand, parseTrackedTaskFromPrompt, parseUserRequest, parseWorktreeListPorcelain, removeKeywordCodeBlocks, replaceAtlasBootstrapTodos, resolveAtlasPendingTaskRef, resolveOmoHookExitPath, resolveOmoHookTargetPackage, resolveOmoHookTestTypes, resolveStartWorkTemplate, shouldRestoreOverCurrentTodos, shouldRunPreemptiveCompaction, shouldShowAutoUpdateToast, shouldTreatAssistantPartAsOutput, shouldWarnAtlasDirectModification, trackAssistantOutput, trackContinuationProgress, transformHashlineReadOutput } from "./index"
import * as hooksCore from "./index"

describe("OMO hook catalog", () => {
  test("lists copied hook registry entries without exposing mutable state", () => {
    const hooks = listOmoHooks()

    expect(hooks.length).toBeGreaterThan(50)
    expect(hooks.some((hook) => hook.name === "ralph-loop" && hook.standalonePackage === "@oh-my-opencode/ulw-kernel")).toBe(true)
    hooks[0].name = "mutated"
    hooks[0].testTypes.push("integration")
    expect(listOmoHooks()[0].name).not.toBe("mutated")
    expect(listOmoHooks()[0].testTypes).not.toContain("integration")
  })

  test("looks up hooks by name", () => {
    expect(getOmoHook("model-fallback")).toEqual(expect.objectContaining({
      name: "model-fallback",
      originalExport: "createModelFallbackHook",
      domain: "model",
      status: "behavior-mapped",
      standalonePackage: "@oh-my-opencode/model-core",
      originalSource: "src/hooks/model-fallback/hook.ts",
      exitPath: "pure-domain-port",
      targetPackage: "@oh-my-opencode/model-core",
      wave: "phase-1-safety",
      testTypes: ["unit", "parity", "manual-qa"],
      adapterImpact: "none",
    }))
    expect(getOmoHook("missing-hook")).toBeUndefined()
  })

  test("keeps an execution-grade parity ledger for every hook", () => {
    for (const hook of listOmoHooks()) {
      expect(hook.originalSource.startsWith(`src/hooks/${hook.name}/`) || hook.name === "startup-toast").toBe(true)
      expect(hook.targetPackage).toStartWith("@oh-my-opencode/")
      expect(hook.testTypes.length).toBeGreaterThan(0)
      expect(hook.exitPath).not.toBe("explicit-exclusion")
      expect(hook.exitPath).not.toBe("unclassified")
    }

    expect(getOmoHook("session-notification-sender")?.originalSource).toBe("src/hooks/session-notification-sender/session-notification-sender.ts")
    expect(getOmoHook("start-work")?.exitPath).toBe("pure-domain-port")
    expect(listOmoHooksByWave("phase-2-recovery").map((hook) => hook.name)).toContain("context-window-monitor")
    expect(listOmoHooksByExitPath("pure-domain-port").map((hook) => hook.name)).toContain("question-label-truncator")
  })

  test("filters and summarizes porting state", () => {
    expect(listOmoHooksByStatus("behavior-mapped").map((hook) => hook.name)).toContain("comment-checker")
    expect(listOmoHooksByStatus("adapter-bound").map((hook) => hook.name)).toEqual([])
    expect(summarizeOmoHookPorting()).toEqual({ "behavior-mapped": 61, "adapter-bound": 0, missing: 0 })
  })

  test("exposes real standalone behavior modules for mapped hooks", () => {
    expect(standaloneHookBehaviors.commentChecker.parseApplyPatchRequests).toBeFunction()
    expect(standaloneHookBehaviors.directoryContext.processFilePathForAgentsInjection).toBeFunction()
    expect(standaloneHookBehaviors.rules.findRuleFiles).toBeFunction()
    expect(standaloneHookBehaviors.rules.shouldApplyRule).toBeFunction()
    expect(standaloneHookBehaviors.modelFallback.resolveModelWithFallback).toBeFunction()
    expect(standaloneHookBehaviors.modelAgentGuard.resolveModelAgentGuard).toBeFunction()
    expect(standaloneHookBehaviors.modelAgentGuard.createThinkModeHook).toBeFunction()
    expect(standaloneHookBehaviors.modelAgentGuard.createAnthropicEffortHook).toBeFunction()
    expect(standaloneHookBehaviors.thinkingBlockValidator.repairThinkingBlockMessages).toBeFunction()
    expect(standaloneHookBehaviors.toolGuards.createBashFileReadGuardHook).toBeFunction()
    expect(standaloneHookBehaviors.toolGuards.createWriteExistingFileGuardHook).toBeFunction()
    expect(standaloneHookBehaviors.outputRecovery.createJsonErrorRecoveryHook).toBeFunction()
    expect(standaloneHookBehaviors.outputRecovery.createEditErrorRecoveryHook).toBeFunction()
    expect(standaloneHookBehaviors.promptDetectors.detectKeywordsWithType).toBeFunction()
    expect(standaloneHookBehaviors.slashCommands.parseSlashCommand).toBeFunction()
    expect(standaloneHookBehaviors.continuation.trackContinuationProgress).toBeFunction()
    expect(standaloneHookBehaviors.todoAndTask.hasIncompleteTodos).toBeFunction()
    expect(standaloneHookBehaviors.taskRecovery.repairMissingToolResults).toBeFunction()
    expect(standaloneHookBehaviors.hostGuards.createNonInteractiveEnvHook).toBeFunction()
    expect(standaloneHookBehaviors.notifications.buildWindowsToastScript).toBeFunction()
    expect(standaloneHookBehaviors.notifications.buildReadyNotificationContent).toBeFunction()
    expect(standaloneHookBehaviors.notificationScheduler.createIdleNotificationScheduler).toBeFunction()
    expect(standaloneHookBehaviors.sessionRecovery.detectErrorType).toBeFunction()
    expect(standaloneHookBehaviors.team.resolveTeamToolGate).toBeFunction()
    expect(standaloneHookBehaviors.team.injectTeamMailboxMessage).toBeFunction()
    expect(standaloneHookBehaviors.team.injectTeamModeStatus).toBeFunction()
    expect(standaloneHookBehaviors.contextWindow.createContextWindowMonitorHook).toBeFunction()
    expect(standaloneHookBehaviors.contextWindow.parseAnthropicTokenLimitError).toBeFunction()
    expect(standaloneHookBehaviors.contextWindow.buildCompactionContextPrompt).toBeFunction()
    expect(standaloneHookBehaviors.contextWindow.replaceAtlasBootstrapTodos).toBeFunction()
    expect(standaloneHookBehaviors.contextWindow.shouldRunPreemptiveCompaction).toBeFunction()
    expect(standaloneHookBehaviors.unstableAgent.buildUnstableAgentReminder).toBeFunction()
    expect(standaloneHookBehaviors.runtimeFallback.isRuntimeFallbackRetryableError).toBeFunction()
    expect(standaloneHookBehaviors.claudeCodeHooks.listClaudeCodeHookNames).toBeFunction()
    expect(standaloneHookBehaviors.autoUpdate.extractChannel).toBeFunction()
    expect(standaloneHookBehaviors.workflow.parseUserRequest).toBeFunction()
    expect(standaloneHookBehaviors.terminal.parseTmuxCommand).toBeFunction()
    expect(standaloneHookBehaviors.imageResizer.calculateTargetDimensions).toBeFunction()
    expect(standaloneHookBehaviors.hashline.transformHashlineReadOutput).toBeFunction()
    expect(standaloneHookBehaviors.ralphLoop.runTrackedUlw).toBeFunction()
    expect(standaloneHookBehaviors.questionLabelTruncator.createQuestionLabelTruncatorHook).toBeFunction()

    expect(getStandaloneHookBehavior("comment-checker")).toBe(standaloneHookBehaviors.commentChecker)
    expect(getStandaloneHookBehavior("directory-agents-injector")).toBe(standaloneHookBehaviors.directoryContext)
    expect(getStandaloneHookBehavior("directory-readme-injector")).toBe(standaloneHookBehaviors.directoryContext)
    expect(getStandaloneHookBehavior("rules-injector")).toBe(standaloneHookBehaviors.rules)
    expect(getStandaloneHookBehavior("model-fallback")).toBe(standaloneHookBehaviors.modelFallback)
    expect(getStandaloneHookBehavior("no-sisyphus-gpt")).toBe(standaloneHookBehaviors.modelAgentGuard)
    expect(getStandaloneHookBehavior("no-hephaestus-non-gpt")).toBe(standaloneHookBehaviors.modelAgentGuard)
    expect(getStandaloneHookBehavior("think-mode")).toBe(standaloneHookBehaviors.modelAgentGuard)
    expect(getStandaloneHookBehavior("anthropic-effort")).toBe(standaloneHookBehaviors.modelAgentGuard)
    expect(getStandaloneHookBehavior("thinking-block-validator")).toBe(standaloneHookBehaviors.thinkingBlockValidator)
    expect(getStandaloneHookBehavior("bash-file-read-guard")).toBe(standaloneHookBehaviors.toolGuards)
    expect(getStandaloneHookBehavior("webfetch-redirect-guard")).toBe(standaloneHookBehaviors.toolGuards)
    expect(getStandaloneHookBehavior("write-existing-file-guard")).toBe(standaloneHookBehaviors.toolGuards)
    expect(getStandaloneHookBehavior("empty-task-response-detector")).toBe(standaloneHookBehaviors.outputRecovery)
    expect(getStandaloneHookBehavior("json-error-recovery")).toBe(standaloneHookBehaviors.outputRecovery)
    expect(getStandaloneHookBehavior("tool-output-truncator")).toBe(standaloneHookBehaviors.outputRecovery)
    expect(getStandaloneHookBehavior("edit-error-recovery")).toBe(standaloneHookBehaviors.outputRecovery)
    expect(getStandaloneHookBehavior("keyword-detector")).toBe(standaloneHookBehaviors.promptDetectors)
    expect(getStandaloneHookBehavior("auto-slash-command")).toBe(standaloneHookBehaviors.slashCommands)
    expect(getStandaloneHookBehavior("todo-continuation-enforcer")).toBe(standaloneHookBehaviors.continuation)
    expect(getStandaloneHookBehavior("session-todo-status")).toBe(standaloneHookBehaviors.todoAndTask)
    expect(getStandaloneHookBehavior("tasks-todowrite-disabler")).toBe(standaloneHookBehaviors.todoAndTask)
    expect(getStandaloneHookBehavior("todo-description-override")).toBe(standaloneHookBehaviors.todoAndTask)
    expect(getStandaloneHookBehavior("notepad-write-guard")).toBe(standaloneHookBehaviors.todoAndTask)
    expect(getStandaloneHookBehavior("tool-pair-validator")).toBe(standaloneHookBehaviors.taskRecovery)
    expect(getStandaloneHookBehavior("delegate-task-retry")).toBe(standaloneHookBehaviors.taskRecovery)
    expect(getStandaloneHookBehavior("task-resume-info")).toBe(standaloneHookBehaviors.taskRecovery)
    expect(getStandaloneHookBehavior("stop-continuation-guard")).toBe(standaloneHookBehaviors.taskRecovery)
    expect(getStandaloneHookBehavior("non-interactive-env")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("category-skill-reminder")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("fsync-skip-warning")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("legacy-plugin-toast")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("prometheus-md-only")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("sisyphus-junior-notepad")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("agent-usage-reminder")).toBe(standaloneHookBehaviors.hostGuards)
    expect(getStandaloneHookBehavior("session-notification-formatting")).toBe(standaloneHookBehaviors.notifications)
    expect(getStandaloneHookBehavior("session-notification-sender")).toBe(standaloneHookBehaviors.notifications)
    expect(getStandaloneHookBehavior("session-notification")).toBe(standaloneHookBehaviors.notifications)
    expect(getStandaloneHookBehavior("background-notification")).toBe(standaloneHookBehaviors.notifications)
    expect(getStandaloneHookBehavior("session-notification-scheduler")).toBe(standaloneHookBehaviors.notificationScheduler)
    expect(getStandaloneHookBehavior("session-recovery")).toBe(standaloneHookBehaviors.sessionRecovery)
    expect(getStandaloneHookBehavior("team-tool-gating")).toBe(standaloneHookBehaviors.team)
    expect(getStandaloneHookBehavior("team-mailbox-injector")).toBe(standaloneHookBehaviors.team)
    expect(getStandaloneHookBehavior("team-mode-status-injector")).toBe(standaloneHookBehaviors.team)
    expect(getStandaloneHookBehavior("context-window-monitor")).toBe(standaloneHookBehaviors.contextWindow)
    expect(getStandaloneHookBehavior("anthropic-context-window-limit-recovery")).toBe(standaloneHookBehaviors.contextWindow)
    expect(getStandaloneHookBehavior("compaction-context-injector")).toBe(standaloneHookBehaviors.contextWindow)
    expect(getStandaloneHookBehavior("compaction-todo-preserver")).toBe(standaloneHookBehaviors.contextWindow)
    expect(getStandaloneHookBehavior("unstable-agent-babysitter")).toBe(standaloneHookBehaviors.unstableAgent)
    expect(getStandaloneHookBehavior("runtime-fallback")).toBe(standaloneHookBehaviors.runtimeFallback)
    expect(getStandaloneHookBehavior("claude-code-hooks")).toBe(standaloneHookBehaviors.claudeCodeHooks)
    expect(getStandaloneHookBehavior("auto-update-checker")).toBe(standaloneHookBehaviors.autoUpdate)
    expect(getStandaloneHookBehavior("startup-toast")).toBe(standaloneHookBehaviors.autoUpdate)
    expect(getStandaloneHookBehavior("start-work")).toBe(standaloneHookBehaviors.workflow)
    expect(getStandaloneHookBehavior("atlas")).toBe(standaloneHookBehaviors.workflow)
    expect(getStandaloneHookBehavior("interactive-bash-session")).toBe(standaloneHookBehaviors.terminal)
    expect(getStandaloneHookBehavior("read-image-resizer")).toBe(standaloneHookBehaviors.imageResizer)
    expect(getStandaloneHookBehavior("hashline-read-enhancer")).toBe(standaloneHookBehaviors.hashline)
    expect(getStandaloneHookBehavior("ralph-loop")).toBe(standaloneHookBehaviors.ralphLoop)
    expect(getStandaloneHookBehavior("question-label-truncator")).toBe(standaloneHookBehaviors.questionLabelTruncator)
    expect(getStandaloneHookBehavior("runtime-fallback")).toBe(standaloneHookBehaviors.runtimeFallback)
  })

  test("truncates question option labels as standalone behavior", () => {
    expect(truncateQuestionLabel("Short label")).toBe("Short label")
    expect(truncateQuestionLabel("Exactly thirty chars here!!!!!")).toBe("Exactly thirty chars here!!!!!")
    expect(truncateQuestionLabel("This is a very long label that exceeds thirty characters")).toBe("This is a very long label t...")

    const args = truncateQuestionLabels({
      questions: [
        { question: "Q1", options: [{ label: "Very long label number one that needs truncation" }, { label: "Short" }] },
        { question: "Q2" },
      ],
    })

    expect(args.questions?.[0]?.options?.[0]?.label).toBe("Very long label number one ...")
    expect(args.questions?.[0]?.options?.[1]?.label).toBe("Short")
    expect(args.questions?.[1]?.options).toEqual([])

    const invalid = { questions: "not-array" } as unknown as Parameters<typeof truncateQuestionLabels>[0]
    expect(truncateQuestionLabels(invalid)).toBe(invalid)
  })

  test("provides question-label-truncator hook wrapper", async () => {
    const hook = createQuestionLabelTruncatorHook()
    const output = { args: { questions: [{ question: "Choose", options: [{ label: "Another extremely long label for testing purposes", description: "desc" }] }] } }

    await hook["tool.execute.before"]({ tool: "AskUserQuestion" }, output)
    expect((output.args.questions as Array<{ options: Array<{ label: string; description: string }> }>)[0].options[0]).toEqual({ label: "Another extremely long labe...", description: "desc" })

    const nonQuestionOutput = { args: { command: "echo hello" } }
    await hook["tool.execute.before"]({ tool: "bash" }, nonQuestionOutput)
    expect(nonQuestionOutput.args).toEqual({ command: "echo hello" })

    const malformedOutput = { args: { questions: "invalid" } }
    await hook["tool.execute.before"]({ tool: "ask_user_question" }, malformedOutput)
    expect(malformedOutput.args).toEqual({ questions: "invalid" })
  })

  test("ports model guard decisions for Sisyphus and Hephaestus", async () => {
    expect(isGptModel("openai/gpt-5.3-codex")).toBe(true)
    expect(isGptModel("anthropic/claude-opus-4-7")).toBe(false)
    expect(isGptNativeSisyphusModel("openai/gpt-5.5")).toBe(true)
    expect(isGptNativeSisyphusModel("openai/gpt-5.3-codex")).toBe(false)

    expect(resolveModelAgentGuard("sisyphus", { providerID: "openai", modelID: "gpt-5.5" })).toEqual({ variant: "medium" })
    expect(resolveModelAgentGuard("sisyphus", { providerID: "openai", modelID: "gpt-5.4" })).toEqual({ variant: undefined })
    expect(resolveModelAgentGuard("Sisyphus", { providerID: "openai", modelID: "gpt-5.3-codex" })).toEqual(expect.objectContaining({
      agent: "hephaestus",
      outputAgent: "hephaestus",
      sessionAgent: "hephaestus",
      toast: expect.objectContaining({ title: "NEVER Use Sisyphus with GPT", variant: "error" }),
    }))
    expect(resolveModelAgentGuard("hephaestus", { providerID: "anthropic", modelID: "claude-opus-4-7" })).toEqual(expect.objectContaining({
      agent: "sisyphus",
      outputAgent: "sisyphus",
      sessionAgent: "sisyphus",
      toast: expect.objectContaining({ title: "NEVER Use Hephaestus with Non-GPT", variant: "error" }),
    }))
    expect(resolveModelAgentGuard("hephaestus", { providerID: "anthropic", modelID: "claude-opus-4-7" }, { allowHephaestusNonGptModel: true })).toEqual(expect.objectContaining({
      toast: expect.objectContaining({ variant: "warning" }),
    }))
    expect(resolveModelAgentGuard("explore", { providerID: "anthropic", modelID: "claude-haiku-4-5" })).toEqual({})

    const hook = createModelAgentGuardHook()
    const input = { sessionID: "s1", agent: "sisyphus", model: { providerID: "openai", modelID: "gpt-5.3-codex" } }
    const output = { message: { agent: "sisyphus" } }
    await hook["chat.message"](input, output)
    expect(input.agent).toBe("hephaestus")
    expect(output.message.agent).toBe("hephaestus")

    const nativeOutput: { message: { variant?: string } } = { message: {} }
    await hook["chat.message"]({ sessionID: "s2", agent: "sisyphus", model: { providerID: "openai", modelID: "gpt-5.5" } }, nativeOutput)
    expect(nativeOutput.message.variant).toBe("medium")
  })

  test("ports anthropic effort chat params behavior", async () => {
    const opusInput = { sessionID: "s", agent: { name: "hephaestus" }, model: { providerID: "anthropic", modelID: "claude-opus-4.7" }, message: { variant: "max" } }
    const opusOutput = { options: {} as Record<string, unknown> }
    expect(hooksCore.isClaudeProvider("github-copilot", "claude-sonnet-4")).toBe(true)
    expect(hooksCore.isOpusModel("claude-opus-4.7")).toBe(true)
    expect(hooksCore.isEffortUnsupportedModel("claude-haiku-4")).toBe(true)
    expect(hooksCore.shouldSkipForInternalAgent(" summary ")).toBe(true)
    await hooksCore.createAnthropicEffortHook()["chat.params"](opusInput, opusOutput)
    expect(opusOutput.options.effort).toBe("max")
    expect(opusInput.message.variant).toBe("max")

    const constrainedInput = { sessionID: "s", agent: { name: "hephaestus" }, model: { providerID: "github-copilot", modelID: "claude-opus-4" }, message: { variant: "max" } }
    const constrainedOutput = { options: { effort: "max" } as Record<string, unknown> }
    expect(hooksCore.resolveAnthropicEffort(constrainedInput, constrainedOutput).reason).toBe("clamped-existing")
    await hooksCore.createAnthropicEffortHook()["chat.params"](constrainedInput, constrainedOutput)
    expect(constrainedOutput.options.effort).toBe("high")
    expect(constrainedInput.message.variant).toBe("high")
    expect(hooksCore.resolveAnthropicEffort({ sessionID: "s", agent: {}, model: { providerID: "anthropic", modelID: "claude-sonnet-4" }, message: { variant: "max" } }, { options: { effort: "high" } })).toEqual({ effort: "high", reason: "existing-effort" })

    expect(hooksCore.resolveAnthropicEffort({ sessionID: "s", agent: { name: "title" }, model: { providerID: "anthropic", modelID: "claude-opus-4" }, message: { variant: "max" } }, { options: {} }).reason).toBe("internal-agent")
    expect(hooksCore.resolveAnthropicEffort({ sessionID: "s", agent: {}, model: { providerID: "anthropic", modelID: "claude-haiku-4" }, message: { variant: "max" } }, { options: {} }).reason).toBe("unsupported-model")
    expect(hooksCore.resolveAnthropicEffort({ sessionID: "s", agent: {}, model: { providerID: "openai", modelID: "gpt-5.5" }, message: { variant: "max" } }, { options: {} }).reason).toBe("not-claude")
    expect(hooksCore.resolveAnthropicEffort({ sessionID: "s", agent: {}, model: { providerID: "anthropic", modelID: "claude-sonnet-4" }, message: { variant: "high" } }, { options: {} }).reason).toBe("variant-not-max")
  })

  test("ports think mode keyword detection and high variant activation", async () => {
    expect(detectThinkKeyword("please think carefully")).toBe(true)
    expect(detectThinkKeyword("검토 제대로 해줘")).toBe(true)
    expect(detectThinkKeyword("`think` as code only")).toBe(false)
    expect(isAlreadyHighReasoningVariant("anthropic/claude-sonnet-4-6-high")).toBe(true)
    expect(isAlreadyHighReasoningVariant("openai/gpt-5.4")).toBe(false)

    expect(resolveThinkMode([{ type: "text", text: "hello" }], { providerID: "openai", modelID: "gpt-5.4" }, {})).toEqual({ state: { requested: false, modelSwitched: false, variantSet: false } })
    expect(resolveThinkMode([{ type: "text", text: "think" }], undefined, {})).toEqual({ state: { requested: true, modelSwitched: false, variantSet: false } })
    expect(resolveThinkMode([{ type: "text", text: "think" }], { providerID: "openai", modelID: "gpt-5.4" }, { variant: "low" })).toEqual({ state: { requested: true, modelSwitched: false, variantSet: false } })
    expect(resolveThinkMode([{ type: "text", text: "think" }], { providerID: "anthropic", modelID: "claude-sonnet-4-6-high" }, {})).toEqual({ state: { requested: true, modelSwitched: false, variantSet: false, providerID: "anthropic", modelID: "claude-sonnet-4-6-high" } })
    expect(resolveThinkMode([{ type: "text", text: "think" }], { providerID: "openai", modelID: "gpt-5.4" }, {})).toEqual({ state: { requested: true, modelSwitched: false, variantSet: true, providerID: "openai", modelID: "gpt-5.4" }, variant: "high" })

    const hook = createThinkModeHook()
    const output = { message: {}, parts: [{ type: "text", text: "ultrathink this" }] }
    await hook["chat.message"]({ sessionID: "think-session", model: { providerID: "openai", modelID: "gpt-5.4" } }, output)
    expect(output.message).toEqual({ variant: "high" })
    expect(hook.getState("think-session")?.variantSet).toBe(true)
    hook.clear("think-session")
    expect(hook.getState("think-session")).toBeUndefined()
    await hook["chat.message"]({ sessionID: "think-session", model: { providerID: "openai", modelID: "gpt-5.4" } }, { message: {}, parts: [{ type: "text", text: "think again" }] })
    await hook.event({ event: { type: "session.deleted", properties: { sessionID: "think-session" } } })
    expect(hook.getState("think-session")).toBeUndefined()
  })

  test("repairs assistant tool messages with previous signed thinking blocks", async () => {
    const signedThinking = { type: "thinking", thinking: "plan", signature: "sig_1" }
    const messages: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [signedThinking, { type: "text", text: "ready" }] },
      { info: { role: "user" }, parts: [{ type: "text", text: "run tool" }] },
      { info: { role: "assistant" }, parts: [{ type: "tool_use", id: "tool_1" }] },
    ]

    expect(hasSignedThinkingBlocksInHistory(messages)).toBe(true)
    repairThinkingBlockMessages(messages)
    expect(messages[2].parts[0]).toBe(signedThinking)

    const hook = createThinkingBlockValidatorHook()
    const hookMessages: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "redacted_thinking", signature: "sig_2" }] },
      { info: { role: "assistant" }, parts: [{ type: "text", text: "needs prefix" }] },
    ]
    await hook["experimental.chat.messages.transform"]({}, { messages: hookMessages })
    expect(hookMessages[1].parts[0]).toBe(hookMessages[0].parts[0])
  })

  test("does not inject unsigned, synthetic, GPT reasoning, or already-prefixed thinking blocks", () => {
    const unsigned: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "thinking", thinking: "no signature" }] },
      { info: { role: "assistant" }, parts: [{ type: "tool_use" }] },
    ]
    repairThinkingBlockMessages(unsigned)
    expect(unsigned[1].parts).toEqual([{ type: "tool_use" }])

    const synthetic: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "thinking", signature: "sig", synthetic: true }] },
      { info: { role: "assistant" }, parts: [{ type: "tool_use" }] },
    ]
    repairThinkingBlockMessages(synthetic)
    expect(synthetic[1].parts).toEqual([{ type: "tool_use" }])

    const reasoning: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "reasoning", text: "gpt" }] },
      { info: { role: "assistant" }, parts: [{ type: "tool_use" }] },
    ]
    repairThinkingBlockMessages(reasoning)
    expect(reasoning[1].parts).toEqual([{ type: "tool_use" }])

    const alreadyPrefixed: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "thinking", signature: "sig" }] },
      { info: { role: "assistant" }, parts: [{ type: "reasoning" }, { type: "tool_use" }] },
    ]
    repairThinkingBlockMessages(alreadyPrefixed)
    expect(alreadyPrefixed[1].parts).toEqual([{ type: "reasoning" }, { type: "tool_use" }])

    const noPreviousThinking: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "tool_use" }] },
      { info: { role: "assistant" }, parts: [{ type: "thinking", signature: "sig" }] },
    ]
    repairThinkingBlockMessages(noPreviousThinking)
    expect(noPreviousThinking[0].parts).toEqual([{ type: "tool_use" }])
  })

  test("ports bash file read guard warning behavior", async () => {
    expect(isSimpleFileReadCommand("cat README.md")).toBe(true)
    expect(isSimpleFileReadCommand("head -n 5 package.json")).toBe(true)
    expect(isSimpleFileReadCommand("tail log.txt")).toBe(true)
    expect(isSimpleFileReadCommand("cat -n README.md")).toBe(false)
    expect(isSimpleFileReadCommand("cat README.md | grep x")).toBe(false)

    const hook = createBashFileReadGuardHook()
    const output = { args: { command: "cat README.md" }, message: undefined as string | undefined }
    await hook["tool.execute.before"]({ tool: "bash" }, output)
    expect(output.message).toBe(BASH_FILE_READ_WARNING_MESSAGE)

    const ignored = { args: { command: "ls" }, message: undefined as string | undefined }
    await hook["tool.execute.before"]({ tool: "bash" }, ignored)
    expect(ignored.message).toBeUndefined()
  })

  test("ports webfetch redirect error normalization", async () => {
    expect(buildWebFetchRedirectLimitMessage("https://example.com")).toBe("Error: WebFetch failed: exceeded maximum redirects (10) for https://example.com")
    expect(normalizeWebFetchRedirectOutput("Error: too many redirects")).toBe("Error: WebFetch failed: exceeded maximum redirects (10)")
    expect(normalizeWebFetchRedirectOutput("ok")).toBe("ok")

    const hook = createWebFetchRedirectGuardHook()
    const output = { args: { url: "https://example.com", redirectFailed: true } }
    await hook["tool.execute.before"]({ tool: "webfetch", sessionID: "s", callID: "c" }, output)
    const after = { title: "", output: "ignored", metadata: {} }
    await hook["tool.execute.after"]({ tool: "webfetch", sessionID: "s", callID: "c" }, after)
    expect(after.output).toBe("Error: WebFetch failed: exceeded maximum redirects (10) for https://example.com")

    const direct = { title: "", output: "Error: redirect loop detected", metadata: {} }
    await hook["tool.execute.after"]({ tool: "webfetch", sessionID: "s", callID: "other" }, direct)
    expect(direct.output).toBe("Error: WebFetch failed: exceeded maximum redirects (10)")
  })

  test("ports write existing file guard read-before-write policy", async () => {
    expect(isOverwriteEnabled(true)).toBe(true)
    expect(isOverwriteEnabled("true")).toBe(true)
    expect(isOverwriteEnabled("false")).toBe(false)
    expect(isOmoWorkspacePath("/repo/.omo/plans/a.md")).toBe(true)
    expect(isOmoWorkspacePath("/repo/src/a.ts")).toBe(false)
    const readPermissions = new Set<string>()
    const exists = (filePath: string) => filePath !== "missing.txt"
    expect(resolveWriteExistingFileGuard({ tool: "bash", sessionID: "s" }, { filePath: "a.txt" }, { exists, readPermissions })).toBe("allow")
    expect(resolveWriteExistingFileGuard({ tool: "read", sessionID: "s" }, { filePath: "a.txt" }, { exists, readPermissions })).toBe("register-read")
    expect(readPermissions.has("a.txt")).toBe(true)
    expect(resolveWriteExistingFileGuard({ tool: "read" }, { filePath: "a.txt" }, { exists, readPermissions })).toBe("allow")
    expect(resolveWriteExistingFileGuard({ tool: "read", sessionID: "s" }, { filePath: "missing.txt" }, { exists, readPermissions })).toBe("allow")
    expect(resolveWriteExistingFileGuard({ tool: "write", sessionID: "s" }, { filePath: "a.txt" }, { exists, readPermissions })).toBe("allow")
    expect(readPermissions.has("a.txt")).toBe(false)
    expect(resolveWriteExistingFileGuard({ tool: "write", sessionID: "s" }, { filePath: "a.txt" }, { exists, readPermissions })).toBe("block")
    expect(resolveWriteExistingFileGuard({ tool: "write", sessionID: "s" }, { filePath: "missing.txt" }, { exists, readPermissions })).toBe("allow")
    expect(resolveWriteExistingFileGuard({ tool: "write", sessionID: "s" }, { filePath: ".omo/plans/a.md" }, { exists, readPermissions })).toBe("allow")
    expect(resolveWriteExistingFileGuard({ tool: "write", sessionID: "s" }, { filePath: "a.txt", overwrite: "true" }, { exists, readPermissions })).toBe("allow")

    const hook = createWriteExistingFileGuardHook({ exists })
    await hook["tool.execute.before"]({ tool: "read", sessionID: "s1" }, { args: { filePath: "a.txt" } })
    expect(hook.getReadPermissions("s1").has("a.txt")).toBe(true)
    const allowedWrite: { args: { filePath: string; overwrite?: string } } = { args: { filePath: "a.txt", overwrite: "false" } }
    await hook["tool.execute.before"]({ tool: "write", sessionID: "s1" }, allowedWrite)
    expect(allowedWrite.args).toEqual({ filePath: "a.txt" })
    try {
      await hook["tool.execute.before"]({ tool: "write", sessionID: "s1" }, { args: { filePath: "a.txt" } })
      throw new Error("expected write guard to block")
    } catch (error) {
      expect(error).toBeInstanceOf(Error)
      expect((error as Error).message).toBe("File already exists. Use edit tool instead.")
    }
    await hook.event({ event: { type: "session.deleted", properties: { sessionID: "s1" } } })
    expect(hook.getReadPermissions("s1").size).toBe(0)
  })

  test("ports interactive bash tmux session tracking", async () => {
    expect(parseTmuxCommand("new-session -d -s omo-dev")).toEqual({ subCommand: "new-session", sessionName: "omo-dev" })
    expect(parseTmuxCommand("-L sock -- kill-session -t 'omo-dev:1.2'")).toEqual({ subCommand: "kill-session", sessionName: "omo-dev" })
    expect(parseTmuxCommand("-V")).toEqual({ subCommand: "", sessionName: null })
    expect(isOmoTmuxSession("omo-dev")).toBe(true)
    expect(isOmoTmuxSession("dev")).toBe(false)
    expect(buildInteractiveBashSessionReminder([])).toBe("")
    expect(buildInteractiveBashSessionReminder(["omo-dev"])).toContain("omo-dev")

    const hook = createInteractiveBashSessionHook({ now: () => 42 })
    const output = { title: "", output: "ok", metadata: {} }
    await hook["tool.execute.after"]({ tool: "interactive_bash", sessionID: "s", args: { tmux_command: "new-session -d -s omo-dev" } }, output)
    expect(output.output).toContain("Active omo-*")
    expect([...hook.getState("s").tmuxSessions]).toEqual(["omo-dev"])
    await hook["tool.execute.after"]({ tool: "interactive_bash", sessionID: "s", args: { tmux_command: "kill-session -t omo-dev:1" } }, output)
    expect([...hook.getState("s").tmuxSessions]).toEqual([])
    await hook["tool.execute.after"]({ tool: "interactive_bash", sessionID: "s", args: { tmux_command: "new-session -d -s dev" } }, output)
    expect([...hook.getState("s").tmuxSessions]).toEqual([])
    await hook["tool.execute.after"]({ tool: "interactive_bash", sessionID: "s", args: { tmux_command: "kill-server" } }, output)
    expect([...hook.getState("s").tmuxSessions]).toEqual([])
    const errorOutput = { title: "", output: "Error: failed", metadata: {} }
    await hook["tool.execute.after"]({ tool: "interactive_bash", sessionID: "s", args: { tmux_command: "new-session -s omo-error" } }, errorOutput)
    expect([...hook.getState("s").tmuxSessions]).toEqual([])
    await hook.event({ event: { type: "session.deleted", properties: { sessionID: "s" } } })
    expect(hook.getState("s").tmuxSessions.size).toBe(0)
  })

  test("ports read image resizer sizing and appendix formatting", () => {
    expect(calculateTargetDimensions(100, 100)).toBeNull()
    expect(calculateTargetDimensions(0, 100)).toBeNull()
    expect(calculateTargetDimensions(3000, 1500, 1500)).toEqual({ width: 1500, height: 750 })
    expect(calculateTargetDimensions(1500, 3000, 1500)).toEqual({ width: 750, height: 1500 })
    expect(calculateImageTokens(750, 750)).toBe(750)
    const appendix = formatImageResizeAppendix([
      { filename: "a.png", originalDims: { width: 3000, height: 1500 }, resizedDims: { width: 1500, height: 750 }, status: "resized" },
      { filename: "b.png", originalDims: { width: 100, height: 100 }, resizedDims: null, status: "within-limits" },
      { filename: "c.png", originalDims: { width: 3000, height: 3000 }, resizedDims: null, status: "resize-skipped" },
      { filename: "c2.png", originalDims: { width: 3000, height: 3000 }, resizedDims: null, status: "resized" },
      { filename: "d.png", originalDims: null, resizedDims: null, status: "unknown-dims" },
    ])
    expect(appendix).toContain("[Image Resize Info]")
    expect(appendix).toContain("3000x1500 -> 1500x750")
    expect(appendix).toContain("within limits")
    expect(appendix).toContain("image removed")
    expect(appendix).toContain("resize skipped")
    expect(appendix).toContain("dimensions could not be parsed")
    expect(formatImageResizeAppendix([{ filename: "x.png", originalDims: { width: 1, height: 1 }, resizedDims: null, status: "within-limits" }])).toContain("[Image Info]")
  })

  test("ports keyword detector prompts without code false positives", async () => {
    expect(hooksCore.detectKeywords("ulw", "hephaestus", "gpt-5.5")[0]).toContain("ULTRAWORK MODE ENABLED")
    expect(removeKeywordCodeBlocks("search `ulw` ```\nhyperplan\n```")).toBe("search  ")
    expect(looksLikeSlashCommand(" /start now")).toBe(true)
    expect(looksLikeSlashCommand("not /start")).toBe(false)
    expect(detectKeywordsWithType("please search then hyperplan ultrawork", "hephaestus", "gpt-5.5").map(({ type }) => type)).toEqual(["ultrawork", "search", "hyperplan", "hyperplan-ultrawork"])
    expect(detectKeywordsWithType("hyperplan ultrawork", undefined, undefined, ["hyperplan"]).map(({ type }) => type)).toEqual(["ultrawork"])
    expect(detectKeywordsWithType("왜 broken인지 분석해줘", undefined, undefined).map(({ type }) => type)).toEqual(["analyze"])
    expect(detectKeywordsWithType("팀으로 하자", undefined, undefined).map(({ type }) => type)).toEqual(["team"])
    const hook = createKeywordDetectorHook()
    const output = { parts: [{ type: "text", text: "ulw", synthetic: false }] }
    await hook["chat.message"]({ agent: "hephaestus", model: { modelID: "gpt-5.5" } }, output)
    expect(output.parts.some((part) => part.synthetic === true && part.text?.includes("ULTRAWORK MODE ENABLED"))).toBe(true)
  })

  test("ports auto slash command parsing and template injection", async () => {
    expect(parseSlashCommand(" /build arg one ")).toEqual({ command: "build", args: "arg one", raw: "/build arg one" })
    expect(parseSlashCommand("not /build")).toBeNull()
    expect(detectSlashCommand("```\n/build\n```\n/build real")).toEqual({ command: "build", args: "real", raw: "/build real" })
    expect(detectSlashCommand("/ulw-loop")).toBeNull()
    expect(findSlashCommandPartIndex([{ type: "text", text: "hello" }, { type: "text", text: "/build", synthetic: false }])).toBe(1)
    expect(findSlashCommandPartIndex([{ type: "text", text: "/build", synthetic: true }])).toBe(-1)
    const template = formatSlashCommandTemplate({ name: "build", scope: "project", description: "Build it", model: "gpt", agent: "hephaestus", content: "Run $ARGUMENTS and ${user_message}" }, "now")
    expect(template).toContain("# /build Command")
    expect(template).toContain("Run now and now")
    const hook = createAutoSlashCommandHook({ commands: [{ name: "build", scope: "project", content: "Do $ARGUMENTS" }] })
    const output = { parts: [{ type: "text", text: "/build fast", synthetic: false }] }
    await hook["chat.message"]({}, output)
    expect(output.parts[0].text).toContain(AUTO_SLASH_COMMAND_TAG_OPEN)
    expect(output.parts[0].text).toContain(AUTO_SLASH_COMMAND_TAG_CLOSE)
    expect(output.parts[0].text).toContain("Do fast")
  })

  test("ports hashline read enhancer and write success normalization", async () => {
    expect(computeLineHash(1, "hello")).toHaveLength(2)
    expect(computeLineHash(1, "this line is definitely longer than sixteen bytes")).toHaveLength(2)
    expect(formatHashLine(1, "hello")).toMatch(/^1#[ZPMQVRWSNKTXJBYH]{2}\|hello$/)
    expect(transformHashlineReadOutput("1: hello\n2| world\nnot a line")).toMatch(/^1#[ZPMQVRWSNKTXJBYH]{2}\|hello\n2#[ZPMQVRWSNKTXJBYH]{2}\|world\nnot a line$/)
    expect(transformHashlineReadOutput("<content>1: hello\n</content>")).toMatch(/^<content>\n1#[ZPMQVRWSNKTXJBYH]{2}\|hello\n<\/content>$/)
    expect(transformHashlineReadOutput("<file>\nnot text\n</file>")).toBe("<file>\nnot text\n</file>")
    expect(transformHashlineReadOutput(`1: long ${"x".repeat(3)} ${"... (line truncated to 2000 chars)"}`)).toContain("line truncated")
    expect(buildHashlineWriteSuccessOutput("ok", { lineCount: 3 })).toBe("File written successfully. 3 lines written.")
    expect(buildHashlineWriteSuccessOutput("Error: no", { lineCount: 3 })).toBe("Error: no")
    expect(buildHashlineWriteSuccessOutput("ok", {})).toBe("ok")
    const hook = createHashlineReadEnhancerHook({ enabled: true })
    const output = { output: "1: hello", metadata: {} }
    await hook["tool.execute.after"]({ tool: "read" }, output)
    expect(output.output).toMatch(/^1#[ZPMQVRWSNKTXJBYH]{2}\|hello$/)
    await hook["tool.execute.after"]({ tool: "write" }, output)
    expect(output.output).toMatch(/^1#[ZPMQVRWSNKTXJBYH]{2}\|hello$/)
    const writeOutput = { output: "ok", metadata: { lines: 2 } }
    await hook["tool.execute.after"]({ tool: "write" }, writeOutput)
    expect(writeOutput.output).toBe("File written successfully. 2 lines written.")
  })

  test("ports idle notification scheduler state machine", () => {
    expect(createIdleNotificationState().notifiedSessions.size).toBe(0)
    let currentTime = 1000
    const scheduler = createIdleNotificationScheduler({ maxTrackedSessions: 2, idleConfirmationDelay: 10, activityGracePeriodMs: 100, now: () => currentTime })
    expect(scheduler.scheduleIdleNotification("s1")).toBe("scheduled")
    expect(scheduler.scheduleIdleNotification("s1")).toBe("ignored-pending")
    expect(scheduler.markSessionActivity("s1")).toBe("ignored-pending")
    currentTime = 1200
    expect(scheduler.markSessionActivity("s1")).toBe("cancelled-by-activity")
    expect(scheduler.scheduleIdleNotification("s1")).toBe("scheduled")
    const version = scheduler.state.notificationVersions.get("s1") ?? 0
    expect(scheduler.shouldExecuteNotification("s1", version, true)).toBe(false)
    expect(scheduler.shouldExecuteNotification("s1", version, false)).toBe(true)
    expect(scheduler.scheduleIdleNotification("s1")).toBe("ignored-already-notified")
    scheduler.state.sessionActivitySinceIdle.add("s1")
    scheduler.finishNotification("s1")
    expect(scheduler.state.notifiedSessions.has("s1")).toBe(false)
    scheduler.state.executingNotifications.add("s2")
    expect(scheduler.scheduleIdleNotification("s2")).toBe("ignored-executing")
    expect(scheduler.deleteSession("s2")).toBe("deleted")
    expect(scheduler.scheduleIdleNotification("a")).toBe("scheduled")
    expect(scheduler.scheduleIdleNotification("b")).toBe("scheduled")
    expect(scheduler.scheduleIdleNotification("c")).toBe("scheduled")
    expect(scheduler.state.pendingSessions.size).toBe(2)
  })

  test("ports session recovery error classification", () => {
    expect(detectErrorType("assistant message prefill is unsupported")).toBe("assistant_prefill_unsupported")
    expect(detectErrorType("thinking must start with first block")).toBe("thinking_block_order")
    expect(detectErrorType("thinking block cannot be modified")).toBe("thinking_block_modified")
    expect(detectErrorType("thinking is disabled and cannot contain blocks")).toBe("thinking_disabled_violation")
    expect(detectErrorType("tool_use id without tool_result")).toBe("tool_result_missing")
    expect(detectErrorType("No such tool: dummy_tool")).toBe("unavailable_tool")
    expect(detectErrorType("ordinary error")).toBeNull()
    expect(extractMessageIndex({ data: { error: { message: "messages.12 failed" } } })).toBe(12)
    expect(detectErrorType({ code: "NoSuchToolError" })).toBe("unavailable_tool")
    const circularError: Record<string, unknown> = {}
    circularError.self = circularError
    expect(detectErrorType(circularError)).toBeNull()
    expect(extractUnavailableToolName("unavailable tool: webgrep.")).toBe("webgrep")
    const hook = createSessionRecoveryHook()
    expect(hook.isRecoverableError("no such tool: x")).toBe(true)
    expect(hook.extractUnavailableToolName("no such tool: x")).toBe("x")
  })

  test("ports anthropic token limit parser and formatting", () => {
    expect(isTokenLimitErrorText("thinking first block expected")).toBe(false)
    expect(isTokenLimitErrorText("prompt is too long: 200 tokens > 100 maximum")).toBe(true)
    expect(parseAnthropicTokenLimitError("prompt is too long: 200 tokens > 100 maximum")).toEqual({ currentTokens: 200, maxTokens: 100, errorType: "token_limit_exceeded", requestId: undefined })
    expect(parseAnthropicTokenLimitError("messages.7: non-empty content required")).toEqual({ currentTokens: 0, maxTokens: 0, errorType: "non-empty content", messageIndex: 7 })
    expect(parseAnthropicTokenLimitError({ data: { responseBody: '{"type":"error","error":{"message":"prompt is too long: prompt 250 tokens exceeds 200"},"request_id":"req_1"}' } })).toEqual({ currentTokens: 250, maxTokens: 200, errorType: "token_limit_exceeded", requestId: "req_1" })
    expect(parseAnthropicTokenLimitError({ message: "context_length_exceeded" })).toEqual({ currentTokens: 0, maxTokens: 0, errorType: "token_limit_exceeded_unknown" })
    expect(parseAnthropicTokenLimitError({ code: "context_length_exceeded" })).toEqual({ currentTokens: 0, maxTokens: 0, errorType: "token_limit_exceeded_unknown" })
    const circularTokenError: Record<string, unknown> = {}
    circularTokenError.self = circularTokenError
    expect(parseAnthropicTokenLimitError(circularTokenError)).toBeNull()
    expect(parseAnthropicTokenLimitError("plain error")).toBeNull()
    expect(formatBytes(100)).toBe("100B")
    expect(formatBytes(2048)).toBe("2.0KB")
    expect(formatBytes(2 * 1024 * 1024)).toBe("2.0MB")
  })

  test("ports team mailbox and team mode status injection", async () => {
    const messages = [
      { info: { role: "assistant", sessionID: "s" }, parts: [{ type: "text", text: "hello" }] },
      { info: { role: "user", sessionID: "s" }, parts: [{ type: "text", text: "team mode please" }] },
    ]
    expect(buildTeamMailboxTurnMarker("s", messages)).toBe("s#2")
    expect(injectTeamMailboxMessage(messages, "s", "mail").map((message) => message.info.role)).toEqual(["assistant", "user", "user"])
    const noUser = [{ info: { role: "assistant", sessionID: "s" }, parts: [{ type: "text", text: "hello" }] }]
    expect(injectTeamMailboxMessage(noUser, "s", "mail")[0].parts[0].text).toBe("mail")
    const mailboxHook = createTeamMailboxInjector({ enabled: true, getInjection: (_sessionID, turnMarker) => ({ injected: turnMarker === "s#2", content: "mail" }) })
    const mailboxOutput = { messages: [...messages] }
    await mailboxHook["experimental.chat.messages.transform"]({ sessionID: "s" }, mailboxOutput)
    expect(mailboxOutput.messages.some((message) => message.parts[0].text === "mail")).toBe(true)
    const inferredMailboxOutput = { messages: [...messages] }
    await mailboxHook["experimental.chat.messages.transform"]({}, inferredMailboxOutput)
    expect(inferredMailboxOutput.messages.some((message) => message.parts[0].text === "mail")).toBe(true)
    const missingSessionOutput = { messages: [{ info: { role: "user" }, parts: [{ type: "text", text: "team mode" }] }] }
    await mailboxHook["experimental.chat.messages.transform"]({}, missingSessionOutput)
    expect(missingSessionOutput.messages).toHaveLength(1)
    expect(buildTeamModeStatusContent()).toContain("team_* tools")
    const statusInjected = injectTeamModeStatus(messages, "s")
    expect(statusInjected.some((message) => typeof message.parts[0].text === "string" && message.parts[0].text.includes("team_mode_status"))).toBe(true)
    expect(injectTeamModeStatus(statusInjected, "s")).toBe(statusInjected)
    const statusHook = createTeamModeStatusInjector({ enabled: true })
    const statusOutput = { messages: [...messages] }
    await statusHook["experimental.chat.messages.transform"]({}, statusOutput)
    expect(statusOutput.messages.some((message) => typeof message.parts[0].text === "string" && message.parts[0].text.includes("Team mode is ENABLED"))).toBe(true)
  })

  test("ports compaction context and todo preservation decisions", () => {
    expect(buildCompactionContextPrompt()).toContain("## 8. Delegated Agent Sessions")
    expect(buildCompactionContextPrompt("task history")).toContain("task history")
    const injector = createCompactionContextInjector({ history: (sessionID) => `history:${sessionID}` })
    expect(injector.inject("s")).toContain("history:s")
    expect(injector.getTailState("s").currentHasOutput).toBe(false)
    expect(injector.clear("s")).toBe(true)
    const tail = createTailMonitorState()
    expect(finalizeTrackedAssistantMessage(tail)).toBe(0)
    expect(shouldTreatAssistantPartAsOutput({ type: "text", text: " " })).toBe(false)
    expect(shouldTreatAssistantPartAsOutput({ type: "tool" })).toBe(true)
    trackAssistantOutput(tail, "m1")
    expect(finalizeTrackedAssistantMessage(tail)).toBe(0)
    tail.currentMessageID = "m2"
    expect(finalizeTrackedAssistantMessage(tail)).toBe(1)
    const snapshot = [{ id: "real", content: "Do real work", status: "pending" as const }]
    const bootstrap = [{ id: "orchestrate-plan", content: "Complete ALL implementation tasks", status: "pending" as const }]
    expect(extractTodos({ data: snapshot })).toEqual(snapshot)
    expect(extractTodos(snapshot)).toEqual(snapshot)
    expect(extractTodos({})).toEqual([])
    expect(hasDetailedTodos(snapshot)).toBe(true)
    expect(isAtlasBootstrapTodoList(bootstrap)).toBe(true)
    expect(shouldRestoreOverCurrentTodos({ snapshot, currentTodos: [] })).toBe(true)
    expect(shouldRestoreOverCurrentTodos({ snapshot, currentTodos: bootstrap })).toBe(true)
    expect(shouldRestoreOverCurrentTodos({ snapshot, currentTodos: snapshot })).toBe(false)
    expect(replaceAtlasBootstrapTodos(bootstrap, snapshot)).toEqual(snapshot)
    expect(replaceAtlasBootstrapTodos(snapshot, bootstrap)).toEqual(snapshot)
  })

  test("ports unstable agent babysitter message analysis", () => {
    const message = { info: { role: "assistant", agent: "oracle", model: { providerID: "p", modelID: "gemini-pro", variant: "v" }, tools: { bash: "allow", bad: "x" } }, parts: [{ type: "text", text: "hello" }, { type: "thinking", thinking: "trace" }] }
    expect(getMessageInfo(message)?.model?.modelID).toBe("gemini-pro")
    expect(getMessageInfo(message)?.tools).toEqual({ bash: "allow" })
    expect(getMessageParts(message)).toEqual([{ type: "text", text: "hello", thinking: undefined }, { type: "thinking", text: undefined, thinking: "trace" }])
    expect(extractMessages({ data: [message] })).toEqual([message])
    expect(extractMessages([message])).toEqual([message])
    expect(extractMessages({})).toEqual([])
    expect(isUnstableTask({ id: "bg", description: "d", agent: "a", status: "running", model: { modelID: "minimax" } })).toBe(true)
    expect(isUnstableTask({ id: "bg", description: "d", agent: "a", status: "running", isUnstableAgent: true })).toBe(true)
    expect(isUnstableTask({ id: "bg", description: "d", agent: "a", status: "running" })).toBe(false)
    const reminder = buildUnstableAgentReminder({ id: "bg", description: "desc", agent: "oracle", status: "running", sessionId: "ses" }, null, 2000)
    expect(THINKING_SUMMARY_MAX_CHARS).toBe(500)
    expect(reminder).toContain("Unstable background agent appears idle for 2s")
    expect(reminder).toContain("background_output task_id=\"bg\"")
  })

  test("ports remaining notification update claude compaction runtime and continuation domains", () => {
    const messages = [
      { info: { role: "user" }, parts: [{ type: "text", text: " hello\nworld " }] },
      { info: { role: "assistant", error: true }, parts: [{ type: "text", text: "bad" }] },
      { info: { role: "assistant" }, parts: [{ type: "text", text: "done\nlast line" }] },
    ]
    expect(extractSessionNotificationText(messages[0])).toBe("hello\nworld")
    expect(findLastSessionNotificationMessage(messages, "assistant")).toBe(messages[2])
    expect(buildReadyNotificationContent({ sessionID: "s", sessionTitle: "Title", baseTitle: "Ready", baseMessage: "Done", messages })).toEqual({ title: "Ready · Title", message: "Done\nUser: hello world\nAssistant: last line" })
    const notification = createSessionNotification({ platform: "darwin", baseTitle: "Ready", baseMessage: "Done" })
    expect(notification.defaultSoundPath).toContain("Glass.aiff")
    expect(notification.buildContent({ sessionID: "s", messages: [] })).toEqual({ title: "Ready · s", message: "Done" })

    expect(isPrereleaseVersion("1.0.0-beta.1")).toBe(true)
    expect(isDistTag("next")).toBe(true)
    expect(isPrereleaseOrDistTag("next")).toBe(true)
    expect(extractChannel("1.0.0-rc.1")).toBe("rc")
    expect(extractChannel(null)).toBe("latest")
    expect(shouldShowAutoUpdateToast({ needsUpdate: true, isLocalDev: false, currentVersion: "1", latestVersion: "2" }, { autoUpdate: true })).toBe(true)
    expect(shouldShowAutoUpdateToast({ needsUpdate: true, isLocalDev: true, currentVersion: "1", latestVersion: "2" }, {})).toBe(false)
    expect(createAutoUpdateCheckerHook({}).shouldShowToast({ needsUpdate: true, isLocalDev: false, currentVersion: "1", latestVersion: "2" })).toBe(true)

    expect(listClaudeCodeHookNames()).toContain("chat.message")
    expect(createClaudeCodeHooksHook()["tool.execute.before"]).toBe(true)

    expect(shouldRunPreemptiveCompaction({ cached: { providerID: "p", modelID: "m", tokens: { input: 800, cache: { read: 100 } } }, actualLimit: 1000, compacted: false, inProgress: false, now: 100000 }).shouldRun).toBe(true)
    expect(shouldRunPreemptiveCompaction({ cached: undefined, actualLimit: 1000, compacted: false, inProgress: false, now: 100000 })).toEqual({ shouldRun: false, usageRatio: 0 })
    expect(shouldRunPreemptiveCompaction({ cached: { providerID: "p", modelID: "m", tokens: { input: 800 } }, actualLimit: 1000, compacted: false, inProgress: false, lastCompactionTime: 99999, now: 100000 })).toEqual({ shouldRun: false, usageRatio: 0 })
    expect(buildPreemptiveCompactionFailureToast("boom").message).toContain("78%")

    expect(getRuntimeFallbackErrorMessage({ data: { message: "RATE LIMIT 429" } })).toBe("rate limit 429")
    expect(extractRuntimeFallbackStatusCode({ status: 503 })).toBe(503)
    expect(classifyRuntimeFallbackErrorType({ name: "ProviderModelNotFoundError" })).toBe("model_not_found")
    expect(classifyRuntimeFallbackErrorType("api key must be a string")).toBe("invalid_api_key")
    expect(containsRuntimeFallbackErrorContent([{ type: "error", text: "bad" }])).toEqual({ hasError: true, errorMessage: "bad" })
    expect(containsRuntimeFallbackErrorContent([])).toEqual({ hasError: false })
    expect(isRuntimeFallbackRetryableError("service unavailable 503")).toBe(true)
    expect(isRuntimeFallbackRetryableError("temporarily overloaded")).toBe(true)
    const runtimeHook = createRuntimeFallbackHook({ retry_on_errors: [418] })
    expect(runtimeHook.isRetryableError({ status: 418 })).toBe(true)
    expect(runtimeHook.classifyErrorType({ name: "ProviderModelNotFoundError" })).toBe("model_not_found")

    const state = { stagnationCount: 0, awaitingPostInjectionProgressCheck: true }
    expect(trackContinuationProgress({ state, incompleteCount: 2 }).hasProgressed).toBe(false)
    expect(trackContinuationProgress({ state, incompleteCount: 1 }).hasProgressed).toBe(true)
    const stuckState = { stagnationCount: 0, lastIncompleteCount: 2, awaitingPostInjectionProgressCheck: true }
    expect(trackContinuationProgress({ state: stuckState, incompleteCount: 2 }).stagnationCount).toBe(1)
    expect(getTodoProgressSnapshot([{ id: "b", content: "B", status: "pending" }, { id: "a", content: "A", status: "completed" }])).toBe("a=completed|b=pending")
    const enforcer = createTodoContinuationEnforcer()
    expect(enforcer.getState("s").stagnationCount).toBe(0)
    enforcer.markRecovering("s")
    expect(enforcer.cleanup("s")).toBe(true)

    expect(parseUserRequest("<user-request>ulw 'Plan A' --worktree ../wt</user-request>")).toEqual({ planName: "Plan A", explicitWorktreePath: "../wt" })
    expect(parseUserRequest("none")).toEqual({ planName: null, explicitWorktreePath: null })
    expect(parseWorktreeListPorcelain("worktree /repo\nbranch refs/heads/main\n\nworktree /bare\nbare\n")).toEqual([{ path: "/repo", branch: "main", bare: false }, { path: "/bare", branch: undefined, bare: true }])
    expect(resolveStartWorkTemplate("<session-context>\nYou are starting a Sisyphus work session.\n$SESSION_ID $TIMESTAMP", { sessionID: "s", timestamp: "t", contextInfo: "ctx" })).toContain("s t")
    expect(resolveStartWorkTemplate("not start", { sessionID: "s", timestamp: "t", contextInfo: "ctx" })).toBeNull()
    const startWorkHook = createStartWorkHook()
    expect(startWorkHook.parseUserRequest("<user-request>ulw</user-request>")).toEqual({ planName: null, explicitWorktreePath: null })
    expect(startWorkHook.parseWorktreeListPorcelain("worktree /repo\n")).toEqual([{ path: "/repo", branch: undefined, bare: false }])
    expect(startWorkHook.resolveStartWorkTemplate("<session-context>\nYou are starting a Sisyphus work session.\n$SESSION_ID", { sessionID: "s", timestamp: "t", contextInfo: "ctx" })).toContain("s")

    expect(parseTrackedTaskFromPrompt("## 1. TASK\n1. Build feature")?.key).toBe("todo:1")
    expect(parseTrackedTaskFromPrompt("## 1. TASK\nF2. Final check")?.key).toBe("final-wave:f2")
    expect(parseTrackedTaskFromPrompt("## 1. TASK\n\nnot a task\n\n1. Still within scan")?.title).toBe("Still within scan")
    expect(parseTrackedTaskFromPrompt("## 1. TASK\nnot a task")).toBeNull()
    expect(parseTrackedTaskFromPrompt("no task")).toBeNull()
    expect(buildAtlasSingleTaskPrompt("do work")).toContain("Complete exactly one assigned task")
    expect(buildAtlasSingleTaskPrompt("<system-reminder>x</system-reminder>")).toBe("<system-reminder>x</system-reminder>")
    expect(shouldWarnAtlasDirectModification({ tool: "write", filePath: "src/a.ts", isOmoPath: false })).toBe(true)
    expect(shouldWarnAtlasDirectModification({ tool: "bash", filePath: "src/a.ts", isOmoPath: false })).toBe(false)
    expect(resolveAtlasPendingTaskRef({ callID: "c", requestedSessionId: "ses" })).toEqual({ kind: "skip", reason: "explicit_resume" })
    expect(resolveAtlasPendingTaskRef({ callID: "c", prompt: "## 1. TASK\n1. Build feature" })).toEqual({ kind: "track", task: { key: "todo:1", label: "1", title: "Build feature" } })
    expect(resolveAtlasPendingTaskRef({ callID: "c", prompt: "## 1. TASK\n1. Build feature", existingKeys: ["todo:1"] })).toEqual({ kind: "skip", reason: "ambiguous_task_key", task: { key: "todo:1", label: "1", title: "Build feature" } })
    const atlasHook = createAtlasHook()
    expect(atlasHook.parseTrackedTaskFromPrompt("## 1. TASK\n1. Build feature")?.key).toBe("todo:1")
    expect(atlasHook.buildAtlasSingleTaskPrompt("do work")).toContain("Complete exactly one assigned task")
    expect(atlasHook.resolveAtlasPendingTaskRef({ callID: "c", requestedSessionId: "ses" })).toEqual({ kind: "skip", reason: "explicit_resume" })
    expect(atlasHook.shouldWarnAtlasDirectModification({ tool: "edit", filePath: "a", isOmoPath: false })).toBe(true)
    expect(resolveOmoHookExitPath("missing", "workflow")).toBe("limited-redesign")
    expect(resolveOmoHookExitPath("missing", "notification")).toBe("limited-redesign")
    expect(resolveOmoHookExitPath("missing", "other")).toBe("pure-domain-port")
    expect(resolveOmoHookTargetPackage("adapter-bound", "workflow")).toBe("@oh-my-opencode/adapter-opencode")
    expect(resolveOmoHookTargetPackage("behavior-mapped", "model")).toBe("@oh-my-opencode/model-core")
    expect(resolveOmoHookTargetPackage("behavior-mapped", "context")).toBe("@oh-my-opencode/agents-md-core")
    expect(resolveOmoHookTargetPackage("behavior-mapped", "loop")).toBe("@oh-my-opencode/ulw-kernel")
    expect(resolveOmoHookTargetPackage("behavior-mapped", "guard")).toBe("@oh-my-opencode/hooks-core")
    expect(resolveOmoHookTestTypes("adapter-bound")).toEqual(["adapter", "integration", "manual-qa"])
    expect(resolveOmoHookTestTypes("missing")).toEqual(["unit", "parity"])
  })

  test("parses escaped tmux session names", () => {
    expect(parseTmuxCommand("new-session -s omo\\ dev")).toEqual({ subCommand: "new-session", sessionName: "omo dev" })
  })

  test("ports empty task response detector", async () => {
    expect(recoverEmptyTaskOutput("task", "   ")).toBe(EMPTY_TASK_RESPONSE_WARNING)
    expect(recoverEmptyTaskOutput("bash", "   ")).toBe("   ")
    const hook = createEmptyTaskResponseDetectorHook()
    const output = { output: "", title: "", metadata: {} }
    await hook["tool.execute.after"]({ tool: "Task" }, output)
    expect(output.output).toBe(EMPTY_TASK_RESPONSE_WARNING)
  })

  test("ports json error recovery reminder behavior", async () => {
    expect(recoverJsonErrorOutput("custom", "invalid json in args")).toContain(JSON_ERROR_REMINDER_MARKER)
    expect(recoverJsonErrorOutput("bash", "invalid json in args")).toBe("invalid json in args")
    const already = `${JSON_ERROR_REMINDER_MARKER}\ninvalid json`
    expect(recoverJsonErrorOutput("custom", already)).toBe(already)
    const hook = createJsonErrorRecoveryHook()
    const output = { output: "Unexpected end of JSON input", title: "", metadata: {} }
    await hook["tool.execute.after"]({ tool: "custom" }, output)
    expect(output.output).toContain(JSON_ERROR_REMINDER_MARKER)
  })

  test("ports edit error recovery reminder behavior", async () => {
    expect(recoverEditErrorOutput("Edit", "oldString not found in file")).toContain(EDIT_ERROR_REMINDER)
    expect(recoverEditErrorOutput("Edit", "oldString and newString must be different")).toContain("READ the file immediately")
    expect(recoverEditErrorOutput("Edit", "oldString found multiple times")).toContain("IMMEDIATE ACTION")
    expect(recoverEditErrorOutput("bash", "oldString not found")).toBe("oldString not found")
    expect(recoverEditErrorOutput("Edit", "ok")).toBe("ok")
    const output = { output: "oldString not found", title: "", metadata: {} }
    await createEditErrorRecoveryHook()["tool.execute.after"]({ tool: "Edit" }, output)
    expect(output.output).toContain(EDIT_ERROR_REMINDER)
  })

  test("ports tool output truncator behavior", async () => {
    expect(truncateToolOutput("bash", "x".repeat(20), { maxTokens: 1 })).toEqual({ output: "x".repeat(20), truncated: false })
    expect(truncateToolOutput("grep", "abcdef", { maxTokens: 1 })).toEqual({ output: "abcd\n\n[Tool output truncated to 1 tokens]", truncated: true })
    expect(truncateToolOutput("bash", "abcdef", { truncateAll: true, maxTokens: 1 }).truncated).toBe(true)
    expect(truncateToolOutput("WebFetch", "x".repeat(40_001)).truncated).toBe(true)
    const hook = createToolOutputTruncatorHook({ maxTokens: 1 })
    const output = { output: "abcdef", title: "", metadata: {} }
    await hook["tool.execute.after"]({ tool: "grep" }, output)
    expect(output.output).toBe("abcd\n\n[Tool output truncated to 1 tokens]")
  })

  test("ports todo/task status and tool guard behavior", async () => {
    expect(hasIncompleteTodos([])).toBe(false)
    expect(hasIncompleteTodos([{ status: "completed" }, { status: "cancelled" }])).toBe(false)
    expect(hasIncompleteTodos([{ status: "pending" }])).toBe(true)
    expect(shouldBlockTaskTodoTool("todowrite", true)).toBe(true)
    expect(shouldBlockTaskTodoTool("todowrite", false)).toBe(false)
    const disabler = createTasksTodowriteDisablerHook({ experimental: { task_system: true } })
    try {
      await disabler["tool.execute.before"]({ tool: "TodoWrite" })
      throw new Error("expected TodoWrite to be blocked")
    } catch (error) {
      expect(error).toBeInstanceOf(Error)
      expect((error as Error).message).toBe(TASK_TODOWRITE_REPLACEMENT_MESSAGE)
    }

    const definition = { description: "old", parameters: {} }
    await applyTodoDescriptionOverride({ toolID: "todowrite" }, definition)
    expect(definition.description).toBe(TODOWRITE_DESCRIPTION)
    const ignoredDefinition = { description: "old", parameters: {} }
    await applyTodoDescriptionOverride({ toolID: "bash" }, ignoredDefinition)
    expect(ignoredDefinition.description).toBe("old")

    expect(isNotepadPath("/repo/.sisyphus/notepads/a.md")).toBe(true)
    expect(isNotepadPath("notes/a.md")).toBe(false)
    const notepadGuard = createNotepadWriteGuardHook()
    try {
      await notepadGuard["tool.execute.before"]({ tool: "write" }, { args: { filePath: "/repo/.sisyphus/notepads/a.md" } })
      throw new Error("expected notepad write to be blocked")
    } catch (error) {
      expect(error).toBeInstanceOf(Error)
      expect((error as Error).message).toContain("append-only")
    }
  })

  test("ports tool pair validator repair behavior", async () => {
    const messages: MessageWithParts[] = [{ info: { role: "assistant", sessionID: "ses" }, parts: [{ type: "tool_use", id: "call_1" }] }]
    repairMissingToolResults(messages)
    expect(messages[1]).toEqual({ info: { role: "user", sessionID: "ses" }, parts: [{ type: "tool_result", toolUseId: "call_1", tool_use_id: "call_1", isError: true, content: [{ type: "text", text: TOOL_RESULT_PLACEHOLDER }] }] })

    const nextMessages: MessageWithParts[] = [
      { info: { role: "assistant" }, parts: [{ type: "tool", callID: "a" }, { type: "tool_use", id: "b" }, { type: "tool_use" }] },
      { info: { role: "user" }, parts: [{ type: "tool_result", tool_use_id: "a", content: [] }, { type: "tool_result", content: [] }] },
    ]
    await createToolPairValidatorHook()["experimental.chat.messages.transform"]({}, { messages: nextMessages })
    expect(nextMessages[1].parts.map((part) => part.toolUseId ?? part.tool_use_id).filter(Boolean)).toEqual(["a", "b"])
  })

  test("ports delegate retry, task resume info, and stop continuation guard", async () => {
    expect(hooksCore.detectDelegateTaskError("[ERROR] Missing load_skills")?.errorType).toBe("missing_load_skills")
    expect(addDelegateTaskRetryGuidance("task", "[ERROR] Missing load_skills")).toContain("missing_load_skills")
    expect(addDelegateTaskRetryGuidance("bash", "[ERROR] Missing load_skills")).toBe("[ERROR] Missing load_skills")
    const retryOutput = { output: "Invalid arguments: Must provide either category or subagent_type", title: "", metadata: {} }
    await createDelegateTaskRetryHook()["tool.execute.after"]({ tool: "task" }, retryOutput)
    expect(retryOutput.output).toContain("missing_category_or_agent")

    expect(appendTaskResumeInfo("task", "done", { taskId: "ses_123" })).toContain("task_id=\"ses_123\"")
    expect(appendTaskResumeInfo("task", "Error: failed", { taskId: "ses_123" })).toBe("Error: failed")
    const resumeOutput = { output: "done\n<task_metadata>\nsession_id: ses_meta\n</task_metadata>", title: "", metadata: {} }
    await createTaskResumeInfoHook()["tool.execute.after"]({ tool: "Task" }, resumeOutput)
    expect(resumeOutput.output).toContain("task_id=\"ses_meta\"")
    expect(appendTaskResumeInfo("task", "Session ID: ses_explicit", {})).toContain("task_id=\"ses_explicit\"")

    const guard = createStopContinuationGuardHook()
    guard.stop("ses_stop")
    expect(guard.isStopped("ses_stop")).toBe(true)
    guard.clear("ses_stop")
    expect(guard.isStopped("ses_stop")).toBe(false)
    guard.stop("ses_stop")
    await guard["chat.message"]({ sessionID: "ses_stop" })
    expect(guard.isStopped("ses_stop")).toBe(true)
    await guard.event({ event: { type: "session.deleted", properties: { sessionID: "ses_stop" } } })
    expect(guard.isStopped("ses_stop")).toBe(false)
    guard.stop("ses_id")
    await guard.event({ event: { type: "session.deleted", properties: { id: "ses_id" } } })
    expect(guard.isStopped("ses_id")).toBe(false)
    guard.stop("ses_nested")
    await guard.event({ event: { type: "session.deleted", properties: { session: { id: "ses_nested" } } } })
    expect(guard.isStopped("ses_nested")).toBe(false)
  })

  test("ports non-interactive environment command behavior", async () => {
    expect(detectBannedInteractiveCommand("git rebase -i main")).toBe("git rebase -i")
    expect(detectBannedInteractiveCommand("git status")).toBeUndefined()
    expect(buildNonInteractiveEnvPrefix("posix")).toContain("GIT_EDITOR=\":\"")
    expect(buildNonInteractiveEnvPrefix("cmd")).toContain("set GIT_EDITOR=:")
    expect(buildNonInteractiveEnvPrefix("powershell")).toContain("$env:GIT_EDITOR=':'")
    expect(buildNonInteractiveGitCommand("npm test")).toBe("npm test")
    expect(buildNonInteractiveGitCommand("git status")).toContain(" git status")
    const prefixed = buildNonInteractiveGitCommand("git status")
    expect(buildNonInteractiveGitCommand(prefixed)).toBe(prefixed)

    const hook = createNonInteractiveEnvHook()
    const output = { args: { command: "git rebase -i main" }, message: undefined as string | undefined }
    await hook["tool.execute.before"]({ tool: "bash" }, output)
    expect(output.message).toContain("interactive command")
    expect(output.args.command).toContain("GIT_TERMINAL_PROMPT")
    const ignored = { args: { command: "git status" }, message: undefined as string | undefined }
    await hook["tool.execute.before"]({ tool: "read" }, ignored)
    expect(ignored.args.command).toBe("git status")
  })

  test("ports category skill reminder formatting and injection", async () => {
    const reminder = buildCategorySkillReminderMessage([
      { name: "frontend-ui-ux", location: "plugin" },
      { name: "custom-skill", location: "user" },
    ])
    expect(reminder).toContain("**Built-in**: frontend-ui-ux")
    expect(reminder).toContain("**⚡ YOUR SKILLS (PRIORITY)**: custom-skill")
    expect(reminder).toContain('load_skills=["custom-skill"]')

    const hook = createCategorySkillReminderHook([{ name: "custom-skill", location: "user" }])
    const output = { title: "", output: "work", metadata: {} }
    await hook["tool.execute.after"]({ tool: "read", sessionID: "s1", agent: "sisyphus" }, output)
    await hook["tool.execute.after"]({ tool: "grep", sessionID: "s1", agent: "sisyphus" }, output)
    expect(output.output).toBe("work")
    await hook["tool.execute.after"]({ tool: "bash", sessionID: "s1", agent: "sisyphus" }, output)
    expect(output.output).toContain("[Category+Skill Reminder]")
    const afterReminder = output.output
    await hook["tool.execute.after"]({ tool: "write", sessionID: "s1", agent: "sisyphus" }, output)
    expect(output.output).toBe(afterReminder)

    const delegated = { title: "", output: "delegated", metadata: {} }
    await hook["tool.execute.after"]({ tool: "task", sessionID: "s2", agent: "atlas" }, delegated)
    await hook["tool.execute.after"]({ tool: "read", sessionID: "s2", agent: "atlas" }, delegated)
    await hook["tool.execute.after"]({ tool: "grep", sessionID: "s2", agent: "atlas" }, delegated)
    await hook["tool.execute.after"]({ tool: "bash", sessionID: "s2", agent: "atlas" }, delegated)
    expect(delegated.output).toBe("delegated")

    await hook.event({ event: { type: "session.deleted", properties: { sessionID: "s1" } } })
    const ignored = { title: "", output: "no reminder", metadata: {} }
    await hook["tool.execute.after"]({ tool: "read", sessionID: "s3", agent: "hephaestus" }, ignored)
    expect(ignored.output).toBe("no reminder")
  })

  test("ports agent usage reminder state behavior", async () => {
    expect(isOrchestratorAgentForReminder(undefined)).toBe(true)
    expect(isOrchestratorAgentForReminder("Sisyphus Junior")).toBe(true)
    expect(isOrchestratorAgentForReminder("explore")).toBe(false)
    const state: AgentUsageState = { sessionID: "s", agentUsed: false, reminderCount: 0, updatedAt: 0 }
    expect(shouldRemindAgentUsage("grep", state, "sisyphus", 3, () => 10)).toBe(true)
    expect(state).toEqual({ sessionID: "s", agentUsed: false, reminderCount: 1, updatedAt: 10 })
    expect(shouldRemindAgentUsage("task", state, "sisyphus", 3, () => 20)).toBe(false)
    expect(state.agentUsed).toBe(true)
    expect(state.updatedAt).toBe(20)
    expect(shouldRemindAgentUsage("grep", state, "sisyphus", 3, () => 30)).toBe(false)
    expect(shouldRemindAgentUsage("grep", { sessionID: "sub", agentUsed: false, reminderCount: 0, updatedAt: 0 }, "explore")).toBe(false)
    expect(shouldRemindAgentUsage("bash", { sessionID: "bash", agentUsed: false, reminderCount: 0, updatedAt: 0 }, "sisyphus")).toBe(false)
    expect(shouldRemindAgentUsage("grep", { sessionID: "max", agentUsed: false, reminderCount: 3, updatedAt: 0 }, "sisyphus")).toBe(false)

    const hook = createAgentUsageReminderHook({ getAgent: () => "hephaestus", now: () => 99 })
    const output = { title: "", output: "search", metadata: {} }
    await hook["tool.execute.after"]({ tool: "webfetch", sessionID: "s1" }, output)
    expect(output.output).toBe(`search${AGENT_USAGE_REMINDER_MESSAGE}`)
    expect(hook.getState("s1")).toEqual({ sessionID: "s1", agentUsed: false, reminderCount: 1, updatedAt: 99 })
    await hook["tool.execute.after"]({ tool: "task", sessionID: "s1" }, output)
    expect(hook.getState("s1").agentUsed).toBe(true)
    await hook.event({ event: { type: "session.deleted", properties: { sessionID: "s1" } } })
    expect(hook.getState("s1")).toEqual({ sessionID: "s1", agentUsed: false, reminderCount: 0, updatedAt: 99 })
  })

  test("ports session notification formatting and background forwarding", async () => {
    expect(escapeAppleScriptText('say "hi" \\ done')).toBe('say \\"hi\\" \\\\ done')
    expect(escapePowerShellSingleQuotedText("can't stop")).toBe("can''t stop")
    const script = buildWindowsToastScript("can't", "hello")
    expect(script).toContain("can''t")
    expect(script).toContain("ToastNotificationManager")
    expect(script).not.toContain("\n")

    expect(normalizeNotificationPlatform("darwin")).toBe("darwin")
    expect(normalizeNotificationPlatform("freebsd")).toBe("unsupported")
    expect(getDefaultNotificationSoundPath("darwin")).toBe("/System/Library/Sounds/Glass.aiff")
    expect(getDefaultNotificationSoundPath("linux")).toContain("complete.oga")
    expect(getDefaultNotificationSoundPath("win32")).toBe("C:\\Windows\\Media\\notify.wav")
    expect(getDefaultNotificationSoundPath("unsupported")).toBe("")

    expect(shouldForwardBackgroundEvent("message.updated")).toBe(true)
    expect(shouldForwardBackgroundEvent("session.created")).toBe(false)
    const forwarded: string[] = []
    const injected: string[] = []
    const hook = createBackgroundNotificationHook({
      handleEvent: (event) => { forwarded.push(event.type) },
      injectPendingNotificationsIntoChatMessage: (output, sessionID) => {
        injected.push(sessionID)
        output.parts.push({ type: "text", text: "pending" })
      },
    })
    await hook.event({ event: { type: "message.updated" } })
    await hook.event({ event: { type: "session.created" } })
    expect(forwarded).toEqual(["message.updated"])
    const output = { parts: [] as Array<{ type: string; text?: string }> }
    await hook["chat.message"]({ sessionID: "s1" }, output)
    expect(injected).toEqual(["s1"])
    expect(output.parts).toEqual([{ type: "text", text: "pending" }])
  })

  test("ports context window monitor reminder behavior", async () => {
    expect(buildContextWindowReminder(200000)).toContain("200,000-token context window")
    expect(shouldWarnContextWindow({ input: 69, cache: { read: 0 } }, 100)).toBe(false)
    expect(shouldWarnContextWindow({ input: 50, cache: { read: 20 } }, 100)).toBe(true)
    expect(appendContextWindowStatus("done", { input: 120, cache: { read: 0 } }, 100)).toContain("100.0% used")

    const hook = createContextWindowMonitorHook({ resolveLimit: () => 100 })
    const output = { title: "", output: "tool result", metadata: {} }
    await hook["tool.execute.after"]({ sessionID: "s1" }, output)
    expect(output.output).toBe("tool result")
    await hook.event({ event: { type: "message.updated", properties: { sessionID: "s1", info: { role: "assistant", finish: true, providerID: "openai", modelID: "gpt-5.4", tokens: { input: 71 } } } } })
    await hook["tool.execute.after"]({ sessionID: "s1" }, output)
    expect(output.output).toContain("[SYSTEM DIRECTIVE: CONTEXT_WINDOW_MONITOR]")
    const once = output.output
    await hook["tool.execute.after"]({ sessionID: "s1" }, output)
    expect(output.output).toBe(once)
    await hook.event({ event: { type: "session.deleted", properties: { sessionID: "s1" } } })
    const afterDelete = { title: "", output: "again", metadata: {} }
    await hook["tool.execute.after"]({ sessionID: "s1" }, afterDelete)
    expect(afterDelete.output).toBe("again")

    const compactionHook = createContextWindowMonitorHook({ resolveLimit: () => 100, isCompactionAgent: () => true })
    const ignored = { title: "", output: "ignored", metadata: {} }
    await compactionHook.event({ event: { type: "message.updated", properties: { sessionID: "s2", info: { role: "assistant", finish: true, agent: "compactor", providerID: "openai", tokens: { input: 100 } } } } })
    await compactionHook["tool.execute.after"]({ sessionID: "s2" }, ignored)
    expect(ignored.output).toBe("ignored")
  })

  test("ports team tool gating decisions", async () => {
    const lead: TeamParticipant = { role: "lead", teamRunId: "team-1" }
    const member: TeamParticipant = { role: "member", teamRunId: "team-1", memberName: "builder" }
    const neither: TeamParticipant = { role: "neither" }
    expect(isUniversalTeamTool("team_send_message")).toBe(true)
    expect(isUniversalTeamTool("team_create")).toBe(false)
    expect(resolveTeamToolGate("bash", neither, {})).toBeUndefined()
    expect(resolveTeamToolGate("delegate-task", neither, {})).toBeUndefined()
    expect(resolveTeamToolGate("team_list", neither, {})).toBeUndefined()
    expect(resolveTeamToolGate("team_create", neither, {})).toBeUndefined()
    expect(resolveTeamToolGate("team_create", lead, {})).toContain("already a participant")
    expect(resolveTeamToolGate("team_delete", lead, { teamRunId: "team-1" })).toBeUndefined()
    expect(resolveTeamToolGate("team_delete", member, { teamRunId: "team-1" })).toBe("team_delete is lead-only")
    expect(resolveTeamToolGate("team_approve_shutdown", member, { teamRunId: "team-1", memberName: "builder" })).toBeUndefined()
    expect(resolveTeamToolGate("team_reject_shutdown", neither, { teamRunId: "team-1", memberName: "builder" })).toBe("team_reject_shutdown: caller must be target member or team lead")
    expect(resolveTeamToolGate("team_status", member, { teamRunId: "team-1" })).toBeUndefined()
    expect(resolveTeamToolGate("team_status", member, {})).toBe("team-mode tool team_status requires teamRunId argument")
    expect(resolveTeamToolGate("team_status", member, { teamRunId: "team-2" })).toBe("team-mode tool team_status denied: not a participant of team team-2")
    expect(resolveTeamToolGate("team_unknown", member, { teamRunId: "team-1" })).toBeUndefined()

    const hook = createTeamToolGating({ enabled: true, getParticipant: () => member })
    await hook["tool.execute.before"]({ tool: "team_status", sessionID: "s1" }, { args: { teamRunId: "team-1" } })
    try {
      await hook["tool.execute.before"]({ tool: "team_status", sessionID: "s1" }, { args: { teamRunId: "team-2" } })
      throw new Error("expected team gate to deny")
    } catch (error) {
      expect(error).toBeInstanceOf(Error)
      expect((error as Error).message).toContain("not a participant")
    }
    await createTeamToolGating({ enabled: false, getParticipant: () => neither })["tool.execute.before"]({ tool: "team_status", sessionID: "s1" }, { args: {} })
  })

  test("ports fsync skip warning formatting", () => {
    expect(describePathClassification("icloud")).toBe("iCloud Drive")
    expect(describePathClassification("onedrive")).toBe("OneDrive")
    expect(describePathClassification("desktop-sync")).toBe("Desktop sync (macOS)")
    expect(describePathClassification("network-drive")).toBe("Network drive")
    expect(describePathClassification("unknown")).toContain("filesystem")
    expect(formatFsyncSkipWarning([])).toBe("")
    const warning = formatFsyncSkipWarning([
      { filePath: "/Users/a/Library/Mobile Documents/file1", errorCode: "EINVAL", pathClassification: "icloud" },
      { filePath: "/Users/a/Library/Mobile Documents/file2", errorCode: "EINVAL", pathClassification: "icloud" },
      { filePath: "/Volumes/share/file3", errorCode: "ENOTSUP", pathClassification: "network-drive" },
      { filePath: "/tmp/file4", errorCode: "EINVAL", pathClassification: "unknown" },
      { filePath: "/tmp/file5", errorCode: "EINVAL", pathClassification: "icloud" },
      { filePath: "/tmp/file6", errorCode: "EINVAL", pathClassification: "unknown" },
    ])
    expect(warning).toContain("[fsync-skipped] 6 write(s)")
    expect(warning).toContain("Detected environment: iCloud Drive")
    expect(warning).toContain("... and 1 more")
  })

  test("ports legacy plugin toast decision behavior", async () => {
    expect(resolveLegacyPluginToastDecision({ hasLegacyEntry: false })).toBeUndefined()
    expect(resolveLegacyPluginToastDecision({ hasLegacyEntry: true, migration: { migrated: true, from: "oh-my-opencode", to: "oh-my-openagent" } })).toEqual({
      title: "Plugin Entry Migrated",
      message: '"oh-my-opencode" has been renamed to "oh-my-openagent" in your opencode.json.\nNo action needed.',
      variant: "success",
      duration: 8000,
    })
    expect(resolveLegacyPluginToastDecision({ hasLegacyEntry: true })?.variant).toBe("warning")

    const hook = createLegacyPluginToastDecisionHook(() => ({ hasLegacyEntry: true, migration: { migrated: true, from: "a", to: "b" } }))
    expect(await hook.event({ event: { type: "session.created", properties: {} } })).toEqual(expect.objectContaining({ variant: "success" }))
    expect(await hook.event({ event: { type: "session.created", properties: {} } })).toBeUndefined()
    const childHook = createLegacyPluginToastDecisionHook(() => ({ hasLegacyEntry: true }))
    expect(await childHook.event({ event: { type: "session.created", properties: { info: { parentID: "root" } } } })).toBeUndefined()
  })

  test("ports Prometheus md-only guard behavior", async () => {
    expect(isPrometheusAgent("Prometheus Planner")).toBe(true)
    expect(isPrometheusAgent("atlas")).toBe(false)
    expect(isPrometheusAllowedFile(".omo/plans/work.md", "/repo")).toBe(true)
    expect(isPrometheusAllowedFile("../outside/.omo/work.md", "/repo")).toBe(false)
    expect(isPrometheusAllowedFile(".omo/plans/work.txt", "/repo")).toBe(false)

    const hook = createPrometheusMdOnlyHook({ workspaceRoot: "/repo", agentName: "prometheus" })
    const taskOutput = { args: { prompt: "inspect only" } }
    await hook["tool.execute.before"]({ tool: "task" }, taskOutput)
    expect(taskOutput.args.prompt).toBe(PLANNING_CONSULT_WARNING + "inspect only")
    await hook["tool.execute.before"]({ tool: "task" }, taskOutput)
    expect(taskOutput.args.prompt).toBe(PLANNING_CONSULT_WARNING + "inspect only")

    const planOutput = { args: { filePath: ".omo/plans/work.md" }, message: "" }
    await hook["tool.execute.before"]({ tool: "Write" }, planOutput)
    expect(planOutput.message).toBe(PROMETHEUS_WORKFLOW_REMINDER)

    try {
      await hook["tool.execute.before"]({ tool: "Edit" }, { args: { filePath: "src/index.ts" } })
      throw new Error("expected prometheus guard to block")
    } catch (error) {
      expect(error).toBeInstanceOf(Error)
      expect((error as Error).message).toContain(".omo/*.md")
    }

    const ignored = { args: { filePath: "src/index.ts" }, message: "" }
    await createPrometheusMdOnlyHook({ workspaceRoot: "/repo", agentName: "atlas" })["tool.execute.before"]({ tool: "Write" }, ignored)
    expect(ignored.message).toBe("")
  })

  test("ports Sisyphus Junior notepad directive injection", async () => {
    expect(addSisyphusJuniorNotepadDirective("do work")).toContain("NOTEPAD PATH")
    expect(addSisyphusJuniorNotepadDirective(`${PLANNING_CONSULT_WARNING}do work`)).toBe(`${PLANNING_CONSULT_WARNING}do work`)

    const output = { args: { prompt: "implement" } }
    await createSisyphusJuniorNotepadHook({ isCallerOrchestrator: true })["tool.execute.before"]({ tool: "task" }, output)
    expect(output.args.prompt).toContain("SUBAGENT PLAN RESTRICTION")

    const ignored = { args: { prompt: "implement" } }
    await createSisyphusJuniorNotepadHook({ isCallerOrchestrator: false })["tool.execute.before"]({ tool: "task" }, ignored)
    expect(ignored.args.prompt).toBe("implement")

    const nonTask = { args: { prompt: "implement" } }
    await createSisyphusJuniorNotepadHook({ isCallerOrchestrator: true })["tool.execute.before"]({ tool: "bash" }, nonTask)
    expect(nonTask.args.prompt).toBe("implement")
  })
})
