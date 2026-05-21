import { describe, expect, test } from "bun:test"
import type { UlwHost, UlwSessionEvent } from "./index"

describe("ulw host contract", () => {
  test("supports prompt dispatch and event subscription through the semantic host surface", async () => {
    const events: UlwSessionEvent[] = []
    const host: UlwHost = {
      async dispatchPrompt(request) {
        return { accepted: request.message.length > 0, sessionID: request.sessionID, dispatchID: "dispatch-1" }
      },
      async readMessages() {
        return [{ role: "assistant", text: "ok" }]
      },
      async readTodos() {
        return [{ content: "finish", status: "completed" }]
      },
      async readStatus() {
        return "idle"
      },
      async abort() {},
      onEvent(listener) {
        listener({ type: "idle", sessionID: "ses_1" })
        return () => events.push({ type: "deleted", sessionID: "ses_1" })
      },
    }

    const dispose = host.onEvent((event) => events.push(event))
    const receipt = await host.dispatchPrompt({ sessionID: "ses_1", message: "ulw" })
    dispose()

    expect(receipt.accepted).toBe(true)
    expect(events).toEqual([{ type: "idle", sessionID: "ses_1" }, { type: "deleted", sessionID: "ses_1" }])
  })
})
