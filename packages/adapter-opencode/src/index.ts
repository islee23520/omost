import type { UlwHost, UlwMessage, UlwSessionEvent, UlwTodo } from "@oh-my-opencode/ulw-host-contract"
import { createFileUlwLoopStateStore, createUlwLoopStateController, type UlwLoopStateController } from "@oh-my-opencode/ulw-loop-state"
import { createUlwLoopEngine, runTrackedUlw, type UlwLoopEngine } from "@oh-my-opencode/ulw-kernel"
import { resolveModelAgentGuard, type ChatMessageHookInput, type ChatMessageHookOutput, type ModelAgentGuardDecision } from "@oh-my-opencode/hooks-core"

export type OpenCodeAdapterClient = {
  session: {
    prompt?: (input: OpenCodePromptInput) => Promise<unknown>
    promptAsync?: (input: OpenCodePromptInput) => Promise<unknown>
    messages(input: { path: { id: string }; query?: { directory?: string } }): Promise<unknown>
    abort(input: { path: { id: string } }): Promise<unknown>
    status?: (input: { path: { id: string }; query?: { directory?: string } }) => Promise<unknown>
    todos?: (input: { path: { id: string }; query?: { directory?: string } }) => Promise<unknown>
  }
}

export type OpenCodePromptInput = {
  path: { id: string }
  body: { parts: Array<{ type: "text"; text: string }>; agent?: string; modelID?: string }
  query?: { directory?: string }
}

export type OpenCodeAdapterOptions = {
  client: OpenCodeAdapterClient
  directory?: string
  subscribe?: (listener: (event: UlwSessionEvent) => void) => () => void
  showToast?: (toast: NonNullable<ModelAgentGuardDecision["toast"]> & { sessionID: string }) => Promise<void> | void
  updateSessionAgent?: (input: { sessionID: string; agent: string }) => Promise<void> | void
}

export type OpenCodeOmoPluginAdapter = {
  host: UlwHost
  loopState: UlwLoopStateController
  engine: UlwLoopEngine
  handleUserMessage(input: { sessionID: string; text: string; agentName?: string; modelID?: string }): Promise<void>
  handleChatMessage(input: ChatMessageHookInput, output?: ChatMessageHookOutput): Promise<ModelAgentGuardDecision>
  stop(): void
}

export type OpenCodeOmoPluginAdapterOptions = OpenCodeAdapterOptions & {
  statePath?: string
  loopState?: UlwLoopStateController
}

export function createOpenCodeUlwHost(options: OpenCodeAdapterOptions): UlwHost {
  return {
    async dispatchPrompt(request) {
      const prompt = options.client.session.promptAsync ?? options.client.session.prompt
      if (!prompt) return { accepted: false, sessionID: request.sessionID, dispatchID: request.sessionID }
      const response = await prompt({
        path: { id: request.sessionID },
        body: {
          parts: [{ type: "text", text: request.message }],
          ...(request.agentName ? { agent: request.agentName } : {}),
          ...(request.modelID ? { modelID: request.modelID } : {}),
        },
        query: options.directory ? { directory: options.directory } : undefined,
      })
      return { accepted: promptAccepted(response), sessionID: request.sessionID, dispatchID: extractDispatchID(response) ?? request.sessionID }
    },
    async readMessages(sessionID) {
      return normalizeMessages(await options.client.session.messages({
        path: { id: sessionID },
        query: options.directory ? { directory: options.directory } : undefined,
      }))
    },
    async readTodos(sessionID) {
      const todos = await options.client.session.todos?.({
        path: { id: sessionID },
        query: options.directory ? { directory: options.directory } : undefined,
      })
      return normalizeTodos(todos)
    },
    async readStatus(sessionID) {
      const status = await options.client.session.status?.({
        path: { id: sessionID },
        query: options.directory ? { directory: options.directory } : undefined,
      })
      return normalizeStatus(status)
    },
    async abort(sessionID) {
      await options.client.session.abort({ path: { id: sessionID } })
    },
    onEvent(listener) {
      return options.subscribe?.(listener) ?? (() => {})
    },
  }
}

export function createOpenCodeOmoPluginAdapter(options: OpenCodeOmoPluginAdapterOptions): OpenCodeOmoPluginAdapter {
  if (!options.directory && !options.loopState) throw new Error("OpenCode OMO plugin adapter requires directory or loopState")
  const host = createOpenCodeUlwHost(options)
  const loopState = options.loopState ?? createUlwLoopStateController(createFileUlwLoopStateStore(options.directory!, options.statePath))
  const engine = createUlwLoopEngine({ host, loopState })
  return {
    host,
    loopState,
    engine,
    async handleUserMessage(input) {
      await runTrackedUlw({ host, loopState, sessionID: input.sessionID, text: input.text, agentName: input.agentName, modelID: input.modelID })
    },
    async handleChatMessage(input, output) {
      return applyOpenCodeModelAgentGuard(options, input, output)
    },
    stop() {
      engine.stop()
    },
  }
}

export async function applyOpenCodeModelAgentGuard(options: OpenCodeAdapterOptions, input: ChatMessageHookInput, output?: ChatMessageHookOutput): Promise<ModelAgentGuardDecision> {
  const decision = resolveModelAgentGuard(input.agent, input.model)
  if (decision.agent !== undefined) input.agent = decision.agent
  if (decision.outputAgent !== undefined && output?.message) output.message.agent = decision.outputAgent
  if (decision.variant !== undefined && output?.message && output.message.variant === undefined) output.message.variant = decision.variant
  if (decision.toast) await options.showToast?.({ ...decision.toast, sessionID: input.sessionID })
  if (decision.sessionAgent) await options.updateSessionAgent?.({ sessionID: input.sessionID, agent: decision.sessionAgent })
  return decision
}

function normalizeMessages(response: unknown): UlwMessage[] {
  return getArrayData(response).flatMap((message) => {
    if (!isRecord(message)) return []
    const role = normalizeRole(getNestedString(message, "info", "role") ?? getString(message, "role"))
    const text = collectMessageText(message)
    return role && text ? [{ role, text }] : []
  })
}

function normalizeTodos(response: unknown): UlwTodo[] {
  return getArrayData(response).flatMap((todo) => {
    if (!isRecord(todo)) return []
    const content = getString(todo, "content") ?? getString(todo, "title")
    const status = normalizeTodoStatus(getString(todo, "status"))
    return content && status ? [{ content, status }] : []
  })
}

function normalizeStatus(response: unknown): string {
  if (typeof response === "string") return response
  if (isRecord(response)) return getString(response, "status") ?? getNestedString(response, "data", "status") ?? "unknown"
  return "unknown"
}

function collectMessageText(message: Record<string, unknown>): string {
  const text = getString(message, "text") ?? getString(message, "content")
  if (text) return text
  const parts = Array.isArray(message.parts) ? message.parts : []
  return parts.flatMap((part) => isRecord(part) ? [getString(part, "text") ?? ""] : []).filter(Boolean).join("\n")
}

function getArrayData(response: unknown): unknown[] {
  if (Array.isArray(response)) return response
  if (isRecord(response) && Array.isArray(response.data)) return response.data
  return []
}

function hasError(response: unknown): boolean {
  return isRecord(response) && response.error !== undefined && response.error !== null
}

function promptAccepted(response: unknown): boolean {
  if (hasError(response)) return false
  if (!isRecord(response)) return true
  const status = getString(response, "status")
  return status === undefined || status === "dispatched"
}

function extractDispatchID(response: unknown): string | undefined {
  if (!isRecord(response)) return undefined
  return getString(response, "id") ?? getString(response, "messageID") ?? getNestedString(response, "data", "id")
}

function normalizeRole(role: string | undefined): UlwMessage["role"] | undefined {
  if (role === "user" || role === "assistant" || role === "system" || role === "tool") return role
  return undefined
}

function normalizeTodoStatus(status: string | undefined): UlwTodo["status"] | undefined {
  if (status === "pending" || status === "in_progress" || status === "completed" || status === "cancelled") return status
  return undefined
}

function getString(record: Record<string, unknown>, key: string): string | undefined {
  const value = record[key]
  return typeof value === "string" && value.length > 0 ? value : undefined
}

function getNestedString(record: Record<string, unknown>, key: string, nestedKey: string): string | undefined {
  const nested = record[key]
  return isRecord(nested) ? getString(nested, nestedKey) : undefined
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null
}
