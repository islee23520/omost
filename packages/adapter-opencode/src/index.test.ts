import { describe, expect, test } from "bun:test"
import { createMemoryUlwLoopStateStore, createUlwLoopStateController } from "@oh-my-opencode/ulw-loop-state"
import { applyOpenCodeModelAgentGuard, createOpenCodeOmoPluginAdapter, createOpenCodeUlwHost } from "./index"

describe("opencode adapter", () => {
  test("maps OpenCode client prompt calls to UlwHost receipts", async () => {
    const prompts: unknown[] = []
    const host = createOpenCodeUlwHost({
      directory: "/repo",
      client: {
        session: {
          async prompt(input) {
            prompts.push(input)
            return { id: "msg_1" }
          },
          async messages() {
            return []
          },
          async abort() {},
        },
      },
    })

    const receipt = await host.dispatchPrompt({ sessionID: "ses_1", message: "continue", agentName: "build", modelID: "gpt" })
    expect(receipt).toEqual({
      accepted: true,
      sessionID: "ses_1",
      dispatchID: "msg_1",
    })
    expect(prompts).toEqual([{ path: { id: "ses_1" }, body: { parts: [{ type: "text", text: "continue" }], agent: "build", modelID: "gpt" }, query: { directory: "/repo" } }])
  })

  test("prefers OpenCode promptAsync when available", async () => {
    const calls: string[] = []
    const host = createOpenCodeUlwHost({
      client: {
        session: {
          async prompt() {
            calls.push("prompt")
            return { id: "sync" }
          },
          async promptAsync() {
            calls.push("promptAsync")
            return { status: "dispatched", id: "async" }
          },
          async messages() {
            return []
          },
          async abort() {},
        },
      },
    })

    expect(await host.dispatchPrompt({ sessionID: "ses_1", message: "continue" })).toEqual({
      accepted: true,
      sessionID: "ses_1",
      dispatchID: "async",
    })
    expect(calls).toEqual(["promptAsync"])
  })

  test("normalizes OpenCode messages, todos, status, abort, and events", async () => {
    let aborted: string | undefined
    let listener: ((event: { type: "idle"; sessionID: string }) => void) | undefined
    const host = createOpenCodeUlwHost({
      client: {
        session: {
          async prompt() {
            return { error: "busy" }
          },
          async messages() {
            return { data: [{ info: { role: "assistant" }, parts: [{ type: "text", text: "done" }] }] }
          },
          async todos() {
            return [{ title: "ship", status: "pending" }]
          },
          async status() {
            return { data: { status: "idle" } }
          },
          async abort(input) {
            aborted = input.path.id
          },
        },
      },
      subscribe(nextListener) {
        listener = nextListener
        return () => {
          listener = undefined
        }
      },
    })

    expect(await host.dispatchPrompt({ sessionID: "ses_1", message: "continue" })).toMatchObject({ accepted: false })
    expect(await host.readMessages("ses_1")).toEqual([{ role: "assistant", text: "done" }])
    expect(await host.readTodos("ses_1")).toEqual([{ content: "ship", status: "pending" }])
    expect(await host.readStatus("ses_1")).toBe("idle")
    await host.abort("ses_1")
    expect(aborted).toBe("ses_1")

    const events: string[] = []
    const unsubscribe = host.onEvent((event) => events.push(`${event.type}:${event.sessionID}`))
    listener?.({ type: "idle", sessionID: "ses_1" })
    unsubscribe()
    expect(events).toEqual(["idle:ses_1"])
    expect(listener).toBeUndefined()
  })

  test("normalizes OpenCode empty and invalid responses", async () => {
    const host = createOpenCodeUlwHost({
      client: {
        session: {
          async prompt() {
            return "ok"
          },
          async messages() {
            return { data: [{ role: "invalid", text: "ignored" }, { role: "assistant" }] }
          },
          async todos() {
            return { data: [{ title: "ignored", status: "blocked" }] }
          },
          async status() {
            return 1
          },
          async abort() {},
        },
      },
    })

    expect(await host.dispatchPrompt({ sessionID: "ses_1", message: "continue" })).toMatchObject({ accepted: true })
    expect(await host.readMessages("ses_1")).toEqual([])
    expect(await host.readTodos("ses_1")).toEqual([])
    expect(await host.readStatus("ses_1")).toBe("unknown")
  })

  test("normalizes non-array OpenCode collection responses", async () => {
    const host = createOpenCodeUlwHost({
      client: {
        session: {
          async messages() {
            return { data: "not-array" }
          },
          async todos() {
            return { data: "not-array" }
          },
          async abort() {},
        },
      },
    })

    expect(await host.readMessages("ses_1")).toEqual([])
    expect(await host.readTodos("ses_1")).toEqual([])
    const unsubscribe = host.onEvent(() => {})
    expect(unsubscribe).toBeFunction()
    unsubscribe()
  })

  test("drives standalone OMO ULW lifecycle through the OpenCode client", async () => {
    const messages: unknown[] = []
    const prompts: unknown[] = []
    let listener: ((event: { type: "idle"; sessionID: string }) => void) | undefined
    const adapter = createOpenCodeOmoPluginAdapter({
      loopState: createUlwLoopStateController(createMemoryUlwLoopStateStore()),
      client: {
        session: {
          async promptAsync(input) {
            prompts.push(input)
            messages.push({ info: { role: "user" }, parts: input.body.parts })
            return { status: "dispatched", id: `opencode-${prompts.length}` }
          },
          async messages() {
            return { data: messages }
          },
          async abort() {},
        },
      },
      subscribe(nextListener) {
        listener = nextListener
        return () => {
          listener = undefined
        }
      },
    })

    await adapter.handleUserMessage({ sessionID: "ses_open", text: "ulw port OMO", agentName: "build", modelID: "gpt" })
    listener?.({ type: "idle", sessionID: "ses_open" })
    await flushEventHandlers()
    messages.push({ info: { role: "assistant" }, parts: [{ type: "text", text: "<promise>DONE</promise>" }] })
    listener?.({ type: "idle", sessionID: "ses_open" })
    await flushEventHandlers()
    messages.push({ info: { role: "assistant" }, parts: [{ type: "text", text: "<promise>VERIFIED</promise>" }] })
    listener?.({ type: "idle", sessionID: "ses_open" })
    await flushEventHandlers()

    expect(prompts).toHaveLength(3)
    expect(prompts[0]).toEqual({ path: { id: "ses_open" }, body: { parts: [{ type: "text", text: "ULTRAWORK MODE ENABLED!" }], agent: "build", modelID: "gpt" }, query: undefined })
    expect(JSON.stringify(prompts[1])).toContain("RALPH LOOP 2/500")
    expect(JSON.stringify(prompts[2])).toContain("ULTRAWORK LOOP VERIFICATION")
    expect(adapter.loopState.getState()).toBeNull()
    adapter.stop()
  })

  test("applies standalone model guard decisions through OpenCode adapter callbacks", async () => {
    const toasts: unknown[] = []
    const sessionAgents: unknown[] = []
    const adapter = createOpenCodeOmoPluginAdapter({
      loopState: createUlwLoopStateController(createMemoryUlwLoopStateStore()),
      client: {
        session: {
          async messages() {
            return []
          },
          async abort() {},
        },
      },
      showToast(toast) {
        toasts.push(toast)
      },
      updateSessionAgent(input) {
        sessionAgents.push(input)
      },
    })

    const input = { sessionID: "ses_guard", agent: "sisyphus", model: { providerID: "openai", modelID: "gpt-5.3-codex" } }
    const output = { message: { agent: "sisyphus" } }
    await adapter.handleChatMessage(input, output)

    expect(input.agent).toBe("hephaestus")
    expect(output.message.agent).toBe("hephaestus")
    expect(toasts).toEqual([expect.objectContaining({ sessionID: "ses_guard", title: "NEVER Use Sisyphus with GPT", variant: "error" })])
    expect(sessionAgents).toEqual([{ sessionID: "ses_guard", agent: "hephaestus" }])

    const nativeOutput: { message: { variant?: string } } = { message: {} }
    await adapter.handleChatMessage({ sessionID: "ses_native", agent: "sisyphus", model: { providerID: "openai", modelID: "gpt-5.5" } }, nativeOutput)
    expect(nativeOutput.message.variant).toBe("medium")
    adapter.stop()
  })

  test("exports pure OpenCode model guard adapter for plugin hook composition", async () => {
    const updates: unknown[] = []
    const decision = await applyOpenCodeModelAgentGuard({
      client: {
        session: {
          async messages() {
            return []
          },
          async abort() {},
        },
      },
      updateSessionAgent(update) {
        updates.push(update)
      },
    }, { sessionID: "ses_h", agent: "hephaestus", model: { providerID: "anthropic", modelID: "claude-opus-4-7" } }, { message: {} })

    expect(decision.sessionAgent).toBe("sisyphus")
    expect(updates).toEqual([{ sessionID: "ses_h", agent: "sisyphus" }])
  })

  test("requires directory or injected loop state for plugin adapter", () => {
    expect(() => createOpenCodeOmoPluginAdapter({
      client: {
        session: {
          async messages() {
            return []
          },
          async abort() {},
        },
      },
    })).toThrow("OpenCode OMO plugin adapter requires directory or loopState")
  })
})

async function flushEventHandlers(): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 0))
}
