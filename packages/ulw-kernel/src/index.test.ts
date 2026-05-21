import { describe, expect, test } from "bun:test"
import type { UlwHost, UlwPromptRequest } from "@oh-my-opencode/ulw-host-contract"
import { createMemoryUlwLoopStateStore, createUlwLoopStateController } from "@oh-my-opencode/ulw-loop-state"
import { createUlwLoopEngine, handleUlwLoopIdle, runTrackedUlw, runUlw } from "./index"

describe("ulw kernel", () => {
  test("dispatches detected ULW prompts through the host contract", async () => {
    const requests: UlwPromptRequest[] = []
    const host: UlwHost = {
      async dispatchPrompt(request) {
        requests.push(request)
        return { accepted: true, sessionID: request.sessionID, dispatchID: `dispatch-${requests.length}` }
      },
      async readMessages() {
        return []
      },
      async readTodos() {
        return []
      },
      async readStatus() {
        return "idle"
      },
      async abort() {},
      onEvent() {
        return () => {}
      },
    }

    const result = await runUlw({ host, sessionID: "ses_1", text: "please ulw" })

    expect(result.dispatched).toBe(true)
    expect(result.intents).toEqual(["ultrawork"])
    expect(requests).toEqual([{ sessionID: "ses_1", message: "ULTRAWORK MODE ENABLED!", agentName: undefined, modelID: undefined }])
  })

  test("starts a tracked ultrawork loop after successful dispatch", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    const requests: UlwPromptRequest[] = []
    const host: UlwHost = {
      async dispatchPrompt(request) {
        requests.push(request)
        return { accepted: true, sessionID: request.sessionID, dispatchID: "dispatch-1" }
      },
      async readMessages() {
        return [{ role: "assistant", text: "old <promise>DONE</promise>" }]
      },
      async readTodos() {
        return []
      },
      async readStatus() {
        return "idle"
      },
      async abort() {},
      onEvent() {
        return () => {}
      },
    }

    const result = await runTrackedUlw({ host, loopState, sessionID: "ses_1", text: "please ulw" })

    expect(result.dispatched).toBe(true)
    expect(loopState.getState()?.maxIterations).toBe(500)
    expect(loopState.getState()?.messageCountAtStart).toBe(1)
    expect(requests).toHaveLength(1)
  })

  test("does not start tracked state when no ultrawork intent dispatches", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    const host: UlwHost = {
      async dispatchPrompt() {
        throw new Error("dispatch should not run")
      },
      async readMessages() {
        return []
      },
      async readTodos() {
        return []
      },
      async readStatus() {
        return "idle"
      },
      async abort() {},
      onEvent() {
        return () => {}
      },
    }

    const result = await runTrackedUlw({ host, loopState, sessionID: "ses_1", text: "hello" })

    expect(result.dispatched).toBe(false)
    expect(loopState.getState()).toBeNull()
  })

  test("does not start tracked state when dispatch is rejected", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    const host: UlwHost = {
      async dispatchPrompt(request) {
        return { accepted: false, sessionID: request.sessionID, dispatchID: "dispatch-1" }
      },
      async readMessages() {
        return []
      },
      async readTodos() {
        return []
      },
      async readStatus() {
        return "idle"
      },
      async abort() {},
      onEvent() {
        return () => {}
      },
    }

    const result = await runTrackedUlw({ host, loopState, sessionID: "ses_1", text: "please ulw" })

    expect(result.dispatched).toBe(false)
    expect(loopState.getState()).toBeNull()
  })

  test("does not start tracked state when only a non-ULW receipt is accepted", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    const host: UlwHost = {
      async dispatchPrompt(request) {
        return {
          accepted: request.message === "HYPERPLAN MODE ENABLED!",
          sessionID: request.sessionID,
          dispatchID: request.message,
        }
      },
      async readMessages() {
        return []
      },
      async readTodos() {
        return []
      },
      async readStatus() {
        return "idle"
      },
      async abort() {},
      onEvent() {
        return () => {}
      },
    }

    const result = await runTrackedUlw({ host, loopState, sessionID: "ses_1", text: "ulw and hyperplan" })

    expect(result.dispatched).toBe(true)
    expect(result.intents).toEqual(["ultrawork", "hyperplan"])
    expect(loopState.getState()).toBeNull()
  })

  test("continues an active loop on idle when completion is absent", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true })
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({ requests })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()?.iteration).toBe(2)
    expect(requests[0]?.message).toContain("RALPH LOOP 2/500")
  })

  test("moves DONE completion into verification pending and dispatches Oracle prompt", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true })
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({ requests, messages: [{ role: "assistant", text: "<promise>DONE</promise>" }] })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()?.verificationPending).toBe(true)
    expect(loopState.getState()?.completionPromise).toBe("VERIFIED")
    expect(requests[0]?.message).toContain("ULTRAWORK LOOP VERIFICATION")
  })

  test("ignores user prompts containing the completion promise", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true })
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({
      requests,
      messages: [{ role: "user", text: "Continue. Output <promise>DONE</promise> when done." }],
    })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()?.verificationPending).toBeUndefined()
    expect(loopState.getState()?.iteration).toBe(2)
    expect(requests[0]?.message).toContain("RALPH LOOP 2/500")
  })

  test("clears the loop when verification succeeds", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true })
    loopState.markVerificationPending("ses_1")
    const host = createTestHost({ messages: [{ role: "assistant", text: "<promise>VERIFIED</promise>" }] })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()).toBeNull()
  })

  test("ignores stale verification promises before the verification baseline", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true, messageCountAtStart: 1 })
    loopState.markVerificationPending("ses_1", 2)
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({
      requests,
      messages: [
        { role: "assistant", text: "<promise>VERIFIED</promise>" },
        { role: "user", text: "verify now" },
      ],
    })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()?.iteration).toBe(2)
    expect(requests[0]?.message).toContain("ULTRAWORK LOOP VERIFICATION FAILED")
  })

  test("continues after failed verification", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true })
    loopState.markVerificationPending("ses_1")
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({ requests, messages: [{ role: "assistant", text: "not verified" }] })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()?.verificationPending).toBeUndefined()
    expect(loopState.getState()?.iteration).toBe(2)
    expect(requests[0]?.message).toContain("ULTRAWORK LOOP VERIFICATION FAILED")
  })

  test("clears pending verification failure at max iteration", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore({
      active: true,
      iteration: 1,
      maxIterations: 1,
      completionPromise: "VERIFIED",
      initialCompletionPromise: "DONE",
      startedAt: "2026-01-01T00:00:00.000Z",
      prompt: "build",
      sessionID: "ses_1",
      strategy: "continue",
      ultrawork: true,
      verificationPending: true,
    }))
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({ requests, messages: [{ role: "assistant", text: "not verified" }] })

    await handleUlwLoopIdle({ host, loopState }, "ses_1")

    expect(loopState.getState()).toBeNull()
    expect(requests).toHaveLength(0)
  })

  test("subscribes to idle events through the host contract", async () => {
    const loopState = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    loopState.start({ sessionID: "ses_1", prompt: "build", ultrawork: true })
    let listener: Parameters<UlwHost["onEvent"]>[0] | undefined
    const requests: UlwPromptRequest[] = []
    const host = createTestHost({
      requests,
      onEvent(nextListener) {
        listener = nextListener
        return () => {
          listener = undefined
        }
      },
    })

    const engine = createUlwLoopEngine({ host, loopState })
    listener?.({ type: "idle", sessionID: "ses_1" })
    await new Promise((resolve) => setTimeout(resolve, 0))
    engine.stop()

    expect(requests[0]?.message).toContain("RALPH LOOP 2/500")
    expect(listener).toBeUndefined()
  })
})

function createTestHost(options: {
  requests?: UlwPromptRequest[]
  messages?: Awaited<ReturnType<UlwHost["readMessages"]>>
  onEvent?: UlwHost["onEvent"]
} = {}): UlwHost {
  return {
    async dispatchPrompt(request) {
      options.requests?.push(request)
      return { accepted: true, sessionID: request.sessionID, dispatchID: `dispatch-${options.requests?.length ?? 1}` }
    },
    async readMessages() {
      return options.messages ?? []
    },
    async readTodos() {
      return []
    },
    async readStatus() {
      return "idle"
    },
    async abort() {},
    onEvent(listener) {
      return options.onEvent?.(listener) ?? (() => {})
    },
  }
}
