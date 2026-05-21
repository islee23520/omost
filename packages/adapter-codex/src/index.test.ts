import { describe, expect, test } from "bun:test"
import type { CodexConversationEvent, CodexSendInput, CodexTranscriptItem } from "./index"
import { createCodexOmoAdapter, createCodexUlwHost } from "./index"

describe("codex adapter", () => {
  test("maps Codex conversation client to UlwHost", async () => {
    const prompts: CodexSendInput[] = []
    const host = createCodexUlwHost({
      conversation: {
        async send(input) {
          prompts.push(input)
          return "codex-dispatch-1"
        },
        async transcript() {
          return [{ role: "assistant", content: [{ type: "output_text", text: "done" }] }]
        },
      },
    })

    const receipt = await host.dispatchPrompt({ sessionID: "ses_1", message: "continue", agentName: "build", modelID: "gpt" })
    expect(receipt).toEqual({
      accepted: true,
      sessionID: "ses_1",
      dispatchID: "codex-dispatch-1",
    })
    expect(await host.readMessages("ses_1")).toEqual([{ role: "assistant", text: "done" }])
    expect(await host.readTodos("ses_1")).toEqual([])
    expect(await host.readStatus("ses_1")).toBe("unknown")
    expect(prompts).toEqual([{ conversationID: "ses_1", input: "continue", agent: "build", model: "gpt" }])
  })

  test("normalizes Codex send failures, status, abort, and transcript variants", async () => {
    let aborted: string | undefined
    const host = createCodexUlwHost({
      conversation: {
        async send() {
          return { status: "rejected", itemID: "item_1" }
        },
        async transcript() {
          return [
            { role: "assistant", text: "text-field" },
            { role: "system", content: "string-content" },
            { role: "bad", content: "ignored-role" },
            { role: "assistant" },
          ]
        },
        async status() {
          return { status: "running" }
        },
        async abort(input) {
          aborted = input.conversationID
        },
      },
    })

    expect(await host.dispatchPrompt({ sessionID: "ses_1", message: "continue" })).toEqual({
      accepted: false,
      sessionID: "ses_1",
      dispatchID: "item_1",
    })
    expect(await host.readMessages("ses_1")).toEqual([
      { role: "assistant", text: "text-field" },
      { role: "system", text: "string-content" },
    ])
    expect(await host.readStatus("ses_1")).toBe("running")
    await host.abort("ses_1")
    expect(aborted).toBe("ses_1")
  })

  test("normalizes string Codex status", async () => {
    const host = createCodexUlwHost({
      conversation: {
        async send() {
          return { accepted: false }
        },
        async transcript() {
          return { items: [] }
        },
        async status() {
          return "idle"
        },
      },
    })

    expect(await host.readStatus("ses_1")).toBe("idle")
    expect(await host.dispatchPrompt({ sessionID: "ses_1", message: "continue" })).toEqual({
      accepted: false,
      sessionID: "ses_1",
      dispatchID: "ses_1",
    })
    expect(await host.abort("ses_1")).toBeUndefined()
    const unsubscribe = host.onEvent(() => {})
    expect(unsubscribe).toBeFunction()
    unsubscribe()
  })

  test("forwards only Codex idle events", () => {
    let captured: ((event: CodexConversationEvent) => void) | undefined
    const host = createCodexUlwHost({
      conversation: {
        async send() {
          return "dispatch"
        },
        async transcript() {
          return []
        },
        onEvent(listener) {
          captured = listener
          return () => {
            captured = undefined
          }
        },
      },
    })

    const events: string[] = []
    const unsubscribe = host.onEvent((event) => events.push(`${event.type}:${event.sessionID}`))
    captured?.({ type: "idle", conversationID: "ses_1" })
    captured?.({ type: "message", conversationID: "ses_1" })
    unsubscribe()

    expect(events).toEqual(["idle:ses_1"])
    expect(captured).toBeUndefined()
  })

  test("drives standalone OMO ULW lifecycle through the Codex conversation client", async () => {
    const messages: CodexTranscriptItem[] = []
    const prompts: CodexSendInput[] = []
    let listener: ((event: CodexConversationEvent) => void) | undefined
    const adapter = createCodexOmoAdapter({
      client: {
        conversation: {
          async send(input) {
            prompts.push(input)
            messages.push({ role: "user", content: input.input })
            return { status: "queued", id: `codex-${prompts.length}` }
          },
          async transcript() {
            return { items: messages }
          },
          onEvent(nextListener) {
            listener = nextListener
            return () => {
              listener = undefined
            }
          },
        },
      },
    })

    await adapter.handleUserMessage({ sessionID: "ses_codex", text: "ulw port OMO" })
    listener?.({ type: "idle", conversationID: "ses_codex" })
    await flushEventHandlers()
    messages.push({ role: "assistant", content: [{ type: "output_text", text: "<promise>DONE</promise>" }] })
    listener?.({ type: "idle", conversationID: "ses_codex" })
    await flushEventHandlers()
    messages.push({ role: "assistant", content: [{ type: "output_text", text: "<promise>VERIFIED</promise>" }] })
    listener?.({ type: "idle", conversationID: "ses_codex" })
    await flushEventHandlers()

    expect(prompts).toHaveLength(3)
    expect(prompts[0]).toMatchObject({ conversationID: "ses_codex", input: "ULTRAWORK MODE ENABLED!" })
    expect(prompts[1].input).toContain("RALPH LOOP 2/500")
    expect(prompts[2].input).toContain("ULTRAWORK LOOP VERIFICATION")
    expect(adapter.loopState.getState()).toBeNull()
    adapter.stop()
  })
})

async function flushEventHandlers(): Promise<void> {
  await new Promise<void>((resolve) => setTimeout(resolve, 0))
}
