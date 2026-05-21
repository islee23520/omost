import { processFilePathForAgentsInjection } from "@oh-my-opencode/agents-md-core"
import { parseApplyPatchRequests, runCommentChecker } from "@oh-my-opencode/comment-checker-core"
import { resolveModelWithFallback } from "@oh-my-opencode/model-core"
import { findRuleFiles, shouldApplyRule } from "@oh-my-opencode/rules-engine"
import { createUlwLoopEngine, runTrackedUlw } from "@oh-my-opencode/ulw-kernel"
import { replaceToolArgs } from "@oh-my-opencode/utils"
import { isAbsolute, relative, resolve } from "node:path"

export type OmoHookStatus = "behavior-mapped" | "adapter-bound" | "missing"
export type OmoHookExitPath = "pure-domain-port" | "adapter-bound" | "limited-redesign" | "explicit-exclusion" | "unclassified"
export type OmoHookWave = "phase-1-safety" | "phase-2-recovery" | "phase-3-orchestration" | "phase-4-host" | "phase-5-adapter-convergence"
export type OmoHookTestType = "unit" | "parity" | "adapter" | "integration" | "manual-qa"

export type OmoHookDefinition = {
  name: string
  originalExport: string
  domain: string
  status: OmoHookStatus
  standalonePackage?: string
  originalSource: string
  exitPath: OmoHookExitPath
  targetPackage: string
  wave: OmoHookWave
  testTypes: OmoHookTestType[]
  adapterImpact: "none" | "low" | "medium" | "high"
}

export type QuestionOption = {
  label: string
  description?: string
}

export type Question = {
  question: string
  header?: string
  options?: QuestionOption[]
  multiSelect?: boolean
}

export type AskUserQuestionArgs = {
  questions?: Question[]
}

export type ToolBeforeHookInput = {
  tool?: string
}

export type ToolBeforeHookOutput = {
  args: Record<string, unknown>
}

export type ChatMessageHookInput = {
  sessionID: string
  agent?: string
  model?: { providerID: string; modelID: string }
}

export type ChatMessageHookOutput = {
  message?: { agent?: string; variant?: string; [key: string]: unknown }
}

export type ModelAgentGuardDecision = {
  agent?: string
  outputAgent?: string
  variant?: string
  sessionAgent?: string
  toast?: { title: string; message: string; variant: "error" | "warning" }
}

export type ModelAgentGuardOptions = {
  sessionAgent?: string
  allowHephaestusNonGptModel?: boolean
}

export type AnthropicEffortInput = {
  sessionID: string
  agent?: { name?: string }
  model?: { providerID?: string; modelID?: string }
  message: { variant?: string }
}

export type AnthropicEffortOutput = {
  options: Record<string, unknown>
}

export type AnthropicEffortDecision = {
  effort?: string
  variant?: string
  reason?: "not-claude" | "unsupported-model" | "internal-agent" | "existing-effort" | "variant-not-max" | "injected" | "clamped-existing" | "clamped-variant"
}

export type AnthropicEffortOptions = {
  isConstrainedProvider?: (providerID: string) => boolean
}

export type MessagePart = { type: string; [key: string]: unknown }

export type MessageWithParts = {
  info: { role: string; [key: string]: unknown }
  parts: MessagePart[]
}

export type ToolAfterHookOutput = {
  title?: string
  output: string
  metadata?: unknown
}

export type TodoLike = { status: string }
export type AvailableSkillLike = { name: string; location?: string }
export type PathClassification = "icloud" | "onedrive" | "desktop-sync" | "network-drive" | "unknown"
export type FsyncSkipEntry = { filePath: string; errorCode: string; pathClassification: PathClassification }
export type ShellType = "posix" | "cmd" | "powershell"
export type AgentUsageState = { sessionID: string; agentUsed: boolean; reminderCount: number; updatedAt: number }
export type NotificationPlatform = "darwin" | "linux" | "win32" | "unsupported"
export type BackgroundNotificationManagerLike = { handleEvent: (event: { type: string; properties?: unknown }) => void; injectPendingNotificationsIntoChatMessage: (output: { parts: Array<{ type: string; text?: string; [key: string]: unknown }> }, sessionID: string) => void }
export type TeamParticipant = { role: "neither" } | { role: "lead"; teamRunId: string } | { role: "member"; teamRunId: string; memberName: string }
export type ThinkModeState = { requested: boolean; modelSwitched: boolean; variantSet: boolean; providerID?: string; modelID?: string }
export type ContextTokenInfo = { input: number; output?: number; reasoning?: number; cache?: { read?: number; write?: number } }
export type ExistingFileGuardArgs = { filePath?: string; path?: string; file_path?: string; overwrite?: boolean | string }
export type ImageDimensions = { width: number; height: number }
export type InteractiveBashSessionState = { sessionID: string; tmuxSessions: Set<string>; updatedAt: number }
export type KeywordType = "ultrawork" | "search" | "analyze" | "team" | "hyperplan" | "hyperplan-ultrawork"
export type DetectedKeyword = { type: KeywordType; message: string }
export type ParsedSlashCommand = { command: string; args: string; raw: string }
export type SlashCommandInfo = { name: string; scope: string; content?: string; description?: string; model?: string; agent?: string }
export type RecoveryErrorType = "tool_result_missing" | "thinking_block_order" | "thinking_disabled_violation" | "thinking_block_modified" | "assistant_prefill_unsupported" | "unavailable_tool" | null
export type ParsedTokenLimitError = { currentTokens: number; maxTokens: number; errorType: string; requestId?: string; messageIndex?: number }
export type IdleNotificationDecision = "scheduled" | "ignored-already-notified" | "ignored-pending" | "ignored-executing" | "cancelled-by-activity" | "deleted"
export type TodoSnapshot = { id?: string; content: string; status?: "pending" | "in_progress" | "completed" | "cancelled"; priority?: "low" | "medium" | "high" }
export type TailMonitorState = { currentMessageID?: string; currentHasOutput: boolean; consecutiveNoTextMessages: number; lastCompactedAt?: number; lastRecoveryAt?: number }
export type BackgroundTaskLike = { id: string; description: string; agent: string; status: string; sessionId?: string; isUnstableAgent?: boolean; model?: { modelID?: string } }
export type SessionNotificationMessage = { info?: { role?: string; error?: unknown }; parts?: Array<{ type?: string; text?: string }> }
export type RuntimeFallbackConfig = { retry_on_errors?: number[] }
export type ContinuationState = { stagnationCount: number; lastIncompleteCount?: number; awaitingPostInjectionProgressCheck?: boolean }
export type WorktreeEntry = { path: string; branch?: string; bare: boolean }
export type ParsedUserRequest = { planName: string | null; explicitWorktreePath: string | null }
export type TrackedTaskRef = { key: string; label: string; title: string }
export type PlanFormatValidationResult = { rawCount: number; parsedCount: number; warning?: string }

type HookOptions = {
  standalonePackage?: string
  sourceFile?: string
  originalSource?: string
}

export const QUESTION_LABEL_MAX_LENGTH = 30
export const AUTO_SLASH_COMMAND_TAG_OPEN = "<auto-slash-command>"
export const AUTO_SLASH_COMMAND_TAG_CLOSE = "</auto-slash-command>"

const KEYWORD_CODE_BLOCK_PATTERN = /```[\s\S]*?```/g
const KEYWORD_INLINE_CODE_PATTERN = /`[^`]+`/g
const SLASH_COMMAND_LEAD_PATTERN = /^\s*\/[a-zA-Z][\w-]*(?:\s|$)/
const SLASH_COMMAND_PATTERN = /^\/([a-zA-Z@][\w.:@/-]*)\s*(.*)/
const EXCLUDED_SLASH_COMMANDS = new Set(["ralph-loop", "cancel-ralph", "ulw-loop"])
const SEARCH_PATTERN = /\b(search|find|locate|lookup|look\s*up|explore|discover|scan|grep|query|browse|detect|trace|seek|track|pinpoint|hunt)\b|where\s+is|show\s+me|list\s+all|검색|찾아|탐색|조회|스캔|서치|뒤져|찾기|어디|추적|탐지|찾아봐|찾아내|보여줘|목록|検索|探して|見つけて|サーチ|探索|スキャン|どこ|発見|捜索|見つけ出す|一覧|搜索|查找|寻找|查询|检索|定位|扫描|发现|在哪里|找出来|列出|tìm kiếm|tra cứu|định vị|quét|phát hiện|truy tìm|tìm ra|ở đâu|liệt kê/i
const TEAM_PATTERN = /\bteam[\s_-]?mode\b|(?<![가-힣])(?:팀\s*모드|팀으로)/i
const HYPERPLAN_PATTERN = /\b(hyperplan|hpp)\b/i
const HYPERPLAN_ULTRAWORK_PATTERN = /\b(?:hpp|hyperplan)\s+(?:ulw|ultrawork)\b|\b(?:ulw|ultrawork)\s+(?:hpp|hyperplan)\b/i
const HASHLINE_NIBBLE_STR = "ZPMQVRWSNKTXJBYH"
const HASHLINE_DICT = Array.from({ length: 256 }, (_, index) => `${HASHLINE_NIBBLE_STR[index >>> 4]}${HASHLINE_NIBBLE_STR[index & 0x0f]}`)
const COLON_READ_LINE_PATTERN = /^\s*(\d+): ?(.*)$/
const PIPE_READ_LINE_PATTERN = /^\s*(\d+)\| ?(.*)$/
const OPENCODE_LINE_TRUNCATION_SUFFIX = "... (line truncated to 2000 chars)"
const WRITE_SUCCESS_MARKER = "File written successfully."
const TOKEN_LIMIT_PATTERNS = [/(\d+)\s*tokens?\s*>\s*(\d+)\s*maximum/i, /prompt.*?(\d+).*?tokens.*?exceeds.*?(\d+)/i, /(\d+).*?tokens.*?limit.*?(\d+)/i, /context.*?length.*?(\d+).*?maximum.*?(\d+)/i, /max.*?context.*?(\d+).*?but.*?(\d+)/i]
const TOKEN_LIMIT_KEYWORDS = ["prompt is too long", "is too long", "context_length_exceeded", "max_tokens", "token limit", "context length", "too many tokens", "non-empty content"]
const THINKING_BLOCK_ERROR_PATTERNS = [/thinking.*first block/i, /first block.*thinking/i, /must.*start.*thinking/i, /thinking.*redacted_thinking/i, /expected.*thinking.*found/i, /thinking.*disabled.*cannot.*contain/i]
const TEAM_MODE_STATUS_MARKER = '<team_mode_status enabled="true">'
export const THINKING_SUMMARY_MAX_CHARS = 500 as const
const RUNTIME_RETRYABLE_ERROR_PATTERNS = [/rate.?limit/i, /too.?many.?requests/i, /quota\s+will\s+reset\s+after/i, /quota.?exceeded/i, /service.?unavailable/i, /overloaded/i, /temporarily.?unavailable/i, /try.?again/i, /(?:^|\s)429(?:\s|$)/, /(?:^|\s)503(?:\s|$)/, /(?:^|\s)529(?:\s|$)/]
const PREEMPTIVE_COMPACTION_THRESHOLD = 0.78
const PREEMPTIVE_COMPACTION_COOLDOWN_MS = 60_000
const START_WORK_TEMPLATE_MARKER = "You are starting a Sisyphus work session."
const START_WORK_KEYWORD_PATTERN = /\b(ultrawork|ulw)\b/gi
const WORKTREE_FLAG_PATTERN = /--worktree(?:\s+(\S+))?/
const WRAPPING_QUOTES_PATTERN = /^(['"`])([\s\S]*)\1$/
const TASK_SECTION_HEADER_PATTERN = /^##\s*1\.\s*TASK\s*$/i
const TODO_TASK_LINE_PATTERN = /^(?:[-*]\s*\[\s*\]\s*)?(\d+)\.\s+(.+)$/
const FINAL_WAVE_TASK_LINE_PATTERN = /^(?:[-*]\s*\[\s*\]\s*)?(F\d+)\.\s+(.+)$/i
const ATLAS_SINGLE_TASK_DIRECTIVE = "Complete exactly one assigned task. Do not broaden scope."

const HOOKS: readonly OmoHookDefinition[] = [
  hook("todo-continuation-enforcer", "createTodoContinuationEnforcer", "loop", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("context-window-monitor", "createContextWindowMonitorHook", "context-window", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("session-notification", "createSessionNotification", "notification", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("session-notification-sender", "sendSessionNotification", "notification", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "session-notification-sender.ts" }),
  hook("session-notification-formatting", "buildWindowsToastScript", "notification", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "session-notification-formatting.ts" }),
  hook("session-todo-status", "hasIncompleteTodos", "todo", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "session-todo-status.ts" }),
  hook("session-notification-scheduler", "createIdleNotificationScheduler", "notification", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "session-notification-scheduler.ts" }),
  hook("session-recovery", "createSessionRecoveryHook", "recovery", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("comment-checker", "createCommentCheckerHooks", "quality", "behavior-mapped", { standalonePackage: "@oh-my-opencode/comment-checker-core" }),
  hook("tool-output-truncator", "createToolOutputTruncatorHook", "tool-output", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "tool-output-truncator.ts" }),
  hook("directory-agents-injector", "createDirectoryAgentsInjectorHook", "context", "behavior-mapped", { standalonePackage: "@oh-my-opencode/agents-md-core" }),
  hook("directory-readme-injector", "createDirectoryReadmeInjectorHook", "context", "behavior-mapped", { standalonePackage: "@oh-my-opencode/agents-md-core" }),
  hook("empty-task-response-detector", "createEmptyTaskResponseDetectorHook", "recovery", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "empty-task-response-detector.ts" }),
  hook("anthropic-context-window-limit-recovery", "createAnthropicContextWindowLimitRecoveryHook", "context-window", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("think-mode", "createThinkModeHook", "model", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("anthropic-effort", "createAnthropicEffortHook", "model", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("model-fallback", "createModelFallbackHook", "model", "behavior-mapped", { standalonePackage: "@oh-my-opencode/model-core" }),
  hook("claude-code-hooks", "createClaudeCodeHooksHook", "plugin-loader", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("rules-injector", "createRulesInjectorHook", "context", "behavior-mapped", { standalonePackage: "@oh-my-opencode/rules-engine" }),
  hook("background-notification", "createBackgroundNotificationHook", "notification", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("auto-update-checker", "createAutoUpdateCheckerHook", "maintenance", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("startup-toast", "showStartupToast", "maintenance", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", originalSource: "src/hooks/auto-update-checker/hook/startup-toasts.ts" }),
  hook("agent-usage-reminder", "createAgentUsageReminderHook", "prompting", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("keyword-detector", "createKeywordDetectorHook", "prompting", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("non-interactive-env", "createNonInteractiveEnvHook", "environment", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("interactive-bash-session", "createInteractiveBashSessionHook", "terminal", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("thinking-block-validator", "createThinkingBlockValidatorHook", "validation", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("team-mailbox-injector", "createTeamMailboxInjector", "team", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("team-mode-status-injector", "createTeamModeStatusInjector", "team", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("tool-pair-validator", "createToolPairValidatorHook", "validation", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("category-skill-reminder", "createCategorySkillReminderHook", "skills", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("ralph-loop", "createRalphLoopHook", "loop", "behavior-mapped", { standalonePackage: "@oh-my-opencode/ulw-kernel" }),
  hook("no-sisyphus-gpt", "createNoSisyphusGptHook", "model", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("no-hephaestus-non-gpt", "createNoHephaestusNonGptHook", "model", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("auto-slash-command", "createAutoSlashCommandHook", "commands", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("edit-error-recovery", "createEditErrorRecoveryHook", "recovery", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("prometheus-md-only", "createPrometheusMdOnlyHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("sisyphus-junior-notepad", "createSisyphusJuniorNotepadHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("task-resume-info", "createTaskResumeInfoHook", "task", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("start-work", "createStartWorkHook", "workflow", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("atlas", "createAtlasHook", "workflow", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("team-tool-gating", "createTeamToolGating", "team", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("delegate-task-retry", "createDelegateTaskRetryHook", "task", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("question-label-truncator", "createQuestionLabelTruncatorHook", "question", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("stop-continuation-guard", "createStopContinuationGuardHook", "loop", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("compaction-context-injector", "createCompactionContextInjector", "context-window", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("compaction-todo-preserver", "createCompactionTodoPreserverHook", "context-window", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("unstable-agent-babysitter", "createUnstableAgentBabysitterHook", "agent", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("preemptive-compaction", "createPreemptiveCompactionHook", "context-window", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "preemptive-compaction.ts" }),
  hook("tasks-todowrite-disabler", "createTasksTodowriteDisablerHook", "task", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("runtime-fallback", "createRuntimeFallbackHook", "runtime", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("write-existing-file-guard", "createWriteExistingFileGuardHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("bash-file-read-guard", "createBashFileReadGuardHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "../bash-file-read-guard.ts" }),
  hook("hashline-read-enhancer", "createHashlineReadEnhancerHook", "tool-output", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("json-error-recovery", "createJsonErrorRecoveryHook", "recovery", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("read-image-resizer", "createReadImageResizerHook", "tool-output", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("todo-description-override", "createTodoDescriptionOverrideHook", "todo", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("webfetch-redirect-guard", "createWebFetchRedirectGuardHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("legacy-plugin-toast", "createLegacyPluginToastHook", "notification", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("fsync-skip-warning", "createFsyncSkipWarningHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
  hook("notepad-write-guard", "createNotepadWriteGuardHook", "guard", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core", sourceFile: "index.ts" }),
  hook("plan-format-validator", "createPlanFormatValidatorHook", "workflow", "behavior-mapped", { standalonePackage: "@oh-my-opencode/hooks-core" }),
]

export const standaloneHookBehaviors = {
  commentChecker: { parseApplyPatchRequests, runCommentChecker },
  directoryContext: { processFilePathForAgentsInjection },
  rules: { findRuleFiles, shouldApplyRule },
  modelFallback: { resolveModelWithFallback },
  modelAgentGuard: { createModelAgentGuardHook, resolveModelAgentGuard, isGptModel, isGptNativeSisyphusModel, createThinkModeHook, resolveThinkMode, detectThinkKeyword, isAlreadyHighReasoningVariant, createAnthropicEffortHook, resolveAnthropicEffort, isClaudeProvider, isOpusModel, isEffortUnsupportedModel, shouldSkipForInternalAgent },
  thinkingBlockValidator: { createThinkingBlockValidatorHook, repairThinkingBlockMessages, hasSignedThinkingBlocksInHistory },
  toolGuards: { createBashFileReadGuardHook, isSimpleFileReadCommand, createWebFetchRedirectGuardHook, normalizeWebFetchRedirectOutput, buildWebFetchRedirectLimitMessage, createWriteExistingFileGuardHook, resolveWriteExistingFileGuard, isOmoWorkspacePath, isOverwriteEnabled },
  outputRecovery: { createEmptyTaskResponseDetectorHook, createJsonErrorRecoveryHook, createToolOutputTruncatorHook, createEditErrorRecoveryHook, recoverEmptyTaskOutput, recoverJsonErrorOutput, recoverEditErrorOutput, truncateToolOutput },
  promptDetectors: { createKeywordDetectorHook, detectKeywordsWithType, detectKeywords, removeKeywordCodeBlocks, looksLikeSlashCommand },
  slashCommands: { createAutoSlashCommandHook, parseSlashCommand, detectSlashCommand, findSlashCommandPartIndex, formatSlashCommandTemplate },
  continuation: { createTodoContinuationEnforcer, trackContinuationProgress, getTodoProgressSnapshot },
  todoAndTask: { hasIncompleteTodos, createTasksTodowriteDisablerHook, applyTodoDescriptionOverride, createNotepadWriteGuardHook, isNotepadPath, shouldBlockTaskTodoTool },
  taskRecovery: { createToolPairValidatorHook, repairMissingToolResults, createDelegateTaskRetryHook, addDelegateTaskRetryGuidance, createTaskResumeInfoHook, appendTaskResumeInfo, createStopContinuationGuardHook },
  hostGuards: { createNonInteractiveEnvHook, buildNonInteractiveGitCommand, detectBannedInteractiveCommand, createCategorySkillReminderHook, buildCategorySkillReminderMessage, formatFsyncSkipWarning, describePathClassification, createLegacyPluginToastDecisionHook, resolveLegacyPluginToastDecision, createPrometheusMdOnlyHook, isPrometheusAgent, isPrometheusAllowedFile, createSisyphusJuniorNotepadHook, addSisyphusJuniorNotepadDirective, createAgentUsageReminderHook, shouldRemindAgentUsage, isOrchestratorAgentForReminder },
  notifications: { escapeAppleScriptText, escapePowerShellSingleQuotedText, buildWindowsToastScript, getDefaultNotificationSoundPath, normalizeNotificationPlatform, createBackgroundNotificationHook, shouldForwardBackgroundEvent, createSessionNotification, buildReadyNotificationContent, extractSessionNotificationText, findLastSessionNotificationMessage },
  notificationScheduler: { createIdleNotificationScheduler, createIdleNotificationState },
  team: { createTeamToolGating, resolveTeamToolGate, isUniversalTeamTool, createTeamMailboxInjector, injectTeamMailboxMessage, buildTeamMailboxTurnMarker, createTeamModeStatusInjector, injectTeamModeStatus, buildTeamModeStatusContent },
  contextWindow: { createContextWindowMonitorHook, buildContextWindowReminder, appendContextWindowStatus, shouldWarnContextWindow, parseAnthropicTokenLimitError, formatBytes, isTokenLimitErrorText, createCompactionContextInjector, buildCompactionContextPrompt, createTailMonitorState, finalizeTrackedAssistantMessage, shouldTreatAssistantPartAsOutput, trackAssistantOutput, extractTodos, hasDetailedTodos, isAtlasBootstrapTodoList, shouldRestoreOverCurrentTodos, replaceAtlasBootstrapTodos, shouldRunPreemptiveCompaction, buildPreemptiveCompactionFailureToast },
  sessionRecovery: { createSessionRecoveryHook, detectErrorType, extractMessageIndex, extractUnavailableToolName },
  unstableAgent: { getMessageInfo, getMessageParts, extractMessages, isUnstableTask, buildUnstableAgentReminder },
  runtimeFallback: { createRuntimeFallbackHook, getRuntimeFallbackErrorMessage, extractRuntimeFallbackStatusCode, classifyRuntimeFallbackErrorType, containsRuntimeFallbackErrorContent, isRuntimeFallbackRetryableError },
  claudeCodeHooks: { createClaudeCodeHooksHook, listClaudeCodeHookNames },
  autoUpdate: { isPrereleaseVersion, isDistTag, isPrereleaseOrDistTag, extractChannel, createAutoUpdateCheckerHook, shouldShowAutoUpdateToast },
  workflow: { createStartWorkHook, parseUserRequest, parseWorktreeListPorcelain, resolveStartWorkTemplate, createAtlasHook, parseTrackedTaskFromPrompt, buildAtlasSingleTaskPrompt, shouldWarnAtlasDirectModification, resolveAtlasPendingTaskRef },
  planFormat: { createPlanFormatValidatorHook, validatePlanFormat, countRawTopLevelPlanCheckboxes, buildPlanFormatWarning, isPlanFilePath },
  terminal: { parseTmuxCommand, buildInteractiveBashSessionReminder, createInteractiveBashSessionHook, isOmoTmuxSession },
  imageResizer: { calculateTargetDimensions, calculateImageTokens, formatImageResizeAppendix },
  hashline: { createHashlineReadEnhancerHook, transformHashlineReadOutput, formatHashLine, computeLineHash, buildHashlineWriteSuccessOutput },
  ralphLoop: { createUlwLoopEngine, runTrackedUlw },
  questionLabelTruncator: { createQuestionLabelTruncatorHook, truncateQuestionLabels, truncateQuestionLabel },
} as const

export function getStandaloneHookBehavior(name: string): unknown {
  if (name === "comment-checker") return standaloneHookBehaviors.commentChecker
  if (name === "directory-agents-injector" || name === "directory-readme-injector") return standaloneHookBehaviors.directoryContext
  if (name === "rules-injector") return standaloneHookBehaviors.rules
  if (name === "model-fallback") return standaloneHookBehaviors.modelFallback
  if (name === "no-sisyphus-gpt" || name === "no-hephaestus-non-gpt" || name === "think-mode" || name === "anthropic-effort") return standaloneHookBehaviors.modelAgentGuard
  if (name === "thinking-block-validator") return standaloneHookBehaviors.thinkingBlockValidator
  if (name === "bash-file-read-guard" || name === "webfetch-redirect-guard" || name === "write-existing-file-guard") return standaloneHookBehaviors.toolGuards
  if (name === "empty-task-response-detector" || name === "json-error-recovery" || name === "tool-output-truncator" || name === "edit-error-recovery") return standaloneHookBehaviors.outputRecovery
  if (name === "keyword-detector") return standaloneHookBehaviors.promptDetectors
  if (name === "auto-slash-command") return standaloneHookBehaviors.slashCommands
  if (name === "todo-continuation-enforcer") return standaloneHookBehaviors.continuation
  if (name === "session-todo-status" || name === "tasks-todowrite-disabler" || name === "todo-description-override" || name === "notepad-write-guard") return standaloneHookBehaviors.todoAndTask
  if (name === "tool-pair-validator" || name === "delegate-task-retry" || name === "task-resume-info" || name === "stop-continuation-guard") return standaloneHookBehaviors.taskRecovery
  if (name === "non-interactive-env" || name === "category-skill-reminder" || name === "fsync-skip-warning" || name === "legacy-plugin-toast" || name === "prometheus-md-only" || name === "sisyphus-junior-notepad" || name === "agent-usage-reminder") return standaloneHookBehaviors.hostGuards
  if (name === "session-notification-formatting" || name === "session-notification-sender" || name === "background-notification" || name === "session-notification") return standaloneHookBehaviors.notifications
  if (name === "session-notification-scheduler") return standaloneHookBehaviors.notificationScheduler
  if (name === "session-recovery") return standaloneHookBehaviors.sessionRecovery
  if (name === "team-tool-gating" || name === "team-mailbox-injector" || name === "team-mode-status-injector") return standaloneHookBehaviors.team
  if (name === "context-window-monitor" || name === "anthropic-context-window-limit-recovery" || name === "compaction-context-injector" || name === "compaction-todo-preserver") return standaloneHookBehaviors.contextWindow
  if (name === "unstable-agent-babysitter") return standaloneHookBehaviors.unstableAgent
  if (name === "runtime-fallback") return standaloneHookBehaviors.runtimeFallback
  if (name === "claude-code-hooks") return standaloneHookBehaviors.claudeCodeHooks
  if (name === "auto-update-checker" || name === "startup-toast") return standaloneHookBehaviors.autoUpdate
  if (name === "start-work" || name === "atlas") return standaloneHookBehaviors.workflow
  if (name === "plan-format-validator") return standaloneHookBehaviors.planFormat
  if (name === "interactive-bash-session") return standaloneHookBehaviors.terminal
  if (name === "read-image-resizer") return standaloneHookBehaviors.imageResizer
  if (name === "hashline-read-enhancer") return standaloneHookBehaviors.hashline
  if (name === "ralph-loop") return standaloneHookBehaviors.ralphLoop
  if (name === "question-label-truncator") return standaloneHookBehaviors.questionLabelTruncator
  return undefined
}

export function truncateQuestionLabel(label: string, maxLength: number = QUESTION_LABEL_MAX_LENGTH): string {
  if (label.length <= maxLength) return label
  return `${label.slice(0, maxLength - 3)}...`
}

export function truncateQuestionLabels(args: AskUserQuestionArgs): AskUserQuestionArgs {
  if (!Array.isArray(args.questions)) return args
  return {
    ...args,
    questions: args.questions.map((question) => ({
      ...question,
      options: question.options?.map((option) => ({
        ...option,
        label: truncateQuestionLabel(option.label),
      })) ?? [],
    })),
  }
}

export function createQuestionLabelTruncatorHook() {
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput, output: ToolBeforeHookOutput): Promise<void> => {
      const toolName = input.tool?.toLowerCase()
      if (toolName !== "askuserquestion" && toolName !== "ask_user_question") return
      if (!hasQuestions(output.args)) return
      replaceToolArgs(output, { questions: truncateQuestionLabels(output.args).questions })
    },
  }
}

export function createModelAgentGuardHook(options: ModelAgentGuardOptions = {}) {
  return {
    "chat.message": async (input: ChatMessageHookInput, output?: ChatMessageHookOutput): Promise<ModelAgentGuardDecision | undefined> => {
      const decision = resolveModelAgentGuard(input.agent ?? options.sessionAgent, input.model, options)
      if (decision.agent !== undefined) input.agent = decision.agent
      if (decision.outputAgent !== undefined && output?.message) output.message.agent = decision.outputAgent
      if (decision.variant !== undefined && output?.message && output.message.variant === undefined) output.message.variant = decision.variant
      return decision
    },
  }
}

const THINK_KEYWORDS = ["생각", "검토", "제대로", "思考", "考虑", "考慮", "考え", "熟考", "सोच", "विचार", "تفكير", "تأمل", "চিন্তা", "ভাবনা", "думать", "думай", "размышлять", "размышляй", "pensar", "pense", "refletir", "reflita", "piensa", "reflexionar", "reflexiona", "penser", "pense", "réfléchir", "réfléchis", "denken", "denk", "nachdenken", "suy nghĩ", "cân nhắc", "düşün", "düşünmek", "pensare", "pensa", "riflettere", "rifletti", "คิด", "พิจารณา", "myśl", "myśleć", "zastanów", "nadenken", "berpikir", "pikir", "pertimbangkan", "думати", "думай", "роздумувати", "σκέψου", "σκέφτομαι", "myslet", "mysli", "přemýšlet", "gândește", "gândi", "reflectă", "tänka", "tänk", "fundera", "gondolkodj", "gondolkodni", "ajattele", "ajatella", "pohdi", "tænk", "tænke", "overvej", "tenk", "tenke", "gruble", "חשוב", "לחשוב", "להרהר", "fikir", "berfikir"]
const CODE_BLOCK_PATTERN = /```[\s\S]*?```/g
const INLINE_CODE_PATTERN = /`[^`]+`/g

export function detectThinkKeyword(text: string): boolean {
  const textWithoutCode = text.replace(CODE_BLOCK_PATTERN, "").replace(INLINE_CODE_PATTERN, "")
  return /\b(?:ultrathink|think)\b/i.test(textWithoutCode) || THINK_KEYWORDS.some((keyword) => textWithoutCode.toLowerCase().includes(keyword.toLowerCase()))
}

export function isAlreadyHighReasoningVariant(modelID: string): boolean {
  const normalized = modelID.replace(/(gpt-\d+)\.(\d+)/g, "$1-$2")
  const base = normalized.includes("/") ? (normalized.split("/").pop() ?? normalized) : normalized
  return base.endsWith("-high")
}

export function resolveThinkMode(parts: Array<{ type: string; text?: string }>, model: ChatMessageHookInput["model"], message: Record<string, unknown>): { state: ThinkModeState; variant?: string } {
  const promptText = parts.filter((part) => part.type === "text").map((part) => part.text ?? "").join("")
  const state: ThinkModeState = { requested: false, modelSwitched: false, variantSet: false }
  if (!detectThinkKeyword(promptText)) return { state }
  state.requested = true
  if (typeof message.variant === "string" || !model) return { state }
  state.providerID = model.providerID
  state.modelID = model.modelID
  if (isAlreadyHighReasoningVariant(model.modelID)) return { state }
  state.variantSet = true
  return { state, variant: "high" }
}

export function createThinkModeHook() {
  const states = new Map<string, ThinkModeState>()
  return {
    getState: (sessionID: string): ThinkModeState | undefined => states.get(sessionID),
    clear: (sessionID: string): void => { states.delete(sessionID) },
    "chat.message": async (input: ChatMessageHookInput, output: { message: Record<string, unknown>; parts: Array<{ type: string; text?: string; [key: string]: unknown }> }): Promise<void> => {
      const result = resolveThinkMode(output.parts, input.model, output.message)
      if (result.variant) output.message.variant = result.variant
      states.set(input.sessionID, result.state)
    },
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type !== "session.deleted") return
      const sessionID = extractSessionId(event.properties)
      if (sessionID) states.delete(sessionID)
    },
  }
}

export function resolveModelAgentGuard(agent: string | undefined, model: ChatMessageHookInput["model"], options: ModelAgentGuardOptions = {}): ModelAgentGuardDecision {
  const agentKey = getAgentConfigKey(agent ?? "")
  const modelID = model?.modelID
  if (agentKey === "sisyphus" && modelID && isGptNativeSisyphusModel(modelID)) {
    return { variant: getNativeSisyphusGptVariant(model) }
  }
  if (agentKey === "sisyphus" && modelID && isGptModel(modelID)) {
    return {
      agent: "hephaestus",
      outputAgent: "hephaestus",
      sessionAgent: "hephaestus",
      toast: {
        title: "NEVER Use Sisyphus with GPT",
        message: "Sisyphus works best with Claude Opus, and works fine with Kimi/GLM models.\nDo NOT use Sisyphus with GPT (except GPT-5.4 and GPT-5.5 which have specialized support).\nFor other GPT models, always use Hephaestus.",
        variant: "error",
      },
    }
  }
  if (agentKey === "hephaestus" && modelID && !isGptModel(modelID)) {
    const allowed = options.allowHephaestusNonGptModel === true
    return {
      agent: allowed ? undefined : "sisyphus",
      outputAgent: allowed ? undefined : "sisyphus",
      sessionAgent: allowed ? undefined : "sisyphus",
      toast: {
        title: "NEVER Use Hephaestus with Non-GPT",
        message: "Hephaestus is designed exclusively for GPT models.\nHephaestus is trash without GPT.\nFor Claude/Kimi/GLM models, always use Sisyphus.",
        variant: allowed ? "warning" : "error",
      },
    }
  }
  return {}
}

const ANTHROPIC_OPUS_PATTERN = /claude-.*opus/i
const ANTHROPIC_EFFORT_UNSUPPORTED_PATTERN = /claude-.*haiku/i
const ANTHROPIC_INTERNAL_SKIP_AGENTS = new Set(["title", "summary", "compaction"])

export function isClaudeProvider(providerID: string, modelID: string): boolean {
  if (["anthropic", "google-vertex-anthropic", "opencode"].includes(providerID)) return true
  return providerID === "github-copilot" && modelID.toLowerCase().includes("claude")
}

export function isOpusModel(modelID: string): boolean {
  return ANTHROPIC_OPUS_PATTERN.test(normalizeAnthropicModelID(modelID))
}

export function isEffortUnsupportedModel(modelID: string): boolean {
  return ANTHROPIC_EFFORT_UNSUPPORTED_PATTERN.test(normalizeAnthropicModelID(modelID))
}

export function shouldSkipForInternalAgent(agentName: string | undefined): boolean {
  return agentName ? ANTHROPIC_INTERNAL_SKIP_AGENTS.has(agentName.trim().toLowerCase()) : false
}

export function resolveAnthropicEffort(input: AnthropicEffortInput, output: AnthropicEffortOutput, options: AnthropicEffortOptions = {}): AnthropicEffortDecision {
  const providerID = input.model?.providerID
  const modelID = input.model?.modelID
  if (!providerID || !modelID || !isClaudeProvider(providerID, modelID)) return { reason: "not-claude" }
  if (isEffortUnsupportedModel(modelID)) return { reason: "unsupported-model" }
  if (shouldSkipForInternalAgent(input.agent?.name)) return { reason: "internal-agent" }

  const opus = isOpusModel(modelID)
  const constrained = providerID === "github-copilot" || options.isConstrainedProvider?.(providerID) === true
  if (output.options.effort !== undefined) {
    if (output.options.effort === "max" && constrained) return { effort: "high", variant: "high", reason: "clamped-existing" }
    return { effort: String(output.options.effort), reason: "existing-effort" }
  }
  if (input.message.variant !== "max") return { reason: "variant-not-max" }
  const effort = opus && !constrained ? "max" : "high"
  return { effort, variant: effort === "max" ? undefined : effort, reason: effort === "max" ? "injected" : "clamped-variant" }
}

export function createAnthropicEffortHook(options: AnthropicEffortOptions = {}) {
  return {
    "chat.params": async (input: AnthropicEffortInput, output: AnthropicEffortOutput): Promise<AnthropicEffortDecision> => {
      const decision = resolveAnthropicEffort(input, output, options)
      if (decision.effort !== undefined) output.options.effort = decision.effort
      if (decision.variant !== undefined) input.message.variant = decision.variant
      return decision
    },
  }
}

export function isGptModel(model: string): boolean {
  return extractModelName(model).toLowerCase().includes("gpt")
}

export function isGptNativeSisyphusModel(model: string): boolean {
  return /gpt-5[.-](?:[4-9]|\d{2,})/i.test(extractModelName(model).toLowerCase())
}

export function createThinkingBlockValidatorHook() {
  return {
    "experimental.chat.messages.transform": async (_input: Record<string, never>, output: { messages: MessageWithParts[] }): Promise<void> => {
      repairThinkingBlockMessages(output.messages)
    },
  }
}

export function repairThinkingBlockMessages(messages: MessageWithParts[]): void {
  if (messages.length === 0 || !hasSignedThinkingBlocksInHistory(messages)) return
  for (let index = 0; index < messages.length; index++) {
    const message = messages[index]
    if (message.info.role !== "assistant") continue
    if (hasContentParts(message.parts) && !startsWithThinkingBlock(message.parts)) {
      const thinkingPart = findPreviousThinkingPart(messages, index)
      if (thinkingPart) message.parts.unshift(thinkingPart)
    }
  }
}

export function hasSignedThinkingBlocksInHistory(messages: MessageWithParts[]): boolean {
  return messages.some((message) => message.info.role === "assistant" && message.parts.some(isSignedThinkingPart))
}

export const BASH_FILE_READ_WARNING_MESSAGE = "Prefer the Read tool over `cat`/`head`/`tail` for reading file contents. The Read tool provides line numbers and hash anchors for precise editing."

const FILE_READ_PATTERNS = [
  /^\s*cat\s+(?!-)[^\s|&;]+\s*$/,
  /^\s*head\s+(-n\s+\d+\s+)?(?!-)[^\s|&;]+\s*$/,
  /^\s*tail\s+(-n\s+\d+\s+)?(?!-)[^\s|&;]+\s*$/,
]

export function isSimpleFileReadCommand(command: string): boolean {
  return FILE_READ_PATTERNS.some((pattern) => pattern.test(command))
}

export function createBashFileReadGuardHook() {
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput, output: ToolBeforeHookOutput & { message?: string }): Promise<void> => {
      if (input.tool?.toLowerCase() !== "bash") return
      const command = output.args.command
      if (typeof command === "string" && isSimpleFileReadCommand(command)) output.message = BASH_FILE_READ_WARNING_MESSAGE
    },
  }
}

export const MAX_WEBFETCH_REDIRECTS = 10
const WEBFETCH_REDIRECT_ERROR_PATTERNS = [/redirect/i, /too many redirects/i, /maximum redirects/i]

export function buildWebFetchRedirectLimitMessage(url?: string): string {
  const suffix = url ? ` for ${url}` : ""
  return `Error: WebFetch failed: exceeded maximum redirects (${MAX_WEBFETCH_REDIRECTS})${suffix}`
}

export function normalizeWebFetchRedirectOutput(output: string, originalUrl?: string): string {
  const isToolError = output.trimStart().toLowerCase().startsWith("error:")
  const isRedirectLoop = WEBFETCH_REDIRECT_ERROR_PATTERNS.some((pattern) => pattern.test(output))
  return isToolError && isRedirectLoop ? buildWebFetchRedirectLimitMessage(originalUrl) : output
}

export function createWebFetchRedirectGuardHook() {
  const pendingFailures = new Map<string, string>()
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput & { sessionID?: string; callID?: string }, output: ToolBeforeHookOutput): Promise<void> => {
      if (input.tool?.toLowerCase() !== "webfetch") return
      const url = typeof output.args.url === "string" ? output.args.url : undefined
      if (url && input.sessionID && input.callID && output.args.redirectFailed === true) pendingFailures.set(`${input.sessionID}:${input.callID}`, url)
    },
    "tool.execute.after": async (input: ToolBeforeHookInput & { sessionID?: string; callID?: string }, output: ToolAfterHookOutput): Promise<void> => {
      if (input.tool?.toLowerCase() !== "webfetch") return
      const key = input.sessionID && input.callID ? `${input.sessionID}:${input.callID}` : undefined
      const pendingUrl = key ? pendingFailures.get(key) : undefined
      if (key) pendingFailures.delete(key)
      output.output = pendingUrl ? buildWebFetchRedirectLimitMessage(pendingUrl) : normalizeWebFetchRedirectOutput(output.output)
    },
  }
}

export type WriteExistingFileGuardDecision = "allow" | "register-read" | "block"

export function isOverwriteEnabled(value: boolean | string | undefined): boolean {
  return value === true || (typeof value === "string" && value.toLowerCase() === "true")
}

export function isOmoWorkspacePath(filePath: string): boolean {
  return /(^|[/\\])\.omo([/\\]|$)/.test(filePath)
}

function getPathFromExistingFileGuardArgs(args: ExistingFileGuardArgs | undefined): string | undefined {
  return args?.filePath ?? args?.path ?? args?.file_path
}

export function resolveWriteExistingFileGuard(input: { tool?: string; sessionID?: string }, args: ExistingFileGuardArgs | undefined, options: { exists: (filePath: string) => boolean; readPermissions: Set<string> }): WriteExistingFileGuardDecision {
  const toolName = input.tool?.toLowerCase()
  if (toolName !== "write" && toolName !== "read") return "allow"
  const filePath = getPathFromExistingFileGuardArgs(args)
  if (!filePath) return "allow"
  if (toolName === "read") {
    if (input.sessionID && options.exists(filePath)) {
      options.readPermissions.add(filePath)
      return "register-read"
    }
    return "allow"
  }
  const overwriteEnabled = isOverwriteEnabled(args?.overwrite)
  if (!options.exists(filePath) || isOmoWorkspacePath(filePath) || overwriteEnabled) return "allow"
  if (input.sessionID && options.readPermissions.delete(filePath)) return "allow"
  return "block"
}

export function createWriteExistingFileGuardHook(options: { exists: (filePath: string) => boolean }) {
  const readPermissionsBySession = new Map<string, Set<string>>()
  const getReadPermissions = (sessionID: string): Set<string> => {
    const existing = readPermissionsBySession.get(sessionID)
    if (existing) return existing
    const created = new Set<string>()
    readPermissionsBySession.set(sessionID, created)
    return created
  }
  return {
    getReadPermissions,
    "tool.execute.before": async (input: { tool?: string; sessionID?: string }, output: ToolBeforeHookOutput): Promise<void> => {
      const sessionID = input.sessionID ?? ""
      const args = output.args as ExistingFileGuardArgs
      const decision = resolveWriteExistingFileGuard(input, args, { exists: options.exists, readPermissions: getReadPermissions(sessionID) })
      if ("overwrite" in output.args) delete output.args.overwrite
      if (decision === "block") throw new Error("File already exists. Use edit tool instead.")
    },
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type !== "session.deleted") return
      const sessionID = extractSessionId(event.properties)
      if (sessionID) readPermissionsBySession.delete(sessionID)
    },
  }
}

export const EMPTY_TASK_RESPONSE_WARNING = `[Task Empty Response Warning]

Task invocation completed but returned no response. This indicates the agent either:
- Failed to execute properly
- Did not terminate correctly
- Returned an empty result

Note: The call has already completed - you are NOT waiting for a response. Proceed accordingly.`

export function recoverEmptyTaskOutput(tool: string, output: string): string {
  return (tool === "Task" || tool === "task") && output.trim() === "" ? EMPTY_TASK_RESPONSE_WARNING : output
}

export function createEmptyTaskResponseDetectorHook() {
  return {
    "tool.execute.after": async (input: ToolBeforeHookInput, output: ToolAfterHookOutput): Promise<void> => {
      output.output = recoverEmptyTaskOutput(input.tool ?? "", output.output)
    },
  }
}

export const JSON_ERROR_TOOL_EXCLUDE_LIST = ["bash", "read", "glob", "grep", "webfetch", "look_at", "grep_app_searchgithub", "websearch_web_search_exa", "todowrite", "todoread"] as const
export const JSON_ERROR_REMINDER_MARKER = "[JSON PARSE ERROR - IMMEDIATE ACTION REQUIRED]"
export const JSON_ERROR_REMINDER = `
[JSON PARSE ERROR - IMMEDIATE ACTION REQUIRED]

You sent invalid JSON arguments. The system could not parse your tool call.
STOP and do this NOW:

1. LOOK at the error message above to see what was expected vs what you sent.
2. CORRECT your JSON syntax (missing braces, unescaped quotes, trailing commas, etc).
3. RETRY the tool call with valid JSON.

DO NOT repeat the exact same invalid call.
`

const JSON_ERROR_EXCLUDED_TOOLS = new Set<string>(JSON_ERROR_TOOL_EXCLUDE_LIST)
const JSON_ERROR_PATTERNS = [/json parse error/i, /failed to parse json/i, /invalid json/i, /malformed json/i, /unexpected end of json input/i, /syntaxerror:\s*unexpected token.*json/i, /json[^\n]*expected '\}'/i, /json[^\n]*unexpected eof/i]

export function recoverJsonErrorOutput(tool: string, output: string): string {
  if (JSON_ERROR_EXCLUDED_TOOLS.has(tool.toLowerCase()) || output.includes(JSON_ERROR_REMINDER_MARKER)) return output
  return JSON_ERROR_PATTERNS.some((pattern) => pattern.test(output)) ? `${output}\n${JSON_ERROR_REMINDER}` : output
}

export function createJsonErrorRecoveryHook() {
  return {
    "tool.execute.after": async (input: ToolBeforeHookInput, output: ToolAfterHookOutput): Promise<void> => {
      output.output = recoverJsonErrorOutput(input.tool ?? "", output.output)
    },
  }
}

export const EDIT_ERROR_PATTERNS = ["oldString and newString must be different", "oldString not found", "oldString found multiple times"] as const
export const EDIT_ERROR_REMINDER = `
[EDIT ERROR - IMMEDIATE ACTION REQUIRED]

You made an Edit mistake. STOP and do this NOW:

1. READ the file immediately to see its ACTUAL current state
2. VERIFY what the content really looks like (your assumption was wrong)
3. APOLOGIZE briefly to the user for the error
4. CONTINUE with corrected action based on the real file content

DO NOT attempt another edit until you've read and verified the file state.
`

export function recoverEditErrorOutput(tool: string, output: string): string {
  if (tool.toLowerCase() !== "edit") return output
  const lowered = output.toLowerCase()
  return EDIT_ERROR_PATTERNS.some((pattern) => lowered.includes(pattern.toLowerCase())) ? `${output}\n${EDIT_ERROR_REMINDER}` : output
}

export function createEditErrorRecoveryHook() {
  return {
    "tool.execute.after": async (input: ToolBeforeHookInput, output: ToolAfterHookOutput): Promise<void> => {
      output.output = recoverEditErrorOutput(input.tool ?? "", output.output)
    },
  }
}

const TRUNCATABLE_TOOLS = new Set(["grep", "Grep", "safe_grep", "glob", "Glob", "safe_glob", "lsp_diagnostics", "ast_grep_search", "interactive_bash", "Interactive_bash", "skill_mcp", "webfetch", "WebFetch"])
const DEFAULT_MAX_TOKENS = 50_000
const WEBFETCH_MAX_TOKENS = 10_000

export function truncateToolOutput(tool: string, output: string, options: { truncateAll?: boolean; maxTokens?: number } = {}): { output: string; truncated: boolean } {
  if (!options.truncateAll && !TRUNCATABLE_TOOLS.has(tool)) return { output, truncated: false }
  const maxTokens = options.maxTokens ?? (tool === "webfetch" || tool === "WebFetch" ? WEBFETCH_MAX_TOKENS : DEFAULT_MAX_TOKENS)
  const maxCharacters = maxTokens * 4
  if (output.length <= maxCharacters) return { output, truncated: false }
  return { output: `${output.slice(0, maxCharacters)}\n\n[Tool output truncated to ${maxTokens} tokens]`, truncated: true }
}

export function createToolOutputTruncatorHook(options: { truncateAll?: boolean; maxTokens?: number } = {}) {
  return {
    "tool.execute.after": async (input: ToolBeforeHookInput, output: ToolAfterHookOutput): Promise<void> => {
      const result = truncateToolOutput(input.tool ?? "", output.output, options)
      if (result.truncated) output.output = result.output
    },
  }
}

export function hasIncompleteTodos(todos: readonly TodoLike[]): boolean {
  return todos.some((todo) => todo.status !== "completed" && todo.status !== "cancelled")
}

export const TASK_TODOWRITE_BLOCKED_TOOLS = ["TodoWrite", "TodoRead"] as const
export const TASK_TODOWRITE_REPLACEMENT_MESSAGE = `TodoRead/TodoWrite are DISABLED because experimental.task_system is enabled.

**ACTION REQUIRED**: RE-REGISTER what you were about to write as Todo using Task tools NOW. Then ASSIGN yourself and START WORKING immediately.

**Use these tools instead:**
- TaskCreate: Create new task with auto-generated ID
- TaskUpdate: Update status, assign owner, add dependencies
- TaskList: List active tasks with dependency info
- TaskGet: Get full task details

**Workflow:**
1. TaskCreate({ subject: "your task description" })
2. TaskUpdate({ id: "T-xxx", status: "in_progress", owner: "your-thread-id" })
3. DO THE WORK
4. TaskUpdate({ id: "T-xxx", status: "completed" })

CRITICAL: 1 task = 1 task. Fire independent tasks concurrently.

**STOP! DO NOT START WORKING DIRECTLY - NO MATTER HOW SMALL THE TASK!**
Even if the task seems trivial (1 line fix, simple edit, quick change), you MUST:
1. FIRST register it with TaskCreate
2. THEN mark it in_progress
3. ONLY THEN do the actual work
4. FINALLY mark it completed

**WHY?** Task tracking = visibility = accountability. Skipping registration = invisible work = chaos.

DO NOT retry TodoWrite. Convert to TaskCreate NOW.`

export function shouldBlockTaskTodoTool(tool: string, taskSystemEnabled: boolean): boolean {
  return taskSystemEnabled && TASK_TODOWRITE_BLOCKED_TOOLS.some((blocked) => blocked.toLowerCase() === tool.toLowerCase())
}

export function createTasksTodowriteDisablerHook(config: { experimental?: { task_system?: boolean } } = {}) {
  const taskSystemEnabled = config.experimental?.task_system === true
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput): Promise<void> => {
      if (input.tool && shouldBlockTaskTodoTool(input.tool, taskSystemEnabled)) throw new Error(TASK_TODOWRITE_REPLACEMENT_MESSAGE)
    },
  }
}

export const TODOWRITE_DESCRIPTION = `Use this tool to create and manage a structured task list for tracking progress on multi-step work.

## OpenCode Schema Contract

The upstream OpenCode \`todowrite\` schema expects each todo item to include:

- \`content\`: string
- \`status\`: string, one of \`pending\`, \`in_progress\`, \`completed\`, \`cancelled\`
- \`priority\`: string, one of \`high\`, \`medium\`, \`low\`

\`priority\` is a string field. Never send numeric priorities such as \`0\`, \`1\`, \`2\`, or labels such as \`P0\`, \`P1\`, \`P2\`.

## Todo Format (MANDATORY)

Each todo title MUST encode four elements: WHERE, WHY, HOW, and EXPECTED RESULT.

Format: "[WHERE] [HOW] to [WHY] - expect [RESULT]"`

export async function applyTodoDescriptionOverride(input: { toolID: string }, output: { description: string; parameters: unknown }): Promise<void> {
  if (input.toolID === "todowrite") output.description = TODOWRITE_DESCRIPTION
}

export function isNotepadPath(filePath: string): boolean {
  return filePath.includes("/.sisyphus/notepads/") || filePath.startsWith(".sisyphus/notepads/")
}

export function createNotepadWriteGuardHook() {
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput, output: ToolBeforeHookOutput): Promise<void> => {
      if (input.tool?.toLowerCase() !== "write") return
      const filePath = getWritePath(output.args)
      if (filePath && isNotepadPath(filePath)) throw new Error(`Refused: Write to ${filePath} is blocked because notepad files are append-only and Write would destroy history. Report the original Edit failure to the user and ask for guidance instead.`)
    },
  }
}

function getWritePath(args: Record<string, unknown>): string | undefined {
  const raw = args.filePath ?? args.path ?? args.file_path
  return typeof raw === "string" ? raw : undefined
}

function getToolUseId(part: MessagePart): string | undefined {
  if (part.type === "tool_use" && typeof part.id === "string" && part.id.length > 0) return part.id
  if (part.type === "tool" && typeof part.callID === "string" && part.callID.length > 0) return part.callID
  return undefined
}

function getToolResultId(part: MessagePart): string | undefined {
  if (part.type !== "tool_result") return undefined
  if (typeof part.toolUseId === "string" && part.toolUseId.length > 0) return part.toolUseId
  if (typeof part.tool_use_id === "string" && part.tool_use_id.length > 0) return part.tool_use_id
  return undefined
}

function extractUniqueToolUseIds(parts: MessagePart[]): string[] {
  const seen = new Set<string>()
  const ids: string[] = []
  for (const part of parts) {
    const id = getToolUseId(part)
    if (!id || seen.has(id)) continue
    seen.add(id)
    ids.push(id)
  }
  return ids
}

function createToolResultPart(toolUseId: string): MessagePart {
  return { type: "tool_result", toolUseId, tool_use_id: toolUseId, isError: true, content: [{ type: "text", text: TOOL_RESULT_PLACEHOLDER }] }
}

function findToolResultInsertIndex(parts: MessagePart[]): number {
  let last = -1
  for (let index = 0; index < parts.length; index++) if (getToolResultId(parts[index])) last = index
  return last === -1 ? 0 : last + 1
}

function buildDelegateTaskRetryGuidance(error: DelegateTaskErrorInfo): string {
  const pattern = DELEGATE_TASK_ERROR_PATTERNS.find((candidate) => candidate.errorType === error.errorType)
  const available = error.originalOutput.match(/Available[^:]*:\s*(.+)$/m)?.[1]?.trim()
  return `
 [task CALL FAILED - IMMEDIATE RETRY REQUIRED]
 
 **Error Type**: ${error.errorType}
 **Fix**: ${pattern?.fixHint ?? "Fix the error and retry with correct parameters."}
 ${available ? `\n**Available Options**: ${available}\n` : ""}
 **Action**: Retry task NOW with corrected parameters.
 
 Example of CORRECT call:
 \`\`\`
 task(
   description="Task description",
   prompt="Detailed prompt...",
   category="unspecified-low",  // OR subagent_type="explore"
   run_in_background=false,
   load_skills=[]
 )
 \`\`\`
 `
}

function extractTaskId(metadata: unknown): string | undefined {
  if (!metadata || typeof metadata !== "object" || Array.isArray(metadata)) return undefined
  const record = metadata as Record<string, unknown>
  for (const key of ["taskId", "taskID", "task_id", "sessionId", "sessionID", "session_id"]) {
    const value = record[key]
    if (typeof value === "string" && value.trim().length > 0) return value.trim()
  }
  return undefined
}

function extractTaskIdFromText(output: string): string | undefined {
  const taskMetadata = output.match(/(?:task_id|session_id):\s*([a-zA-Z0-9_-]+)/)?.[1]
  if (taskMetadata) return taskMetadata
  return output.match(/Session ID:\s*(ses_[a-zA-Z0-9_-]+)/)?.[1]
}

function extractSessionId(properties: unknown): string | undefined {
  if (!properties || typeof properties !== "object" || Array.isArray(properties)) return undefined
  const record = properties as Record<string, unknown>
  const nested = record.session
  if (typeof record.sessionID === "string") return record.sessionID
  if (typeof record.id === "string") return record.id
  if (nested && typeof nested === "object" && !Array.isArray(nested) && typeof (nested as Record<string, unknown>).id === "string") return (nested as Record<string, string>).id
  return undefined
}

export const TOOL_RESULT_PLACEHOLDER = "Tool output unavailable (context compacted)"

export function createToolPairValidatorHook() {
  return {
    "experimental.chat.messages.transform": async (_input: Record<string, never>, output: { messages: MessageWithParts[] }): Promise<void> => {
      repairMissingToolResults(output.messages)
    },
  }
}

export function repairMissingToolResults(messages: MessageWithParts[]): void {
  for (let index = 0; index < messages.length; index++) {
    const message = messages[index]
    if (message.info.role !== "assistant") continue
    const toolUseIds = extractUniqueToolUseIds(message.parts)
    if (toolUseIds.length === 0) continue
    const next = messages[index + 1]
    if (next?.info.role !== "user") {
      messages.splice(index + 1, 0, { info: { role: "user", ...(typeof message.info.sessionID === "string" ? { sessionID: message.info.sessionID } : {}) }, parts: toolUseIds.map(createToolResultPart) })
      continue
    }
    const existing = new Set(next.parts.map(getToolResultId).filter((id): id is string => id !== undefined))
    const missing = toolUseIds.filter((id) => !existing.has(id))
    if (missing.length > 0) next.parts.splice(findToolResultInsertIndex(next.parts), 0, ...missing.map(createToolResultPart))
  }
}

export type DelegateTaskErrorInfo = { errorType: string; originalOutput: string }
const DELEGATE_TASK_ERROR_PATTERNS = [
  { pattern: "run_in_background", errorType: "missing_run_in_background", fixHint: "Add run_in_background=false (for delegation) or run_in_background=true (for parallel exploration)" },
  { pattern: "load_skills", errorType: "missing_load_skills", fixHint: "Add load_skills=[] parameter (empty array if no skills needed). Note: Calling Skill tool does NOT populate this." },
  { pattern: "category OR subagent_type", errorType: "mutual_exclusion", fixHint: "Provide ONLY one of: category (e.g., 'general', 'quick') OR subagent_type (e.g., 'oracle', 'explore')" },
  { pattern: "Must provide either category or subagent_type", errorType: "missing_category_or_agent", fixHint: "Add either category='general' OR subagent_type='explore'" },
  { pattern: "Unknown category", errorType: "unknown_category", fixHint: "Use a valid category from the Available list in the error message" },
  { pattern: "Agent name cannot be empty", errorType: "empty_agent", fixHint: "Provide a non-empty subagent_type value" },
  { pattern: "Unknown agent", errorType: "unknown_agent", fixHint: "Use a valid agent from the Available agents list in the error message" },
  { pattern: "Cannot call primary agent", errorType: "primary_agent", fixHint: "Primary agents cannot be called via task. Use a subagent like 'explore', 'oracle', or 'librarian'" },
  { pattern: "Skills not found", errorType: "unknown_skills", fixHint: "Use valid skill names from the Available list in the error message" },
]

export function detectDelegateTaskError(output: string): DelegateTaskErrorInfo | undefined {
  if (!output.includes("[ERROR]") && !output.includes("Invalid arguments")) return undefined
  const pattern = DELEGATE_TASK_ERROR_PATTERNS.find((candidate) => output.includes(candidate.pattern))
  return pattern ? { errorType: pattern.errorType, originalOutput: output } : undefined
}

export function addDelegateTaskRetryGuidance(tool: string, output: string): string {
  if (tool.toLowerCase() !== "task") return output
  const error = detectDelegateTaskError(output)
  return error ? `${output}\n${buildDelegateTaskRetryGuidance(error)}` : output
}

export function createDelegateTaskRetryHook() {
  return {
    "tool.execute.after": async (input: ToolBeforeHookInput, output: ToolAfterHookOutput): Promise<void> => {
      output.output = addDelegateTaskRetryGuidance(input.tool ?? "", output.output)
    },
  }
}

export function appendTaskResumeInfo(tool: string, output: string, metadata: unknown): string {
  if (!["task", "Task", "task_tool", "call_omo_agent"].includes(tool)) return output
  if (output.startsWith("Error:") || output.startsWith("Failed") || output.includes("\nto continue:")) return output
  const taskID = extractTaskId(metadata) ?? extractTaskIdFromText(output)
  return taskID ? `${output.trimEnd()}\n\nto continue: task(task_id="${taskID}", load_skills=[], run_in_background=false, prompt="...")` : output
}

export function createTaskResumeInfoHook() {
  return {
    "tool.execute.after": async (input: ToolBeforeHookInput, output: ToolAfterHookOutput): Promise<void> => {
      output.output = appendTaskResumeInfo(input.tool ?? "", output.output, output.metadata)
    },
  }
}

export function createStopContinuationGuardHook() {
  const stopped = new Set<string>()
  return {
    stop(sessionID: string): void { stopped.add(sessionID) },
    isStopped(sessionID: string): boolean { return stopped.has(sessionID) },
    clear(sessionID: string): void { stopped.delete(sessionID) },
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type === "session.deleted") {
        const sessionID = extractSessionId(event.properties)
        if (sessionID) stopped.delete(sessionID)
      }
    },
    "chat.message": async (_input: { sessionID?: string }): Promise<void> => {},
  }
}

const NON_INTERACTIVE_ENV: Record<string, string> = {
  CI: "true",
  DEBIAN_FRONTEND: "noninteractive",
  GIT_TERMINAL_PROMPT: "0",
  GCM_INTERACTIVE: "never",
  HOMEBREW_NO_AUTO_UPDATE: "1",
  GIT_EDITOR: ":",
  EDITOR: ":",
  VISUAL: "",
  GIT_SEQUENCE_EDITOR: ":",
  GIT_MERGE_AUTOEDIT: "no",
  GIT_PAGER: "cat",
  PAGER: "cat",
  npm_config_yes: "true",
  PIP_NO_INPUT: "1",
  YARN_ENABLE_IMMUTABLE_INSTALLS: "false",
}

const BANNED_INTERACTIVE_COMMANDS = ["vim", "nano", "vi", "emacs", "less", "more", "man", "git add -p", "git rebase -i"] as const

export function detectBannedInteractiveCommand(command: string): string | undefined {
  return BANNED_INTERACTIVE_COMMANDS.find((candidate) => new RegExp(`\\b${escapeRegExp(candidate)}\\b`).test(command))
}

export function buildNonInteractiveEnvPrefix(shellType: ShellType = "posix"): string {
  const entries = Object.entries(NON_INTERACTIVE_ENV)
  if (shellType === "cmd") return entries.map(([key, value]) => `set ${key}=${value}`).join(" && ")
  if (shellType === "powershell") return entries.map(([key, value]) => `$env:${key}='${value.replaceAll("'", "''")}'`).join("; ")
  return entries.map(([key, value]) => `${key}=${JSON.stringify(value)}`).join(" ")
}

export function buildNonInteractiveGitCommand(command: string, shellType: ShellType = "posix"): string {
  if (!/\bgit\b/.test(command)) return command
  const prefix = buildNonInteractiveEnvPrefix(shellType)
  return command.trimStart().startsWith(prefix.trim()) ? command : `${prefix} ${command}`
}

export function createNonInteractiveEnvHook(options: { shellType?: ShellType } = {}) {
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput, output: ToolBeforeHookOutput & { message?: string }): Promise<void> => {
      if (input.tool?.toLowerCase() !== "bash") return
      const command = typeof output.args.command === "string" ? output.args.command : undefined
      if (!command) return
      const banned = detectBannedInteractiveCommand(command)
      if (banned) output.message = `Warning: '${banned}' is an interactive command that may hang in non-interactive environments.`
      const nextCommand = buildNonInteractiveGitCommand(command, options.shellType)
      if (nextCommand !== command) replaceToolArgs(output, { command: nextCommand })
    },
  }
}

function formatSkillNames(skills: AvailableSkillLike[], limit: number): string {
  if (skills.length === 0) return "(none)"
  const shown = skills.slice(0, limit).map((skill) => skill.name)
  const remaining = skills.length - shown.length
  return shown.join(", ") + (remaining > 0 ? ` (+${remaining} more)` : "")
}

export function buildCategorySkillReminderMessage(availableSkills: AvailableSkillLike[]): string {
  const builtinSkills = availableSkills.filter((skill) => skill.location === "plugin")
  const customSkills = availableSkills.filter((skill) => skill.location !== "plugin")
  const exampleSkillName = customSkills[0]?.name ?? builtinSkills[0]?.name
  const loadSkills = exampleSkillName ? `["${exampleSkillName}"]` : "[]"
  return [
    "",
    "[Category+Skill Reminder]",
    "",
    `**Built-in**: ${formatSkillNames(builtinSkills, 8)}`,
    `**⚡ YOUR SKILLS (PRIORITY)**: ${formatSkillNames(customSkills, 8)}`,
    "",
    "> User-installed skills OVERRIDE built-in defaults. ALWAYS prefer YOUR SKILLS when domain matches.",
    "",
    "```typescript",
    `task(category=\"visual-engineering\", load_skills=${loadSkills}, run_in_background=true)`,
    "```",
    "",
  ].join("\n")
}

export function createCategorySkillReminderHook(availableSkills: AvailableSkillLike[] = []) {
  const sessionStates = new Map<string, { delegationUsed: boolean; reminderShown: boolean; toolCallCount: number }>()
  const reminder = buildCategorySkillReminderMessage(availableSkills)
  return {
    "tool.execute.after": async (input: { tool?: string; sessionID?: string; agent?: string }, output: ToolAfterHookOutput): Promise<void> => {
      const sessionID = input.sessionID
      if (!sessionID || !isCategoryReminderTargetAgent(input.agent) || !input.tool) return
      const tool = input.tool.toLowerCase()
      const state = sessionStates.get(sessionID) ?? { delegationUsed: false, reminderShown: false, toolCallCount: 0 }
      sessionStates.set(sessionID, state)
      if (tool === "task" || tool === "call_omo_agent") {
        state.delegationUsed = true
        return
      }
      if (!["edit", "write", "bash", "read", "grep", "glob"].includes(tool)) return
      state.toolCallCount++
      if (state.toolCallCount >= 3 && !state.delegationUsed && !state.reminderShown) {
        output.output += reminder
        state.reminderShown = true
      }
    },
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type !== "session.deleted") return
      const sessionID = extractSessionId(event.properties)
      if (sessionID) sessionStates.delete(sessionID)
    },
  }
}

export const AGENT_USAGE_REMINDER_MESSAGE = `
[Agent Usage Reminder]

You called a search/fetch tool directly without leveraging specialized agents.

RECOMMENDED: Use task with explore/librarian agents for better results:

\`\`\`
// Parallel exploration - fire multiple agents simultaneously
task(subagent_type="explore", load_skills=[], prompt="Find all files matching pattern X")
task(subagent_type="explore", load_skills=[], prompt="Search for implementation of Y")
task(subagent_type="librarian", load_skills=[], prompt="Lookup documentation for Z")

// Then continue your work while they run in background
// System will notify you when each completes
\`\`\`

WHY:
- Agents can perform deeper, more thorough searches
- Background tasks run in parallel, saving time
- Specialized agents have domain expertise
- Reduces context window usage in main session

ALWAYS prefer: Multiple parallel task calls > Direct tool calls
`

const AGENT_USAGE_TARGET_TOOLS = new Set(["grep", "safe_grep", "glob", "safe_glob", "webfetch", "context7_resolve-library-id", "context7_query-docs", "websearch_web_search_exa", "context7_get-library-docs", "grep_app_searchgithub"])
const AGENT_USAGE_AGENT_TOOLS = new Set(["task", "call_omo_agent"])

export function isOrchestratorAgentForReminder(agentName: string | undefined): boolean {
  if (!agentName) return true
  return ["sisyphus", "sisyphus-junior", "atlas", "hephaestus", "prometheus"].includes(getAgentConfigKey(agentName))
}

export function shouldRemindAgentUsage(tool: string, state: AgentUsageState, agentName?: string, maxReminders = 3, now: () => number = Date.now): boolean {
  if (!isOrchestratorAgentForReminder(agentName)) return false
  const toolLower = tool.toLowerCase()
  if (AGENT_USAGE_AGENT_TOOLS.has(toolLower)) {
    state.agentUsed = true
    state.updatedAt = now()
    return false
  }
  if (!AGENT_USAGE_TARGET_TOOLS.has(toolLower) || state.agentUsed || state.reminderCount >= maxReminders) return false
  state.reminderCount++
  state.updatedAt = now()
  return true
}

export function createAgentUsageReminderHook(options: { getAgent?: (sessionID: string) => string | undefined; now?: () => number } = {}) {
  const states = new Map<string, AgentUsageState>()
  const now = options.now ?? Date.now
  const getState = (sessionID: string): AgentUsageState => {
    const existing = states.get(sessionID)
    if (existing) return existing
    const created = { sessionID, agentUsed: false, reminderCount: 0, updatedAt: now() }
    states.set(sessionID, created)
    return created
  }
  return {
    getState,
    "tool.execute.after": async (input: { tool?: string; sessionID?: string; agent?: string }, output: ToolAfterHookOutput): Promise<void> => {
      if (!input.tool || !input.sessionID) return
      const state = getState(input.sessionID)
      if (shouldRemindAgentUsage(input.tool, state, options.getAgent?.(input.sessionID) ?? input.agent, 3, now)) output.output += AGENT_USAGE_REMINDER_MESSAGE
    },
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type !== "session.deleted") return
      const sessionID = extractSessionId(event.properties)
      if (sessionID) states.delete(sessionID)
    },
  }
}

export function escapeAppleScriptText(input: string): string {
  return input.replace(/\\/g, "\\\\").replace(/"/g, '\\"')
}

export function escapePowerShellSingleQuotedText(input: string): string {
  return input.replace(/'/g, "''")
}

export function buildWindowsToastScript(title: string, message: string): string {
  const psTitle = escapePowerShellSingleQuotedText(title)
  const psMessage = escapePowerShellSingleQuotedText(message)
  return `
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
$Template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$RawXml = [xml] $Template.GetXml()
($RawXml.toast.visual.binding.text | Where-Object {$_.id -eq '1'}).AppendChild($RawXml.CreateTextNode('${psTitle}')) | Out-Null
($RawXml.toast.visual.binding.text | Where-Object {$_.id -eq '2'}).AppendChild($RawXml.CreateTextNode('${psMessage}')) | Out-Null
$SerializedXml = New-Object Windows.Data.Xml.Dom.XmlDocument
$SerializedXml.LoadXml($RawXml.OuterXml)
$Toast = [Windows.UI.Notifications.ToastNotification]::new($SerializedXml)
$Notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('OpenCode')
$Notifier.Show($Toast)
`.trim().replace(/\n/g, "; ")
}

export function normalizeNotificationPlatform(platform: string): NotificationPlatform {
  return platform === "darwin" || platform === "linux" || platform === "win32" ? platform : "unsupported"
}

export function getDefaultNotificationSoundPath(platform: NotificationPlatform): string {
  switch (platform) {
    case "darwin": return "/System/Library/Sounds/Glass.aiff"
    case "linux": return "/usr/share/sounds/freedesktop/stereo/complete.oga"
    case "win32": return "C:\\Windows\\Media\\notify.wav"
    case "unsupported": return ""
  }
}

const BACKGROUND_FORWARDED_EVENT_TYPES = new Set(["message.updated", "message.part.updated", "message.part.delta", "todo.updated", "session.idle", "session.error", "session.deleted", "session.status"])

export function shouldForwardBackgroundEvent(eventType: string): boolean {
  return BACKGROUND_FORWARDED_EVENT_TYPES.has(eventType)
}

export function createBackgroundNotificationHook(manager: BackgroundNotificationManagerLike) {
  return {
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (shouldForwardBackgroundEvent(event.type)) manager.handleEvent(event)
    },
    "chat.message": async (input: { sessionID: string }, output: { parts: Array<{ type: string; text?: string; [key: string]: unknown }> }): Promise<void> => {
      manager.injectPendingNotificationsIntoChatMessage(output, input.sessionID)
    },
  }
}

export const CONTEXT_WARNING_THRESHOLD = 0.70

export function buildContextWindowReminder(actualLimit: number): string {
  return `[SYSTEM DIRECTIVE: CONTEXT_WINDOW_MONITOR]

You are using a ${actualLimit.toLocaleString()}-token context window.
You still have context remaining - do NOT rush or skip tasks.
Complete your work thoroughly and methodically.`
}

export function shouldWarnContextWindow(tokens: ContextTokenInfo, actualLimit: number): boolean {
  const totalInputTokens = (tokens.input ?? 0) + (tokens.cache?.read ?? 0)
  return actualLimit > 0 && totalInputTokens / actualLimit >= CONTEXT_WARNING_THRESHOLD
}

export function appendContextWindowStatus(output: string, tokens: ContextTokenInfo, actualLimit: number): string {
  const totalInputTokens = (tokens.input ?? 0) + (tokens.cache?.read ?? 0)
  const usage = totalInputTokens / actualLimit
  const clampedPercentage = Math.min(Math.max(usage, 0), 1)
  const usedPct = (clampedPercentage * 100).toFixed(1)
  const remainingPct = ((1 - clampedPercentage) * 100).toFixed(1)
  return `${output}\n\n${buildContextWindowReminder(actualLimit)}
[Context Status: ${usedPct}% used (${totalInputTokens.toLocaleString()}/${actualLimit.toLocaleString()} tokens), ${remainingPct}% remaining]`
}

export function createContextWindowMonitorHook(options: { resolveLimit: (providerID: string, modelID: string) => number | undefined; isCompactionAgent?: (agent: unknown) => boolean }) {
  const remindedSessions = new Set<string>()
  const tokenCache = new Map<string, { providerID: string; modelID: string; tokens: ContextTokenInfo }>()
  return {
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type === "session.deleted") {
        const sessionID = extractSessionId(event.properties)
        if (sessionID) {
          remindedSessions.delete(sessionID)
          tokenCache.delete(sessionID)
        }
        return
      }
      if (event.type !== "message.updated") return
      const props = event.properties as Record<string, unknown> | undefined
      const info = props?.info as { agent?: unknown; role?: string; sessionID?: string; providerID?: string; modelID?: string; finish?: unknown; tokens?: ContextTokenInfo } | undefined
      if (!info || info.role !== "assistant" || !info.finish || options.isCompactionAgent?.(info.agent) === true) return
      const sessionID = extractSessionId(props)
      if (!sessionID || !info.providerID || !info.tokens) return
      tokenCache.set(sessionID, { providerID: info.providerID, modelID: info.modelID ?? "", tokens: info.tokens })
    },
    "tool.execute.after": async (input: { sessionID?: string }, output: ToolAfterHookOutput): Promise<void> => {
      const sessionID = input.sessionID
      if (!sessionID || remindedSessions.has(sessionID)) return
      const cached = tokenCache.get(sessionID)
      if (!cached) return
      const actualLimit = options.resolveLimit(cached.providerID, cached.modelID)
      if (!actualLimit || !shouldWarnContextWindow(cached.tokens, actualLimit)) return
      remindedSessions.add(sessionID)
      output.output = appendContextWindowStatus(output.output, cached.tokens, actualLimit)
    },
  }
}

const UNIVERSAL_TEAM_TOOL_NAMES = new Set(["team_send_message", "team_task_create", "team_task_list", "team_task_update", "team_task_get", "team_status"])

export function isUniversalTeamTool(toolName: string): boolean {
  return UNIVERSAL_TEAM_TOOL_NAMES.has(toolName)
}

export function resolveTeamToolGate(toolName: string, participant: TeamParticipant, args: Record<string, unknown>): string | undefined {
  if (!toolName.startsWith("team_") && toolName !== "delegate-task") return undefined
  if (toolName === "delegate-task" || toolName === "team_list") return undefined
  if (toolName === "team_create") return participant.role === "neither" ? undefined : `team_create denied: session is already a participant of team ${participant.teamRunId}`
  const teamRunId = typeof args.teamRunId === "string" ? args.teamRunId : undefined
  const memberName = typeof args.memberName === "string" ? args.memberName : undefined
  if (toolName === "team_delete" || toolName === "team_shutdown_request") return participant.role === "lead" && participant.teamRunId === teamRunId ? undefined : `${toolName} is lead-only`
  if (toolName === "team_approve_shutdown" || toolName === "team_reject_shutdown") {
    const isLead = participant.role === "lead" && participant.teamRunId === teamRunId
    const isTargetMember = participant.role === "member" && participant.teamRunId === teamRunId && participant.memberName === memberName
    return isLead || isTargetMember ? undefined : `${toolName}: caller must be target member or team lead`
  }
  if (isUniversalTeamTool(toolName)) {
    const participantInTeam = (participant.role === "lead" || participant.role === "member") && participant.teamRunId === teamRunId
    if (participantInTeam) return undefined
    return teamRunId === undefined ? `team-mode tool ${toolName} requires teamRunId argument` : `team-mode tool ${toolName} denied: not a participant of team ${teamRunId}`
  }
  return undefined
}

export function createTeamToolGating(options: { enabled?: boolean; getParticipant: (sessionID: string) => TeamParticipant }) {
  return {
    "tool.execute.before": async (input: { tool?: string; sessionID?: string }, output: ToolBeforeHookOutput): Promise<void> => {
      if (!options.enabled || !input.tool || !input.sessionID) return
      const denial = resolveTeamToolGate(input.tool, options.getParticipant(input.sessionID), output.args)
      if (denial) throw new Error(denial)
    },
  }
}

export function parseTmuxCommand(tmuxCommand: string): { subCommand: string; sessionName: string | null } {
  const tokens = tokenizeTmuxCommand(tmuxCommand)
  const subCommand = findTmuxSubcommand(tokens)
  const sessionName = extractTmuxSessionName(tokens, subCommand)
  return { subCommand, sessionName }
}

export function isOmoTmuxSession(sessionName: string | null): sessionName is string {
  return typeof sessionName === "string" && sessionName.startsWith("omo-")
}

export function buildInteractiveBashSessionReminder(sessions: string[]): string {
  return sessions.length === 0 ? "" : `\n\n[System Reminder] Active omo-* tmux sessions: ${sessions.join(", ")}`
}

export function createInteractiveBashSessionHook(options: { now?: () => number } = {}) {
  const states = new Map<string, InteractiveBashSessionState>()
  const now = options.now ?? Date.now
  const getState = (sessionID: string): InteractiveBashSessionState => {
    const existing = states.get(sessionID)
    if (existing) return existing
    const created = { sessionID, tmuxSessions: new Set<string>(), updatedAt: now() }
    states.set(sessionID, created)
    return created
  }
  return {
    getState,
    "tool.execute.after": async (input: { tool?: string; sessionID?: string; args?: Record<string, unknown> }, output: ToolAfterHookOutput): Promise<void> => {
      if (input.tool?.toLowerCase() !== "interactive_bash" || !input.sessionID || typeof input.args?.tmux_command !== "string" || output.output.startsWith("Error:")) return
      const state = getState(input.sessionID)
      const { subCommand, sessionName } = parseTmuxCommand(input.args.tmux_command)
      let stateChanged = false
      if (subCommand === "new-session" && isOmoTmuxSession(sessionName)) {
        state.tmuxSessions.add(sessionName)
        stateChanged = true
      } else if (subCommand === "kill-session" && isOmoTmuxSession(sessionName)) {
        state.tmuxSessions.delete(sessionName)
        stateChanged = true
      } else if (subCommand === "kill-server") {
        state.tmuxSessions.clear()
        stateChanged = true
      }
      if (stateChanged) state.updatedAt = now()
      if (subCommand === "new-session" || subCommand === "kill-session" || subCommand === "kill-server") output.output += buildInteractiveBashSessionReminder([...state.tmuxSessions])
    },
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<void> => {
      if (event.type !== "session.deleted") return
      const sessionID = extractSessionId(event.properties)
      if (sessionID) states.delete(sessionID)
    },
  }
}

export function removeKeywordCodeBlocks(text: string): string {
  return text.replace(KEYWORD_CODE_BLOCK_PATTERN, "").replace(KEYWORD_INLINE_CODE_PATTERN, "")
}

export function looksLikeSlashCommand(text: string): boolean {
  return SLASH_COMMAND_LEAD_PATTERN.test(text)
}

export function detectKeywords(text: string, agentName?: string, modelID?: string, disabledKeywords?: readonly KeywordType[]): string[] {
  return detectKeywordsWithType(text, agentName, modelID, disabledKeywords).map(({ message }) => message)
}

export function detectKeywordsWithType(text: string, agentName?: string, modelID?: string, disabledKeywords?: readonly KeywordType[]): DetectedKeyword[] {
  const textWithoutCode = removeKeywordCodeBlocks(text)
  const disabled = new Set(disabledKeywords ?? [])
  if (disabled.has("ultrawork") || disabled.has("hyperplan")) disabled.add("hyperplan-ultrawork")
  const detectors: Array<{ type: KeywordType; pattern: RegExp; message: string }> = [
    { type: "ultrawork", pattern: /\b(ultrawork|ulw)\b/i, message: getUltraworkDirective(agentName, modelID) },
    { type: "search", pattern: SEARCH_PATTERN, message: "[search-mode]\nMAXIMIZE SEARCH EFFORT. Launch multiple background agents IN PARALLEL." },
    { type: "analyze", pattern: /\b(analyze|analyse|investigate|examine|research|study|deep[\s-]?dive|inspect|audit|evaluate|assess|review|diagnose|debug|understand)\b|why\s+is|how\s+does|how\s+to|분석|조사|파악|검토|진단|이해|설명|원인|이유|왜|어떻게/i, message: "[analyze-mode]\nANALYSIS MODE. Gather context before diving deep." },
    { type: "team", pattern: TEAM_PATTERN, message: "[team-mode]\nTeam mode reference detected. If user wants team-mode work, MUST orchestrate via team_* tools." },
    { type: "hyperplan", pattern: HYPERPLAN_PATTERN, message: '<hyperplan-mode>\n**MANDATORY**: Say "HYPERPLAN MODE ENABLED!" as your first response, exactly once.\n</hyperplan-mode>' },
    { type: "hyperplan-ultrawork", pattern: HYPERPLAN_ULTRAWORK_PATTERN, message: '<hyperplan-ultrawork-mode>\n**MANDATORY**: Say "HYPERPLAN ULTRAWORK MODE ENABLED!" exactly once as your first response.\n</hyperplan-ultrawork-mode>' },
  ]
  return detectors.filter((detector) => detector.pattern.test(textWithoutCode) && !disabled.has(detector.type)).map(({ type, message }) => ({ type, message }))
}

export function createKeywordDetectorHook() {
  return {
    "chat.message": async (input: { agent?: string; model?: { modelID?: string } }, output: { parts: Array<{ type: string; text?: string; synthetic?: boolean; [key: string]: unknown }> }): Promise<void> => {
      const text = extractRealPromptText(output.parts)
      const messages = detectKeywords(text, input.agent, input.model?.modelID)
      for (const message of messages) output.parts.push({ type: "text", text: message, synthetic: true })
    },
  }
}

export function parseSlashCommand(text: string): ParsedSlashCommand | null {
  const trimmed = text.trim()
  if (!trimmed.startsWith("/")) return null
  const match = trimmed.match(SLASH_COMMAND_PATTERN)
  if (!match) return null
  const [raw, command, args] = match
  return { command: command.toLowerCase(), args: args.trim(), raw }
}

export function detectSlashCommand(text: string): ParsedSlashCommand | null {
  const parsed = parseSlashCommand(text.replace(KEYWORD_CODE_BLOCK_PATTERN, "").trim())
  return parsed && !EXCLUDED_SLASH_COMMANDS.has(parsed.command) ? parsed : null
}

export function findSlashCommandPartIndex(parts: Array<{ type: string; text?: string; synthetic?: boolean }>): number {
  for (let index = 0; index < parts.length; index++) {
    const part = parts[index]
    if (part.type === "text" && part.synthetic !== true && (part.text ?? "").trim().startsWith("/")) return index
  }
  return -1
}

export function formatSlashCommandTemplate(command: SlashCommandInfo, args: string): string {
  const sections = [`# /${command.name} Command\n`]
  if (command.description) sections.push(`**Description**: ${command.description}\n`)
  if (args) sections.push(`**User Arguments**: ${args}\n`)
  if (command.model) sections.push(`**Model**: ${command.model}\n`)
  if (command.agent) sections.push(`**Agent**: ${command.agent}\n`)
  sections.push(`**Scope**: ${command.scope}\n`, "---\n", "## Command Instructions\n", (command.content ?? "").replace(/\$\{user_message\}/g, args).replace(/\$ARGUMENTS/g, args).trim())
  if (args) sections.push("\n\n---\n", "## User Request\n", args)
  return sections.join("\n")
}

export function createAutoSlashCommandHook(options: { commands: SlashCommandInfo[] }) {
  return {
    "chat.message": async (_input: unknown, output: { parts: Array<{ type: string; text?: string; synthetic?: boolean; [key: string]: unknown }> }): Promise<void> => {
      const index = findSlashCommandPartIndex(output.parts)
      if (index === -1) return
      const parsed = detectSlashCommand(output.parts[index]?.text ?? "")
      if (!parsed) return
      const command = options.commands.find((candidate) => candidate.name.toLowerCase() === parsed.command)
      if (!command) return
      output.parts[index] = { type: "text", text: `${AUTO_SLASH_COMMAND_TAG_OPEN}\n${formatSlashCommandTemplate(command, parsed.args)}\n${AUTO_SLASH_COMMAND_TAG_CLOSE}`, synthetic: true }
    },
  }
}

export function calculateTargetDimensions(width: number, height: number, maxLongEdge = 1568): ImageDimensions | null {
  if (width <= 0 || height <= 0 || maxLongEdge <= 0) return null
  const longEdge = Math.max(width, height)
  if (longEdge <= maxLongEdge) return null
  return width >= height
    ? { width: maxLongEdge, height: Math.max(1, Math.floor((height * maxLongEdge) / width)) }
    : { width: Math.max(1, Math.floor((width * maxLongEdge) / height)), height: maxLongEdge }
}

export function calculateImageTokens(width: number, height: number): number {
  return Math.ceil((width * height) / 750)
}

export function formatImageResizeAppendix(entries: Array<{ filename: string; originalDims: ImageDimensions | null; resizedDims: ImageDimensions | null; status: "resized" | "within-limits" | "resize-skipped" | "unknown-dims" }>): string {
  const header = entries.some((entry) => entry.status === "resized") ? "[Image Resize Info]" : "[Image Info]"
  const lines = [`\n\n${header}`]
  for (const entry of entries) {
    if (entry.status === "unknown-dims" || !entry.originalDims) {
      lines.push(`- ${entry.filename}: dimensions could not be parsed`)
      continue
    }
    const originalText = `${entry.originalDims.width}x${entry.originalDims.height}`
    const originalTokens = calculateImageTokens(entry.originalDims.width, entry.originalDims.height)
    if (entry.status === "within-limits") {
      lines.push(`- ${entry.filename}: ${originalText} (within limits, tokens: ${originalTokens})`)
    } else if (entry.status === "resize-skipped") {
      lines.push(`- ${entry.filename}: ${originalText} (exceeds provider limits, image removed to prevent API error)`)
    } else if (!entry.resizedDims) {
      lines.push(`- ${entry.filename}: ${originalText} (resize skipped, tokens: ${originalTokens})`)
    } else {
      const resizedText = `${entry.resizedDims.width}x${entry.resizedDims.height}`
      const resizedTokens = calculateImageTokens(entry.resizedDims.width, entry.resizedDims.height)
      lines.push(`- ${entry.filename}: ${originalText} -> ${resizedText} (resized, tokens: ${originalTokens} -> ${resizedTokens})`)
    }
  }
  return lines.join("\n")
}

export function computeLineHash(lineNumber: number, content: string): string {
  const normalized = content.replace(/\r/g, "").trimEnd()
  const seed = /[\p{L}\p{N}]/u.test(normalized) ? 0 : lineNumber
  return HASHLINE_DICT[xxHash32(normalized, seed) % 256]
}

export function formatHashLine(lineNumber: number, content: string): string {
  return `${lineNumber}#${computeLineHash(lineNumber, content)}|${content}`
}

export function transformHashlineReadOutput(output: string): string {
  if (!output) return output
  const lines = output.split("\n")
  const contentStart = lines.findIndex((line) => line === "<content>" || line.startsWith("<content>"))
  const contentEnd = lines.indexOf("</content>")
  const fileStart = lines.findIndex((line) => line === "<file>" || line.startsWith("<file>"))
  const fileEnd = lines.indexOf("</file>")
  const blockStart = contentStart !== -1 ? contentStart : fileStart
  const blockEnd = contentStart !== -1 ? contentEnd : fileEnd
  const openTag = contentStart !== -1 ? "<content>" : "<file>"
  if (blockStart !== -1 && blockEnd !== -1 && blockEnd > blockStart) {
    const openLine = lines[blockStart] ?? ""
    const inlineFirst = openLine.startsWith(openTag) && openLine !== openTag ? openLine.slice(openTag.length) : null
    const fileLines = inlineFirst !== null ? [inlineFirst, ...lines.slice(blockStart + 1, blockEnd)] : lines.slice(blockStart + 1, blockEnd)
    if (!isHashlineTextFile(fileLines[0] ?? "")) return output
    const transformed = transformHashlineLines(fileLines)
    const prefix = inlineFirst !== null ? [...lines.slice(0, blockStart), openTag] : lines.slice(0, blockStart + 1)
    return [...prefix, ...transformed, ...lines.slice(blockEnd)].join("\n")
  }
  return isHashlineTextFile(lines[0] ?? "") ? transformHashlineLines(lines).join("\n") : output
}

export function buildHashlineWriteSuccessOutput(output: string, metadata: unknown): string {
  if (output.startsWith(WRITE_SUCCESS_MARKER) || output.toLowerCase().startsWith("error") || output.toLowerCase().includes("failed")) return output
  const lineCount = extractMetadataLineCount(metadata)
  return lineCount === undefined ? output : `${WRITE_SUCCESS_MARKER} ${lineCount} lines written.`
}

export function createHashlineReadEnhancerHook(config: { enabled?: boolean }) {
  return {
    "tool.execute.after": async (input: { tool: string }, output: { output: string; metadata?: unknown }): Promise<void> => {
      if (!config.enabled || typeof output.output !== "string") return
      if (input.tool.toLowerCase() === "read") output.output = transformHashlineReadOutput(output.output)
      else if (input.tool.toLowerCase() === "write") output.output = buildHashlineWriteSuccessOutput(output.output, output.metadata)
    },
  }
}

export function createIdleNotificationState() {
  return { notifiedSessions: new Set<string>(), pendingSessions: new Set<string>(), sessionActivitySinceIdle: new Set<string>(), notificationVersions: new Map<string, number>(), executingNotifications: new Set<string>(), scheduledAt: new Map<string, number>() }
}

export function createIdleNotificationScheduler(options: { maxTrackedSessions: number; idleConfirmationDelay: number; activityGracePeriodMs?: number; now?: () => number }) {
  const state = createIdleNotificationState()
  const now = options.now ?? Date.now
  const activityGracePeriodMs = options.activityGracePeriodMs ?? 100
  const cleanupOldSessions = () => {
    trimSet(state.notifiedSessions, options.maxTrackedSessions)
    trimSet(state.pendingSessions, options.maxTrackedSessions)
    trimSet(state.sessionActivitySinceIdle, options.maxTrackedSessions)
    trimMap(state.notificationVersions, options.maxTrackedSessions)
    trimSet(state.executingNotifications, options.maxTrackedSessions)
    trimMap(state.scheduledAt, options.maxTrackedSessions)
  }
  return {
    state,
    markSessionActivity(sessionID: string): IdleNotificationDecision {
      const scheduledTime = state.scheduledAt.get(sessionID)
      if (activityGracePeriodMs > 0 && scheduledTime !== undefined && now() - scheduledTime <= activityGracePeriodMs) return "ignored-pending"
      state.pendingSessions.delete(sessionID)
      state.scheduledAt.delete(sessionID)
      state.sessionActivitySinceIdle.add(sessionID)
      state.notificationVersions.set(sessionID, (state.notificationVersions.get(sessionID) ?? 0) + 1)
      if (!state.executingNotifications.has(sessionID)) state.notifiedSessions.delete(sessionID)
      return "cancelled-by-activity"
    },
    scheduleIdleNotification(sessionID: string): IdleNotificationDecision {
      if (state.notifiedSessions.has(sessionID)) return "ignored-already-notified"
      if (state.pendingSessions.has(sessionID)) return "ignored-pending"
      if (state.executingNotifications.has(sessionID)) return "ignored-executing"
      state.sessionActivitySinceIdle.delete(sessionID)
      state.scheduledAt.set(sessionID, now())
      state.pendingSessions.add(sessionID)
      state.notificationVersions.set(sessionID, (state.notificationVersions.get(sessionID) ?? 0) + 1)
      cleanupOldSessions()
      return "scheduled"
    },
    shouldExecuteNotification(sessionID: string, version: number, hasIncompleteTodos: boolean): boolean {
      if (state.executingNotifications.has(sessionID) || state.notificationVersions.get(sessionID) !== version || state.sessionActivitySinceIdle.delete(sessionID) || state.notifiedSessions.has(sessionID) || hasIncompleteTodos) return false
      state.executingNotifications.add(sessionID)
      state.notifiedSessions.add(sessionID)
      return true
    },
    finishNotification(sessionID: string): void {
      state.executingNotifications.delete(sessionID)
      state.pendingSessions.delete(sessionID)
      state.scheduledAt.delete(sessionID)
      if (state.sessionActivitySinceIdle.delete(sessionID)) state.notifiedSessions.delete(sessionID)
    },
    deleteSession(sessionID: string): IdleNotificationDecision {
      state.pendingSessions.delete(sessionID)
      state.notifiedSessions.delete(sessionID)
      state.sessionActivitySinceIdle.delete(sessionID)
      state.notificationVersions.delete(sessionID)
      state.executingNotifications.delete(sessionID)
      state.scheduledAt.delete(sessionID)
      return "deleted"
    },
  }
}

export function detectErrorType(error: unknown): RecoveryErrorType {
  const message = getRecoveryErrorMessage(error)
  if (message.includes("assistant message prefill") || message.includes("conversation must end with a user message")) return "assistant_prefill_unsupported"
  if (message.includes("thinking") && (message.includes("first block") || message.includes("must start with") || message.includes("preceeding") || message.includes("final block") || message.includes("cannot be thinking") || (message.includes("expected") && message.includes("found")))) return "thinking_block_order"
  if (message.includes("thinking") && message.includes("cannot be modified")) return "thinking_block_modified"
  if (message.includes("thinking is disabled") && message.includes("cannot contain")) return "thinking_disabled_violation"
  if (message.includes("tool_use") && message.includes("tool_result")) return "tool_result_missing"
  if (message.includes("dummy_tool") || message.includes("unavailable tool") || message.includes("model tried to call unavailable") || message.includes("nosuchtoolerror") || message.includes("no such tool")) return "unavailable_tool"
  return null
}

export function extractMessageIndex(error: unknown): number | null {
  const match = getRecoveryErrorMessage(error).match(/messages\.(\d+)/)
  return match ? Number.parseInt(match[1], 10) : null
}

export function extractUnavailableToolName(error: unknown): string | null {
  const match = getRecoveryErrorMessage(error).match(/(?:unavailable tool|no such tool)[:\s'"]+([^'".\s]+)/)
  return match ? match[1] : null
}

export function createSessionRecoveryHook() {
  return { isRecoverableError: (error: unknown) => detectErrorType(error) !== null, detectErrorType, extractMessageIndex, extractUnavailableToolName }
}

export function parseAnthropicTokenLimitError(error: unknown): ParsedTokenLimitError | null {
  const textSources = collectTokenLimitTextSources(error)
  if (textSources.length === 0) return null
  const combinedText = textSources.join(" ")
  if (!isTokenLimitErrorText(combinedText)) return null
  if (combinedText.toLowerCase().includes("non-empty content")) return { currentTokens: 0, maxTokens: 0, errorType: "non-empty content", messageIndex: extractTokenLimitMessageIndex(combinedText) }
  for (const text of textSources) {
    const tokens = extractTokensFromLimitMessage(text)
    if (tokens) return { ...tokens, errorType: "token_limit_exceeded", requestId: extractRequestId(text) }
  }
  return { currentTokens: 0, maxTokens: 0, errorType: "token_limit_exceeded_unknown" }
}

export function isTokenLimitErrorText(text: string): boolean {
  if (THINKING_BLOCK_ERROR_PATTERNS.some((pattern) => pattern.test(text))) return false
  const lower = text.toLowerCase()
  return TOKEN_LIMIT_KEYWORDS.some((keyword) => lower.includes(keyword))
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes}B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)}MB`
}

export function buildTeamMailboxTurnMarker(sessionID: string, messages: MessageWithParts[]): string {
  return `${sessionID}#${messages.length}`
}

export function injectTeamMailboxMessage(messages: MessageWithParts[], sessionID: string, content: string): MessageWithParts[] {
  const injected = createSyntheticUserMessage(sessionID, content)
  const lastUserIndex = findLastUserMessageIndex(messages)
  const next = [...messages]
  if (lastUserIndex === -1) next.unshift(injected)
  else next.splice(lastUserIndex, 0, injected)
  return next
}

export function createTeamMailboxInjector(options: { enabled: boolean; getInjection: (sessionID: string, turnMarker: string) => { injected: boolean; content?: string } }) {
  return {
    "experimental.chat.messages.transform": async (input: { sessionID?: string }, output: { messages: MessageWithParts[] }): Promise<void> => {
      if (!options.enabled || output.messages.length === 0) return
      const sessionID = input.sessionID ?? resolveMessageSessionID(output.messages)
      if (!sessionID) return
      const result = options.getInjection(sessionID, buildTeamMailboxTurnMarker(sessionID, output.messages))
      if (result.injected && result.content) output.messages = injectTeamMailboxMessage(output.messages, sessionID, result.content)
    },
  }
}

export function buildTeamModeStatusContent(): string {
  return `${TEAM_MODE_STATUS_MARKER}\nTeam mode is ENABLED for this session.\nIf the team_* tools are present, that is authoritative proof that team mode is active.\nDo not inspect ~/.config/opencode or project config files to verify team mode.\nIf you need usage guidance, load the team-mode skill. Otherwise use the team_* tools directly.\n</team_mode_status>`
}

export function injectTeamModeStatus(messages: MessageWithParts[], sessionID: string): MessageWithParts[] {
  if (messages.some((message) => message.parts.some((part) => part.synthetic === true && part.type === "text" && typeof part.text === "string" && part.text.includes(TEAM_MODE_STATUS_MARKER)))) return messages
  const lastUserIndex = findLastUserMessageIndex(messages)
  if (lastUserIndex === -1) return messages
  const next = [...messages]
  next.splice(lastUserIndex, 0, createSyntheticUserMessage(sessionID, buildTeamModeStatusContent()))
  return next
}

export function createTeamModeStatusInjector(options: { enabled: boolean; disabledKeywords?: readonly KeywordType[] }) {
  return {
    "experimental.chat.messages.transform": async (input: { sessionID?: string }, output: { messages: MessageWithParts[] }): Promise<void> => {
      if (!options.enabled || output.messages.length === 0) return
      const sessionID = input.sessionID ?? resolveMessageSessionID(output.messages)
      if (!sessionID) return
      const lastUserIndex = findLastUserMessageIndex(output.messages)
      if (lastUserIndex === -1) return
      const message = output.messages[lastUserIndex]
      const promptText = message.parts.filter((part) => part.type === "text" && part.synthetic !== true).map((part) => part.text || "").join(" ")
      if (detectKeywordsWithType(promptText, undefined, undefined, options.disabledKeywords).some((keyword) => keyword.type === "team")) output.messages = injectTeamModeStatus(output.messages, sessionID)
    },
  }
}

export function buildCompactionContextPrompt(history?: string): string {
  const prompt = `<system-directive type="compaction_context">\n\nWhen summarizing this session, you MUST include the following sections in your summary:\n\n## 1. User Requests (As-Is)\n## 2. Final Goal\n## 3. Work Completed\n## 4. Remaining Tasks\n## 5. Active Working Context (For Seamless Continuation)\n## 6. Explicit Constraints (Verbatim Only)\n## 7. Agent Verification State (Critical for Reviewers)\n## 8. Delegated Agent Sessions\n`
  return history ? `${prompt}\n### Active/Recent Delegated Sessions\n${history}\n` : prompt
}

export function createTailMonitorState(): TailMonitorState {
  return { currentHasOutput: false, consecutiveNoTextMessages: 0 }
}

export function finalizeTrackedAssistantMessage(state: TailMonitorState): number {
  if (!state.currentMessageID) return state.consecutiveNoTextMessages
  state.consecutiveNoTextMessages = state.currentHasOutput ? 0 : state.consecutiveNoTextMessages + 1
  state.currentMessageID = undefined
  state.currentHasOutput = false
  return state.consecutiveNoTextMessages
}

export function shouldTreatAssistantPartAsOutput(part: { type?: string; text?: string }): boolean {
  return part.type === "text" ? !!part.text?.trim() : typeof part.type === "string" && ["reasoning", "tool", "tool_use"].includes(part.type)
}

export function trackAssistantOutput(state: TailMonitorState, messageID?: string): void {
  if (messageID && !state.currentMessageID) state.currentMessageID = messageID
  state.currentHasOutput = true
  state.consecutiveNoTextMessages = 0
}

export function createCompactionContextInjector(options: { history?: (sessionID: string) => string | undefined } = {}) {
  const tailStates = new Map<string, TailMonitorState>()
  const getTailState = (sessionID: string) => tailStates.get(sessionID) ?? (tailStates.set(sessionID, createTailMonitorState()), tailStates.get(sessionID)!)
  return { inject: (sessionID?: string) => buildCompactionContextPrompt(sessionID ? options.history?.(sessionID) : undefined), getTailState, clear: (sessionID: string) => tailStates.delete(sessionID) }
}

export function extractTodos(response: unknown): TodoSnapshot[] {
  const payload = response as { data?: unknown }
  if (Array.isArray(payload?.data)) return payload.data as TodoSnapshot[]
  return Array.isArray(response) ? response as TodoSnapshot[] : []
}

export function hasDetailedTodos(todos: TodoSnapshot[]): boolean {
  return todos.some((todo) => !isAtlasBootstrapTodo(todo))
}

export function isAtlasBootstrapTodoList(todos: TodoSnapshot[]): boolean {
  return todos.length > 0 && todos.every(isAtlasBootstrapTodo)
}

export function shouldRestoreOverCurrentTodos(input: { snapshot: TodoSnapshot[]; currentTodos: TodoSnapshot[] }): boolean {
  if (input.currentTodos.length === 0) return true
  if (!isAtlasBootstrapTodoList(input.currentTodos)) return false
  return hasDetailedTodos(input.snapshot)
}

export function replaceAtlasBootstrapTodos(requestedTodos: TodoSnapshot[], snapshot: TodoSnapshot[]): TodoSnapshot[] {
  return isAtlasBootstrapTodoList(requestedTodos) && hasDetailedTodos(snapshot) ? snapshot : requestedTodos
}

export function getMessageInfo(value: unknown): { role?: string; agent?: string; model?: { providerID: string; modelID: string; variant?: string }; providerID?: string; modelID?: string; tools?: Record<string, boolean | "allow" | "deny" | "ask"> } | undefined {
  if (!isRecord(value) || !isRecord(value.info)) return undefined
  const info = value.info
  const modelValue = isRecord(info.model) ? info.model : undefined
  const model = modelValue && typeof modelValue.providerID === "string" && typeof modelValue.modelID === "string" ? { providerID: modelValue.providerID, modelID: modelValue.modelID, ...(typeof modelValue.variant === "string" ? { variant: modelValue.variant } : {}) } : undefined
  return { role: typeof info.role === "string" ? info.role : undefined, agent: typeof info.agent === "string" ? info.agent : undefined, model, providerID: typeof info.providerID === "string" ? info.providerID : undefined, modelID: typeof info.modelID === "string" ? info.modelID : undefined, tools: isRecord(info.tools) ? Object.fromEntries(Object.entries(info.tools).filter(([, value]) => value === true || value === false || value === "allow" || value === "deny" || value === "ask")) as Record<string, boolean | "allow" | "deny" | "ask"> : undefined }
}

export function getMessageParts(value: unknown): Array<{ type?: string; text?: string; thinking?: string }> {
  if (!isRecord(value) || !Array.isArray(value.parts)) return []
  return value.parts.filter(isRecord).map((part) => ({ type: typeof part.type === "string" ? part.type : undefined, text: typeof part.text === "string" ? part.text : undefined, thinking: typeof part.thinking === "string" ? part.thinking : undefined }))
}

export function extractMessages(value: unknown): unknown[] {
  return Array.isArray(value) ? value : isRecord(value) && Array.isArray(value.data) ? value.data : []
}

export function isUnstableTask(task: BackgroundTaskLike): boolean {
  const modelID = task.model?.modelID?.toLowerCase()
  return task.isUnstableAgent === true || (modelID ? modelID.includes("gemini") || modelID.includes("minimax") : false)
}

export function buildUnstableAgentReminder(task: BackgroundTaskLike, summary: string | null, idleMs: number): string {
  return `Unstable background agent appears idle for ${Math.round(idleMs / 1000)}s.\n\nTask ID: ${task.id}\nDescription: ${task.description}\nAgent: ${task.agent}\nStatus: ${task.status}\nSession ID: ${task.sessionId ?? "N/A"}\n\nThinking summary (first ${THINKING_SUMMARY_MAX_CHARS} chars):\n${summary ?? "(No thinking trace available)"}\n\nSuggested actions:\n- background_output task_id="${task.id}" full_session=true include_thinking=true include_tool_results=true message_limit=50\n- background_cancel taskId="${task.id}"\n\nThis is a reminder only. No automatic action was taken.`
}

export function getTodoProgressSnapshot(todos: TodoSnapshot[]): string {
  return todos.map((todo) => ({ key: todo.id ?? `${todo.content}:${todo.priority}`, status: todo.status })).sort((left, right) => left.key.localeCompare(right.key)).map(({ key, status }) => `${key}=${status}`).join("|")
}

export function trackContinuationProgress(input: { state: ContinuationState; incompleteCount: number; previousSnapshot?: string; todos?: TodoSnapshot[] }): { nextSnapshot?: string; hasProgressed: boolean; stagnationCount: number } {
  const previousIncompleteCount = input.state.lastIncompleteCount
  const nextSnapshot = input.todos ? getTodoProgressSnapshot(input.todos) : undefined
  input.state.lastIncompleteCount = input.incompleteCount
  const hasProgressed = previousIncompleteCount !== undefined && (input.incompleteCount < previousIncompleteCount || (nextSnapshot !== undefined && input.previousSnapshot !== undefined && nextSnapshot !== input.previousSnapshot))
  if (hasProgressed) {
    input.state.stagnationCount = 0
    input.state.awaitingPostInjectionProgressCheck = false
  } else if (previousIncompleteCount !== undefined && input.state.awaitingPostInjectionProgressCheck) {
    input.state.stagnationCount += 1
    input.state.awaitingPostInjectionProgressCheck = false
  }
  return { nextSnapshot, hasProgressed, stagnationCount: input.state.stagnationCount }
}

export function createTodoContinuationEnforcer() {
  const states = new Map<string, ContinuationState>()
  return { getState: (sessionID: string) => states.get(sessionID) ?? (states.set(sessionID, { stagnationCount: 0 }), states.get(sessionID)!), markRecovering: (sessionID: string) => { states.set(sessionID, { ...states.get(sessionID), stagnationCount: states.get(sessionID)?.stagnationCount ?? 0 }) }, cleanup: (sessionID: string) => states.delete(sessionID) }
}

export function extractSessionNotificationText(message: SessionNotificationMessage | undefined): string {
  return (message?.parts ?? []).filter((part) => part.type === "text" && typeof part.text === "string").map((part) => part.text?.trim() ?? "").filter(Boolean).join("\n")
}

export function findLastSessionNotificationMessage(messages: SessionNotificationMessage[], role: "user" | "assistant"): SessionNotificationMessage | undefined {
  for (let index = messages.length - 1; index >= 0; index--) {
    const message = messages[index]
    if (message.info?.role === role && !(role === "assistant" && message.info.error) && extractSessionNotificationText(message)) return message
  }
  return undefined
}

export function buildReadyNotificationContent(input: { sessionID: string; sessionTitle?: string; baseTitle: string; baseMessage: string; messages?: SessionNotificationMessage[] }): { title: string; message: string } {
  const messages = input.messages ?? []
  const lastUserText = collapseWhitespace(extractSessionNotificationText(findLastSessionNotificationMessage(messages, "user")))
  const lastAssistantLine = getLastNonEmptyLine(extractSessionNotificationText(findLastSessionNotificationMessage(messages, "assistant")))
  const detailLines = [lastUserText ? `User: ${lastUserText}` : "", lastAssistantLine ? `Assistant: ${lastAssistantLine}` : ""].filter(Boolean)
  return { title: `${input.baseTitle} · ${input.sessionTitle?.trim() || input.sessionID}`, message: detailLines.length > 0 ? [input.baseMessage, ...detailLines].join("\n") : input.baseMessage }
}

export function createSessionNotification(options: { platform?: NotificationPlatform; baseTitle: string; baseMessage: string }) {
  const platform = options.platform ?? normalizeNotificationPlatform(process.platform)
  return { platform, defaultSoundPath: getDefaultNotificationSoundPath(platform), buildContent: (input: { sessionID: string; sessionTitle?: string; messages?: SessionNotificationMessage[] }) => buildReadyNotificationContent({ ...input, baseTitle: options.baseTitle, baseMessage: options.baseMessage }) }
}

export function isPrereleaseVersion(version: string): boolean { return version.includes("-") }
export function isDistTag(version: string): boolean { return !/^\d/.test(version) }
export function isPrereleaseOrDistTag(version: string | null): boolean { return !!version && (isPrereleaseVersion(version) || isDistTag(version)) }
export function extractChannel(version: string | null): string {
  if (!version) return "latest"
  if (isDistTag(version)) return version
  const prerelease = version.split("-")[1]
  return prerelease?.match(/^(alpha|beta|rc|canary|next)/)?.[1] ?? "latest"
}
export function shouldShowAutoUpdateToast(result: { needsUpdate: boolean; isLocalDev: boolean; currentVersion: string | null; latestVersion: string | null }, options: { showStartupToast?: boolean; autoUpdate?: boolean }): boolean {
  return options.showStartupToast !== false && options.autoUpdate !== false && result.needsUpdate && !result.isLocalDev && !!result.currentVersion && !!result.latestVersion
}
export function createAutoUpdateCheckerHook(options: { showStartupToast?: boolean; autoUpdate?: boolean }) { return { shouldShowToast: (result: Parameters<typeof shouldShowAutoUpdateToast>[0]) => shouldShowAutoUpdateToast(result, options), extractChannel } }

export function listClaudeCodeHookNames(): string[] { return ["experimental.session.compacting", "chat.message", "tool.execute.before", "tool.execute.after", "event", "dispose"] }
export function createClaudeCodeHooksHook() { return Object.fromEntries(listClaudeCodeHookNames().map((name) => [name, true])) }

export function shouldRunPreemptiveCompaction(input: { cached?: { providerID: string; modelID: string; tokens: ContextTokenInfo }; actualLimit: number | null; compacted: boolean; inProgress: boolean; lastCompactionTime?: number; now: number }): { shouldRun: boolean; usageRatio: number } {
  if (input.compacted || input.inProgress || !input.cached || input.actualLimit === null || !input.cached.modelID) return { shouldRun: false, usageRatio: 0 }
  if (input.lastCompactionTime && input.now - input.lastCompactionTime < PREEMPTIVE_COMPACTION_COOLDOWN_MS) return { shouldRun: false, usageRatio: 0 }
  const usageRatio = ((input.cached.tokens.input ?? 0) + (input.cached.tokens.cache?.read ?? 0)) / input.actualLimit
  return { shouldRun: usageRatio >= PREEMPTIVE_COMPACTION_THRESHOLD, usageRatio }
}
export function buildPreemptiveCompactionFailureToast(error: unknown): { title: string; message: string; variant: "warning"; duration: number } { return { title: "Preemptive compaction failed", message: `Context window is above ${Math.round(PREEMPTIVE_COMPACTION_THRESHOLD * 100)}% and auto-compaction could not run. The session may grow large. Error: ${String(error)}`, variant: "warning", duration: 10000 } }

export function getRuntimeFallbackErrorMessage(error: unknown): string { return getRecoveryErrorMessage(error) }
export function extractRuntimeFallbackStatusCode(error: unknown, retryOnErrors: number[] = [429, 500, 502, 503, 504]): number | undefined { const direct = extractStatusCodeFromObject(error); if (direct !== undefined) return direct; const match = getRuntimeFallbackErrorMessage(error).match(new RegExp(`\\b(${retryOnErrors.join("|")})\\b`)); return match ? Number.parseInt(match[1], 10) : undefined }
export function classifyRuntimeFallbackErrorType(error: unknown): string | undefined { const message = getRuntimeFallbackErrorMessage(error); const name = extractRuntimeFallbackErrorName(error)?.toLowerCase().replace(/[_-]/g, ""); if (name?.includes("loadapi") || (/api.?key.?is.?missing/i.test(message) && /environment variable/i.test(message))) return "missing_api_key"; if (/api.?key/i.test(message) && /must be a string/i.test(message)) return "invalid_api_key"; if (name?.includes("modelnotfound") || /model\s+not\s+found/i.test(message)) return "model_not_found"; if (name?.includes("quotaexceeded") || name?.includes("resourceexhausted") || /quota.?exceeded|insufficient.?quota|out\s+of\s+credits?|payment.?required/i.test(message)) return "quota_exceeded"; return undefined }
export function containsRuntimeFallbackErrorContent(parts: Array<{ type?: string; text?: string }> | undefined): { hasError: boolean; errorMessage?: string } { const errors = (parts ?? []).filter((part) => part.type === "error").map((part) => part.text).filter((text): text is string => typeof text === "string"); return errors.length > 0 ? { hasError: true, errorMessage: errors.join("\n") || undefined } : { hasError: false } }
export function isRuntimeFallbackRetryableError(error: unknown, retryOnErrors: number[] = [429, 500, 502, 503, 504]): boolean { const type = classifyRuntimeFallbackErrorType(error); return type === "missing_api_key" || type === "model_not_found" || type === "quota_exceeded" || !!(extractRuntimeFallbackStatusCode(error, retryOnErrors) && retryOnErrors.includes(extractRuntimeFallbackStatusCode(error, retryOnErrors)!)) || RUNTIME_RETRYABLE_ERROR_PATTERNS.some((pattern) => pattern.test(getRuntimeFallbackErrorMessage(error))) }
export function createRuntimeFallbackHook(config: RuntimeFallbackConfig = {}) { return { isRetryableError: (error: unknown) => isRuntimeFallbackRetryableError(error, config.retry_on_errors ?? [429, 500, 502, 503, 504]), classifyErrorType: classifyRuntimeFallbackErrorType } }

export function parseUserRequest(promptText: string): ParsedUserRequest {
  const match = promptText.match(/<user-request>\s*([\s\S]*?)\s*<\/user-request>/i)
  if (!match) return { planName: null, explicitWorktreePath: null }
  let rawArg = match[1].trim()
  if (!rawArg) return { planName: null, explicitWorktreePath: null }
  const worktreeMatch = rawArg.match(WORKTREE_FLAG_PATTERN)
  const explicitWorktreePath = worktreeMatch ? worktreeMatch[1] ?? null : null
  if (worktreeMatch) rawArg = rawArg.replace(worktreeMatch[0], "").trim()
  const cleanedArg = rawArg.replace(START_WORK_KEYWORD_PATTERN, "").trim()
  const quoted = cleanedArg.match(WRAPPING_QUOTES_PATTERN)
  return { planName: (quoted ? quoted[2].trim() : cleanedArg) || null, explicitWorktreePath }
}

export function parseWorktreeListPorcelain(output: string): WorktreeEntry[] {
  const entries: WorktreeEntry[] = []
  let current: Partial<WorktreeEntry> | undefined
  for (const line of output.split("\n").map((value) => value.trim())) {
    if (!line) {
      if (current?.path) entries.push({ path: current.path, branch: current.branch, bare: current.bare ?? false })
      current = undefined
    } else if (line.startsWith("worktree ")) current = { path: line.slice("worktree ".length).trim() }
    else if (current && line.startsWith("branch ")) current.branch = line.slice("branch ".length).trim().replace(/^refs\/heads\//, "")
    else if (current && line === "bare") current.bare = true
  }
  if (current?.path) entries.push({ path: current.path, branch: current.branch, bare: current.bare ?? false })
  return entries
}

export function resolveStartWorkTemplate(promptText: string, input: { sessionID: string; timestamp: string; contextInfo: string }): string | null {
  if (!promptText.includes("<session-context>") || !promptText.includes(START_WORK_TEMPLATE_MARKER)) return null
  return `${promptText.replace(/\$SESSION_ID/g, input.sessionID).replace(/\$TIMESTAMP/g, input.timestamp)}\n\n---\n${input.contextInfo}`
}

export function createStartWorkHook() { return { parseUserRequest, parseWorktreeListPorcelain, resolveStartWorkTemplate } }

export function parseTrackedTaskFromPrompt(prompt: string): TrackedTaskRef | null {
  const lines = prompt.split(/\r?\n/)
  const taskHeaderIndex = lines.findIndex((line) => TASK_SECTION_HEADER_PATTERN.test(line.trim()))
  if (taskHeaderIndex < 0) return null
  for (let index = taskHeaderIndex + 1; index < Math.min(lines.length, taskHeaderIndex + 6); index++) {
    const candidate = lines[index]?.trim()
    if (!candidate) continue
    const finalWaveMatch = candidate.match(FINAL_WAVE_TASK_LINE_PATTERN)
    if (finalWaveMatch?.[1] && finalWaveMatch[2]) return { key: `final-wave:${finalWaveMatch[1].toLowerCase()}`, label: finalWaveMatch[1].toUpperCase(), title: finalWaveMatch[2].trim() }
    const todoMatch = candidate.match(TODO_TASK_LINE_PATTERN)
    if (todoMatch?.[1] && todoMatch[2]) return { key: `todo:${todoMatch[1]}`, label: todoMatch[1], title: todoMatch[2].trim() }
  }
  return null
}

export function buildAtlasSingleTaskPrompt(prompt: string): string {
  return prompt.includes("<system-") ? prompt : `<system-reminder>${ATLAS_SINGLE_TASK_DIRECTIVE}</system-reminder>\n${prompt}`
}

export function shouldWarnAtlasDirectModification(input: { tool: string; filePath?: string; isOmoPath?: boolean }): boolean {
  return ["write", "edit", "multiedit"].includes(input.tool.toLowerCase()) && !!input.filePath && input.isOmoPath !== true
}

export function resolveAtlasPendingTaskRef(input: { callID?: string; prompt?: string; requestedSessionId?: string; existingKeys?: string[] }): { kind: "track"; task: TrackedTaskRef } | { kind: "skip"; reason: "explicit_resume" | "ambiguous_task_key"; task?: TrackedTaskRef } | null {
  if (!input.callID) return null
  if (input.requestedSessionId) return { kind: "skip", reason: "explicit_resume" }
  const task = input.prompt ? parseTrackedTaskFromPrompt(input.prompt) : null
  if (!task) return null
  return input.existingKeys?.includes(task.key) ? { kind: "skip", reason: "ambiguous_task_key", task } : { kind: "track", task }
}

export function createAtlasHook() { return { parseTrackedTaskFromPrompt, buildAtlasSingleTaskPrompt, shouldWarnAtlasDirectModification, resolveAtlasPendingTaskRef } }

export function describePathClassification(pathClassification: PathClassification): string {
  switch (pathClassification) {
    case "icloud": return "iCloud Drive"
    case "onedrive": return "OneDrive"
    case "desktop-sync": return "Desktop sync (macOS)"
    case "network-drive": return "Network drive"
    case "unknown": return "filesystem that does not support fsync"
  }
}

export function formatFsyncSkipWarning(entries: FsyncSkipEntry[]): string {
  if (entries.length === 0) return ""
  const classification = selectMostCommonPathClassification(entries)
  const shownEntries = entries.slice(0, 5)
  const hiddenCount = entries.length - shownEntries.length
  const pathLines = shownEntries.map((entry) => `  - ${entry.filePath} (code: ${entry.errorCode})`)
  if (hiddenCount > 0) pathLines.push(`  ... and ${hiddenCount} more`)
  const environmentLines = classification === "unknown" ? [] : [`Detected environment: ${describePathClassification(classification)}`]
  const durabilityLine = classification === "unknown"
    ? "  - Crash durability is best-effort because this filesystem does not support fsync."
    : "  - Crash durability is best-effort on this filesystem (this is normal for iCloud, OneDrive, network drives, antivirus-locked paths)."
  return [
    "---",
    `[fsync-skipped] ${entries.length} write(s) bypassed fsync because the underlying filesystem rejected the syscall.`,
    "",
    ...environmentLines,
    "Affected paths:",
    ...pathLines,
    "",
    "What this means:",
    "  - The write+rename succeeded — the file is on disk, atomicity is preserved.",
    durabilityLine,
    "  - No action required. Operation completed successfully.",
  ].join("\n")
}

export type LegacyPluginToastInput = { hasLegacyEntry: boolean; legacyEntries?: string[]; migration?: { migrated: boolean; from?: string; to?: string } }
export type LegacyPluginToastDecision = { title: string; message: string; variant: "success" | "warning"; duration: number } | undefined

export function resolveLegacyPluginToastDecision(input: LegacyPluginToastInput): LegacyPluginToastDecision {
  if (!input.hasLegacyEntry) return undefined
  if (input.migration?.migrated) {
    return {
      title: "Plugin Entry Migrated",
      message: `"${input.migration.from}" has been renamed to "${input.migration.to}" in your opencode.json.\nNo action needed.`,
      variant: "success",
      duration: 8000,
    }
  }
  return {
    title: "Legacy Plugin Name Detected",
    message: "Update your opencode.json: \"oh-my-opencode\" has been renamed to \"oh-my-openagent\".\nRun: bunx oh-my-opencode install",
    variant: "warning",
    duration: 10000,
  }
}

export function createLegacyPluginToastDecisionHook(getInput: () => LegacyPluginToastInput) {
  let fired = false
  return {
    event: async ({ event }: { event: { type: string; properties?: unknown } }): Promise<LegacyPluginToastDecision> => {
      if (event.type !== "session.created" || fired || extractParentId(event.properties)) return undefined
      fired = true
      return resolveLegacyPluginToastDecision(getInput())
    },
  }
}

const SYSTEM_DIRECTIVE_PREFIX = "[SYSTEM DIRECTIVE:"
export const PLANNING_CONSULT_WARNING = `

---

[SYSTEM DIRECTIVE: PROMETHEUS READ ONLY]

You are being invoked by Prometheus, a planning agent restricted to .omo/*.md plan files only.

**CRITICAL CONSTRAINTS:**
- DO NOT modify any files (no Write, Edit, or any file mutations)
- DO NOT execute commands that change system state
- DO NOT create, delete, or rename files
- ONLY provide analysis, recommendations, and information

**YOUR ROLE**: Provide consultation, research, and analysis to assist with planning.
Return your findings and recommendations. The actual implementation will be handled separately after planning is complete.

---

`

export const PROMETHEUS_WORKFLOW_REMINDER = `

---

[SYSTEM DIRECTIVE: PROMETHEUS READ ONLY]

## PROMETHEUS MANDATORY WORKFLOW REMINDER

**You are writing a work plan. STOP AND VERIFY you completed ALL steps:**

**DID YOU COMPLETE STEPS 1-2 BEFORE WRITING THIS PLAN?**
**AFTER WRITING, WILL YOU DO STEPS 4-5?**

If you skipped steps, STOP NOW. Go back and complete them.

---

`

export const NOTEPAD_DIRECTIVE = `
<Work_Context>
## Notepad Location (for recording learnings)
NOTEPAD PATH: .omo/notepads/{plan-name}/
- learnings.md: Record patterns, conventions, successful approaches
- issues.md: Record problems, blockers, gotchas encountered
- decisions.md: Record architectural choices and rationales
- problems.md: Record unresolved issues, technical debt

You SHOULD append findings to notepad files after completing work.
IMPORTANT: Always APPEND to notepad files - never overwrite or use Edit tool.

## Plan Location (subagent: READ ONLY)
PLAN PATH: .omo/plans/{plan-name}.md

SUBAGENT PLAN RESTRICTION (applies to YOU, the delegated worker — NOT to the Orchestrator):
- You may READ the plan to understand your assigned tasks
- You may READ checkbox items to know what to work on
- You MUST NOT edit the plan file or mark checkboxes — that is the Orchestrator's job
- The Orchestrator (Atlas) updates checkboxes after verifying your completed work
</Work_Context>
`

export function isPrometheusAgent(agentName: string | undefined): boolean {
  return agentName?.toLowerCase().includes("prometheus") ?? false
}

export function isPrometheusAllowedFile(filePath: string, workspaceRoot: string): boolean {
  const resolvedPath = resolve(workspaceRoot, filePath)
  const relativePath = relative(workspaceRoot, resolvedPath)
  return !relativePath.startsWith("..") && !isAbsolute(relativePath) && /(^|[/\\])\.omo([/\\]|$)/i.test(relativePath) && resolvedPath.toLowerCase().endsWith(".md")
}

export function createPrometheusMdOnlyHook(options: { workspaceRoot: string; agentName?: string }) {
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput, output: ToolBeforeHookOutput & { message?: string }): Promise<void> => {
      if (!isPrometheusAgent(options.agentName)) return
      const toolName = input.tool ?? ""
      if (toolName === "task" || toolName === "call_omo_agent") {
        const prompt = typeof output.args.prompt === "string" ? output.args.prompt : undefined
        if (prompt && !prompt.includes(SYSTEM_DIRECTIVE_PREFIX)) replaceToolArgs(output, { prompt: PLANNING_CONSULT_WARNING + prompt })
        return
      }
      if (!["Write", "Edit", "write", "edit"].includes(toolName)) return
      const filePath = getWritePath(output.args)
      if (!filePath) return
      if (!isPrometheusAllowedFile(filePath, options.workspaceRoot)) throw new Error(`[prometheus-md-only] Prometheus is a planning agent. File operations restricted to .omo/*.md plan files only. Use task() to delegate implementation. Attempted to modify: ${filePath}. APOLOGIZE TO THE USER, REMIND OF YOUR PLAN WRITING PROCESSES, TELL USER WHAT YOU WILL GOING TO DO AS THE PROCESS, WRITE THE PLAN`)
      const normalizedPath = filePath.toLowerCase().replace(/\\/g, "/")
      if (normalizedPath.includes(".omo/plans/")) output.message = (output.message ?? "") + PROMETHEUS_WORKFLOW_REMINDER
    },
  }
}

export function addSisyphusJuniorNotepadDirective(prompt: string): string {
  return prompt.includes(SYSTEM_DIRECTIVE_PREFIX) ? prompt : NOTEPAD_DIRECTIVE + prompt
}

export function createSisyphusJuniorNotepadHook(options: { isCallerOrchestrator: boolean }) {
  return {
    "tool.execute.before": async (input: ToolBeforeHookInput, output: ToolBeforeHookOutput): Promise<void> => {
      if (input.tool !== "task" || !options.isCallerOrchestrator) return
      const prompt = typeof output.args.prompt === "string" ? output.args.prompt : undefined
      if (prompt) replaceToolArgs(output, { prompt: addSisyphusJuniorNotepadDirective(prompt) })
    },
  }
}

function hasQuestions(args: Record<string, unknown>): args is Record<string, unknown> & AskUserQuestionArgs {
  return Array.isArray(args.questions)
}

function isSignedThinkingPart(part: MessagePart): boolean {
  return (part.type === "thinking" || part.type === "redacted_thinking") && typeof part.signature === "string" && part.signature.length > 0 && part.synthetic !== true
}

function hasContentParts(parts: MessagePart[]): boolean {
  return parts.some((part) => part.type === "tool" || part.type === "tool_use" || part.type === "text")
}

function startsWithThinkingBlock(parts: MessagePart[]): boolean {
  const firstPart = parts[0]
  return firstPart?.type === "thinking" || firstPart?.type === "redacted_thinking" || firstPart?.type === "reasoning"
}

function findPreviousThinkingPart(messages: MessageWithParts[], currentIndex: number): MessagePart | undefined {
  for (let index = currentIndex - 1; index >= 0; index--) {
    const message = messages[index]
    if (message.info.role !== "assistant") continue
    const thinkingPart = message.parts.find(isSignedThinkingPart)
    if (thinkingPart) return thinkingPart
  }
  return undefined
}

function extractModelName(model: string): string {
  return model.includes("/") ? (model.split("/").pop() ?? model) : model
}

function normalizeAnthropicModelID(modelID: string): string {
  return extractModelName(modelID).toLowerCase().replace(/\./g, "-")
}

function getAgentConfigKey(agent: string): string {
  return agent.trim().toLowerCase().replaceAll("_", "-").replaceAll(" ", "-")
}

function getNativeSisyphusGptVariant(model: { providerID: string; modelID: string }): string | undefined {
  if (model.modelID === "gpt-5.5" || model.modelID.endsWith("/gpt-5.5")) return "medium"
  return undefined
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")
}

function isCategoryReminderTargetAgent(agent: string | undefined): boolean {
  const key = getAgentConfigKey(agent ?? "")
  return key === "sisyphus" || key === "sisyphus-junior" || key === "atlas" || key.includes("sisyphus") || key.includes("atlas")
}

function selectMostCommonPathClassification(entries: FsyncSkipEntry[]): PathClassification {
  const counts = new Map<PathClassification, number>()
  for (const entry of entries) counts.set(entry.pathClassification, (counts.get(entry.pathClassification) ?? 0) + 1)
  let selected: PathClassification = "unknown"
  let selectedCount = -1
  for (const [classification, count] of counts) {
    if (count > selectedCount) {
      selected = classification
      selectedCount = count
    }
  }
  return selected
}

function extractParentId(properties: unknown): string | undefined {
  if (!properties || typeof properties !== "object" || Array.isArray(properties)) return undefined
  const info = (properties as Record<string, unknown>).info
  if (!info || typeof info !== "object" || Array.isArray(info)) return undefined
  const parentID = (info as Record<string, unknown>).parentID
  return typeof parentID === "string" && parentID.length > 0 ? parentID : undefined
}

function tokenizeTmuxCommand(command: string): string[] {
  const tokens: string[] = []
  let current = ""
  let quote: string | undefined
  let escaped = false
  for (const char of command) {
    if (escaped) {
      current += char
      escaped = false
    } else if (char === "\\") {
      escaped = true
    } else if ((char === "'" || char === '"') && quote === undefined) {
      quote = char
    } else if (char === quote) {
      quote = undefined
    } else if (char === " " && quote === undefined) {
      if (current) {
        tokens.push(current)
        current = ""
      }
    } else {
      current += char
    }
  }
  if (current) tokens.push(current)
  return tokens
}

function findTmuxSubcommand(tokens: string[]): string {
  const optionsWithArgs = new Set(["-L", "-S", "-f", "-c", "-T"])
  for (let index = 0; index < tokens.length;) {
    const token = tokens[index]
    if (token === "--") return tokens[index + 1] ?? ""
    if (optionsWithArgs.has(token)) {
      index += 2
    } else if (token.startsWith("-")) {
      index++
    } else {
      return token
    }
  }
  return ""
}

function extractTmuxSessionName(tokens: string[], subCommand: string): string | null {
  const flag = subCommand === "new-session" ? (findFlagValue(tokens, "-s") ?? findFlagValue(tokens, "-t")) : findFlagValue(tokens, "-t")
  return flag ? flag.split(":")[0].split(".")[0] : null
}

function findFlagValue(tokens: string[], flag: string): string | null {
  const index = tokens.indexOf(flag)
  return index >= 0 ? (tokens[index + 1] ?? null) : null
}

function extractRealPromptText(parts: Array<{ type: string; text?: string; synthetic?: boolean }>): string {
  return parts.filter((part) => part.type === "text" && part.synthetic !== true).map((part) => part.text || "").join(" ")
}

function getUltraworkDirective(agentName?: string, modelID?: string): string {
  if (agentName === "prometheus" || agentName === "plan") return '<ultrawork-mode>\nPlanner ultrawork mode activated. Say "ULTRAWORK MODE ENABLED!" first.\n</ultrawork-mode>'
  if (modelID?.toLowerCase().includes("gpt")) return '<ultrawork-mode>\n**MANDATORY**: You MUST say "ULTRAWORK MODE ENABLED!" to the user as your first response when this mode activates.\n[CODE RED] Maximum precision required.\n</ultrawork-mode>'
  if (modelID?.toLowerCase().includes("gemini")) return '<ultrawork-mode>\n**MANDATORY**: Say "ULTRAWORK MODE ENABLED!" first. Gemini ultrawork protocol active.\n</ultrawork-mode>'
  return '<ultrawork-mode>\n**MANDATORY**: You MUST say "ULTRAWORK MODE ENABLED!" to the user as your first response when this mode activates.\n</ultrawork-mode>'
}

function isHashlineTextFile(firstLine: string): boolean {
  return COLON_READ_LINE_PATTERN.test(firstLine) || PIPE_READ_LINE_PATTERN.test(firstLine)
}

function parseHashlineReadLine(line: string): { lineNumber: number; content: string } | null {
  const colonMatch = COLON_READ_LINE_PATTERN.exec(line)
  if (colonMatch) return { lineNumber: Number.parseInt(colonMatch[1], 10), content: colonMatch[2] }
  const pipeMatch = PIPE_READ_LINE_PATTERN.exec(line)
  return pipeMatch ? { lineNumber: Number.parseInt(pipeMatch[1], 10), content: pipeMatch[2] } : null
}

function transformHashlineLines(lines: string[]): string[] {
  const result: string[] = []
  for (const line of lines) {
    const parsed = parseHashlineReadLine(line)
    if (!parsed) {
      result.push(...lines.slice(result.length))
      break
    }
    result.push(parsed.content.endsWith(OPENCODE_LINE_TRUNCATION_SUFFIX) ? line : formatHashLine(parsed.lineNumber, parsed.content))
  }
  return result
}

function extractMetadataLineCount(metadata: unknown): number | undefined {
  if (!metadata || typeof metadata !== "object") return undefined
  for (const value of [(metadata as Record<string, unknown>).lineCount, (metadata as Record<string, unknown>).linesWritten, (metadata as Record<string, unknown>).lines]) {
    if (typeof value === "number" && Number.isInteger(value) && value >= 0) return value
  }
  return undefined
}

function trimSet<T>(set: Set<T>, max: number): void {
  while (set.size > max) {
    const next = set.values().next()
    if (next.done) return
    set.delete(next.value)
  }
}

function trimMap<K, V>(map: Map<K, V>, max: number): void {
  while (map.size > max) {
    const next = map.keys().next()
    if (next.done) return
    map.delete(next.value)
  }
}

function getRecoveryErrorMessage(error: unknown): string {
  if (!error) return ""
  if (typeof error === "string") return error.toLowerCase()
  const errorObj = error as Record<string, unknown>
  const paths = [errorObj.data, errorObj.error, errorObj, (errorObj.data as Record<string, unknown> | undefined)?.error]
  for (const obj of paths) {
    if (obj && typeof obj === "object") {
      const message = (obj as Record<string, unknown>).message
      if (typeof message === "string" && message.length > 0) return message.toLowerCase()
    }
  }
  try {
    return JSON.stringify(error).toLowerCase()
  } catch {
    return ""
  }
}

function collapseWhitespace(text: string): string { return text.split(/\r?\n/g).map((line) => line.trim()).filter(Boolean).join(" ") }
function getLastNonEmptyLine(text: string): string { return text.split(/\r?\n/g).map((line) => line.trim()).filter(Boolean).at(-1) ?? "" }
function extractStatusCodeFromObject(error: unknown): number | undefined { if (!error || typeof error !== "object") return undefined; const object = error as Record<string, unknown>; return [object.statusCode, object.status, (object.data as Record<string, unknown> | undefined)?.statusCode, (object.error as Record<string, unknown> | undefined)?.statusCode, (object.cause as Record<string, unknown> | undefined)?.statusCode].find((code): code is number => typeof code === "number") }
function extractRuntimeFallbackErrorName(error: unknown): string | undefined { if (!error || typeof error !== "object") return undefined; const object = error as Record<string, unknown>; for (const value of [object.name, (object.data as Record<string, unknown> | undefined)?.name, (object.error as Record<string, unknown> | undefined)?.name, ((object.data as Record<string, unknown> | undefined)?.error as Record<string, unknown> | undefined)?.name]) if (typeof value === "string" && value.length > 0) return value; return undefined }

function collectTokenLimitTextSources(error: unknown): string[] {
  if (typeof error === "string") return [error]
  if (!error || typeof error !== "object") return []
  const object = error as Record<string, unknown>
  const data = object.data as Record<string, unknown> | undefined
  const nestedError = (object.error as Record<string, unknown> | undefined)?.error as Record<string, unknown> | undefined
  const candidates = [data?.responseBody, object.message, (object.error as Record<string, unknown> | undefined)?.message, object.body, object.details, object.reason, object.description, nestedError?.message, data?.message, data?.error]
  const textSources = candidates.filter((candidate): candidate is string => typeof candidate === "string")
  if (textSources.length > 0) return textSources
  try {
    const serialized = JSON.stringify(object)
    return isTokenLimitErrorText(serialized) ? [serialized] : []
  } catch {
    return []
  }
}

function extractTokensFromLimitMessage(message: string): { currentTokens: number; maxTokens: number } | null {
  for (const pattern of TOKEN_LIMIT_PATTERNS) {
    const match = message.match(pattern)
    if (!match) continue
    const first = Number.parseInt(match[1], 10)
    const second = Number.parseInt(match[2], 10)
    return first > second ? { currentTokens: first, maxTokens: second } : { currentTokens: second, maxTokens: first }
  }
  return null
}

function extractTokenLimitMessageIndex(text: string): number | undefined {
  const match = text.match(/messages\.(\d+)/)
  return match ? Number.parseInt(match[1], 10) : undefined
}

function extractRequestId(text: string): string | undefined {
  const match = text.match(/"request_id"\s*:\s*"([^"]+)"/)
  return match?.[1]
}

function findLastUserMessageIndex(messages: MessageWithParts[]): number {
  for (let index = messages.length - 1; index >= 0; index--) {
    if (messages[index]?.info.role === "user") return index
  }
  return -1
}

function resolveMessageSessionID(messages: MessageWithParts[]): string | undefined {
  for (let index = messages.length - 1; index >= 0; index--) {
    const sessionID = messages[index]?.info.sessionID
    if (typeof sessionID === "string" && sessionID.length > 0) return sessionID
  }
  return undefined
}

function createSyntheticUserMessage(sessionID: string, content: string): MessageWithParts {
  return { info: { role: "user", sessionID }, parts: [{ type: "text", text: content, synthetic: true }] }
}

function isAtlasBootstrapTodo(todo: TodoSnapshot): boolean {
  return todo.id === "orchestrate-plan" || todo.id === "pass-final-wave" || todo.content === "Complete ALL implementation tasks" || todo.content === "Pass Final Verification Wave - ALL reviewers APPROVE"
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null
}

function xxHash32(input: string, seed: number): number {
  const data = new TextEncoder().encode(input)
  let offset = 0
  let hash: number
  if (data.length >= 16) {
    const limit = data.length - 16
    let value1 = (seed + 0x9e3779b1 + 0x85ebca77) >>> 0
    let value2 = (seed + 0x85ebca77) >>> 0
    let value3 = seed >>> 0
    let value4 = (seed - 0x9e3779b1) >>> 0
    while (offset <= limit) {
      value1 = round32(value1, readUint32LittleEndian(data, offset)); offset += 4
      value2 = round32(value2, readUint32LittleEndian(data, offset)); offset += 4
      value3 = round32(value3, readUint32LittleEndian(data, offset)); offset += 4
      value4 = round32(value4, readUint32LittleEndian(data, offset)); offset += 4
    }
    hash = (rotateLeft32(value1, 1) + rotateLeft32(value2, 7)) >>> 0
    hash = (hash + rotateLeft32(value3, 12)) >>> 0
    hash = (hash + rotateLeft32(value4, 18)) >>> 0
  } else {
    hash = (seed + 0x165667b1) >>> 0
  }
  hash = (hash + data.length) >>> 0
  while (offset + 4 <= data.length) {
    hash = (hash + Math.imul(readUint32LittleEndian(data, offset), 0xc2b2ae3d)) >>> 0
    hash = Math.imul(rotateLeft32(hash, 17), 0x27d4eb2f) >>> 0
    offset += 4
  }
  while (offset < data.length) {
    hash = (hash + Math.imul(data[offset] ?? 0, 0x165667b1)) >>> 0
    hash = Math.imul(rotateLeft32(hash, 11), 0x9e3779b1) >>> 0
    offset++
  }
  hash = Math.imul((hash ^ (hash >>> 15)) >>> 0, 0x85ebca77) >>> 0
  hash = Math.imul((hash ^ (hash >>> 13)) >>> 0, 0xc2b2ae3d) >>> 0
  return (hash ^ (hash >>> 16)) >>> 0
}

function round32(accumulator: number, value: number): number {
  return Math.imul(rotateLeft32((accumulator + Math.imul(value, 0x85ebca77)) >>> 0, 13), 0x9e3779b1) >>> 0
}

function rotateLeft32(value: number, bits: number): number {
  return ((value << bits) | (value >>> (32 - bits))) >>> 0
}

function readUint32LittleEndian(input: Uint8Array, offset: number): number {
  return ((input[offset] ?? 0) | ((input[offset + 1] ?? 0) << 8) | ((input[offset + 2] ?? 0) << 16) | ((input[offset + 3] ?? 0) << 24)) >>> 0
}

export function listOmoHooks(): OmoHookDefinition[] {
  return HOOKS.map(cloneHook)
}

export function getOmoHook(name: string): OmoHookDefinition | undefined {
  const hook = HOOKS.find((candidate) => candidate.name === name)
  return hook ? cloneHook(hook) : undefined
}

export function listOmoHooksByStatus(status: OmoHookStatus): OmoHookDefinition[] {
  return HOOKS.filter((hook) => hook.status === status).map(cloneHook)
}

export function listOmoHooksByWave(wave: OmoHookWave): OmoHookDefinition[] {
  return HOOKS.filter((hook) => hook.wave === wave).map(cloneHook)
}

export function listOmoHooksByExitPath(exitPath: OmoHookExitPath): OmoHookDefinition[] {
  return HOOKS.filter((hook) => hook.exitPath === exitPath).map(cloneHook)
}

export function summarizeOmoHookPorting(): Record<OmoHookStatus, number> {
  return HOOKS.reduce<Record<OmoHookStatus, number>>((summary, hook) => {
    summary[hook.status] += 1
    return summary
  }, { "behavior-mapped": 0, "adapter-bound": 0, missing: 0 })
}

function hook(name: string, originalExport: string, domain: string, status: OmoHookStatus, options: HookOptions = {}): OmoHookDefinition {
  return {
    name,
    originalExport,
    domain,
    status,
    standalonePackage: options.standalonePackage,
    originalSource: options.originalSource ?? `src/hooks/${name}/${options.sourceFile ?? "hook.ts"}`,
    exitPath: resolveOmoHookExitPath(status, domain),
    targetPackage: options.standalonePackage ?? resolveOmoHookTargetPackage(status, domain),
    wave: resolveWave(domain),
    testTypes: resolveOmoHookTestTypes(status),
    adapterImpact: resolveAdapterImpact(status, domain),
  }
}

function cloneHook(hook: OmoHookDefinition): OmoHookDefinition {
  return { ...hook, testTypes: [...hook.testTypes] }
}

export function resolveOmoHookExitPath(status: OmoHookStatus, domain: string): OmoHookExitPath {
  if (status === "adapter-bound") return "adapter-bound"
  if (status === "behavior-mapped") return "pure-domain-port"
  if (domain === "workflow" || domain === "plugin-loader" || domain === "terminal" || domain === "environment") return "limited-redesign"
  if (domain === "notification" || domain === "maintenance") return "limited-redesign"
  return "pure-domain-port"
}

export function resolveOmoHookTargetPackage(status: OmoHookStatus, domain: string): string {
  if (status === "adapter-bound") return "@oh-my-opencode/adapter-opencode"
  if (domain === "model") return "@oh-my-opencode/model-core"
  if (domain === "context") return "@oh-my-opencode/agents-md-core"
  if (domain === "loop") return "@oh-my-opencode/ulw-kernel"
  return "@oh-my-opencode/hooks-core"
}

function resolveWave(domain: string): OmoHookWave {
  if (domain === "guard" || domain === "prompting" || domain === "model" || domain === "validation" || domain === "quality") return "phase-1-safety"
  if (domain === "context-window" || domain === "recovery" || domain === "tool-output" || domain === "runtime") return "phase-2-recovery"
  if (domain === "loop" || domain === "task" || domain === "team" || domain === "workflow" || domain === "todo" || domain === "commands") return "phase-3-orchestration"
  if (domain === "notification" || domain === "environment" || domain === "terminal" || domain === "maintenance" || domain === "agent") return "phase-4-host"
  return "phase-5-adapter-convergence"
}

export function resolveOmoHookTestTypes(status: OmoHookStatus): OmoHookTestType[] {
  if (status === "adapter-bound") return ["adapter", "integration", "manual-qa"]
  if (status === "behavior-mapped") return ["unit", "parity", "manual-qa"]
  return ["unit", "parity"]
}

function resolveAdapterImpact(status: OmoHookStatus, domain: string): OmoHookDefinition["adapterImpact"] {
  if (status === "adapter-bound") return "high"
  if (domain === "notification" || domain === "terminal" || domain === "environment" || domain === "workflow" || domain === "plugin-loader") return "high"
  if (domain === "team" || domain === "task" || domain === "loop" || domain === "runtime") return "medium"
  return status === "behavior-mapped" ? "none" : "low"
}
