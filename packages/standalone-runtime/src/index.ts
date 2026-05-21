import { listOmoHooks, summarizeOmoHookPorting, type OmoHookDefinition } from "@oh-my-opencode/hooks-core"
import { createBuiltinSkills, type BuiltinSkill } from "@oh-my-opencode/skills-core"
import type { UlwHost, UlwMessage, UlwPromptRequest, UlwSessionEvent } from "@oh-my-opencode/ulw-host-contract"
import { createUlwLoopEngine, runTrackedUlw, type UlwLoopEngine } from "@oh-my-opencode/ulw-kernel"
import { createMemoryUlwLoopStateStore, createUlwLoopStateController, type UlwLoopStateController } from "@oh-my-opencode/ulw-loop-state"

export type StandaloneOmoRuntime = {
  host: UlwHost
  loopState: UlwLoopStateController
  engine: UlwLoopEngine
  skills: BuiltinSkill[]
  hooks: OmoHookDefinition[]
  dispatchedPrompts: UlwPromptRequest[]
  submitUserMessage(input: { sessionID: string; text: string }): Promise<void>
  appendAssistantMessage(sessionID: string, text: string): void
  emitIdle(sessionID: string): Promise<void>
  readMessages(sessionID: string): UlwMessage[]
  stop(): void
}

export function createStandaloneOmoRuntime(): StandaloneOmoRuntime {
  const messagesBySession = new Map<string, UlwMessage[]>()
  const listeners = new Set<(event: UlwSessionEvent) => void>()
  const dispatchedPrompts: UlwPromptRequest[] = []
  const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
  const skills = createBuiltinSkills({ teamModeEnabled: true })
  const hooks = listOmoHooks()

  const host: UlwHost = {
    async dispatchPrompt(request) {
      dispatchedPrompts.push(request)
      appendMessage(messagesBySession, request.sessionID, { role: "user", text: request.message })
      return { accepted: true, sessionID: request.sessionID, dispatchID: `runtime-dispatch-${dispatchedPrompts.length}` }
    },
    async readMessages(sessionID) {
      return messagesBySession.get(sessionID) ?? []
    },
    async readTodos() {
      return []
    },
    async readStatus() {
      return "idle"
    },
    async abort() {},
    onEvent(listener) {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
  }

  const engine = createUlwLoopEngine({ host, loopState })
  return {
    host,
    loopState,
    engine,
    skills,
    hooks,
    dispatchedPrompts,
    async submitUserMessage(input) {
      appendMessage(messagesBySession, input.sessionID, { role: "user", text: input.text })
      await runTrackedUlw({ host, loopState, sessionID: input.sessionID, text: input.text })
    },
    appendAssistantMessage(sessionID, text) {
      appendMessage(messagesBySession, sessionID, { role: "assistant", text })
    },
    async emitIdle(sessionID) {
      for (const listener of listeners) listener({ type: "idle", sessionID })
      await flushEventHandlers()
    },
    readMessages(sessionID) {
      return messagesBySession.get(sessionID) ?? []
    },
    stop() {
      engine.stop()
    },
  }
}

function appendMessage(messagesBySession: Map<string, UlwMessage[]>, sessionID: string, message: UlwMessage): void {
  messagesBySession.set(sessionID, [...messagesBySession.get(sessionID) ?? [], message])
}

async function flushEventHandlers(): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 0))
}

export async function runStandaloneOmo(): Promise<{ prompts: string[]; finalState: unknown; skillNames: string[]; hookSummary: Record<string, number> }> {
  const runtime = createStandaloneOmoRuntime()
  await runtime.submitUserMessage({ sessionID: "runtime-session", text: "ulw build standalone" })
  await runtime.emitIdle("runtime-session")
  runtime.appendAssistantMessage("runtime-session", "<promise>DONE</promise>")
  await runtime.emitIdle("runtime-session")
  runtime.appendAssistantMessage("runtime-session", "<promise>VERIFIED</promise>")
  await runtime.emitIdle("runtime-session")
  runtime.stop()
  return {
    prompts: runtime.dispatchedPrompts.map((prompt) => prompt.message),
    finalState: runtime.loopState.getState(),
    skillNames: runtime.skills.map((skill) => skill.name),
    hookSummary: summarizeOmoHookPorting(),
  }
}
