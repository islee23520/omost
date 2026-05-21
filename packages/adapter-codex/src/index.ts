import type { UlwHost, UlwMessage, UlwPromptRequest, UlwPromptReceipt } from "@oh-my-opencode/ulw-host-contract"
import { createMemoryUlwLoopStateStore, createUlwLoopStateController, type UlwLoopStateController } from "@oh-my-opencode/ulw-loop-state"
import { createUlwLoopEngine, runTrackedUlw, type UlwLoopEngine } from "@oh-my-opencode/ulw-kernel"

export type CodexClient = {
  conversation: {
    send(input: CodexSendInput): Promise<CodexSendResult>
    transcript(input: CodexConversationInput): Promise<CodexTranscriptResult>
    status?: (input: CodexConversationInput) => Promise<CodexStatusResult>
    abort?: (input: CodexConversationInput) => Promise<unknown>
    onEvent?: (listener: (event: CodexConversationEvent) => void) => () => void
  }
}

export type CodexConversationInput = {
  conversationID: string
}

export type CodexSendInput = CodexConversationInput & {
  input: string
  agent?: string
  model?: string
}

export type CodexSendResult = string | {
  id?: string
  itemID?: string
  accepted?: boolean
  status?: "queued" | "running" | "completed" | "failed" | "rejected"
  error?: unknown
}

export type CodexTranscriptResult = CodexTranscriptItem[] | {
  items?: CodexTranscriptItem[]
}

export type CodexTranscriptItem = {
  role?: string
  type?: string
  text?: string
  content?: string | Array<{ type?: string; text?: string }>
}

export type CodexStatusResult = string | {
  status?: string
}

export type CodexConversationEvent = {
  type: "idle" | "started" | "message" | "error"
  conversationID: string
}

export type CodexOmoAdapter = {
  host: UlwHost
  loopState: UlwLoopStateController
  engine: UlwLoopEngine
  handleUserMessage(input: { sessionID: string; text: string; agentName?: string; modelID?: string }): Promise<void>
  stop(): void
}

export type CodexOmoAdapterOptions = {
  client: CodexClient
  loopState?: UlwLoopStateController
}

export function createCodexUlwHost(client: CodexClient): UlwHost {
  return {
    async dispatchPrompt(request) {
      const result = await client.conversation.send({
        conversationID: request.sessionID,
        input: request.message,
        ...(request.agentName ? { agent: request.agentName } : {}),
        ...(request.modelID ? { model: request.modelID } : {}),
      })
      return normalizeCodexSendResult(request, result)
    },
    async readMessages(sessionID) {
      return normalizeCodexMessages(await client.conversation.transcript({ conversationID: sessionID }))
    },
    readTodos() {
      return Promise.resolve([])
    },
    readStatus(sessionID) {
      return client.conversation.status?.({ conversationID: sessionID }).then(normalizeCodexStatus) ?? Promise.resolve("unknown")
    },
    async abort(sessionID) {
      return client.conversation.abort?.({ conversationID: sessionID }).then(() => {}) ?? Promise.resolve()
    },
    onEvent(listener) {
      return client.conversation.onEvent?.((event) => {
        if (event.type === "idle") listener({ type: "idle", sessionID: event.conversationID })
      }) ?? (() => {})
    },
  }
}

export function createCodexOmoAdapter(options: CodexOmoAdapterOptions): CodexOmoAdapter {
  const host = createCodexUlwHost(options.client)
  const loopState = options.loopState ?? createUlwLoopStateController(createMemoryUlwLoopStateStore())
  const engine = createUlwLoopEngine({ host, loopState })
  return {
    host,
    loopState,
    engine,
    async handleUserMessage(input) {
      await runTrackedUlw({ host, loopState, sessionID: input.sessionID, text: input.text, agentName: input.agentName, modelID: input.modelID })
    },
    stop() {
      engine.stop()
    },
  }
}

function normalizeCodexSendResult(request: UlwPromptRequest, result: CodexSendResult): UlwPromptReceipt {
  if (typeof result === "string") return { accepted: true, sessionID: request.sessionID, dispatchID: result }
  const accepted = result.error === undefined && result.status !== "failed" && result.status !== "rejected" && result.accepted !== false
  return { accepted, sessionID: request.sessionID, dispatchID: result.id ?? result.itemID ?? request.sessionID }
}

function normalizeCodexMessages(result: CodexTranscriptResult): UlwMessage[] {
  const items = Array.isArray(result) ? result : result.items ?? []
  return items.flatMap((item) => {
    const role = normalizeRole(item.role)
    const text = collectCodexText(item)
    return role && text ? [{ role, text }] : []
  })
}

function normalizeCodexStatus(result: CodexStatusResult): string {
  if (typeof result === "string") return result
  return result.status ?? "unknown"
}

function collectCodexText(item: CodexTranscriptItem): string {
  if (item.text) return item.text
  if (typeof item.content === "string") return item.content
  if (Array.isArray(item.content)) return item.content.flatMap((part) => part.text ? [part.text] : []).join("\n")
  return ""
}

function normalizeRole(role: string | undefined): UlwMessage["role"] | undefined {
  if (role === "user" || role === "assistant" || role === "system" || role === "tool") return role
  return undefined
}
