import { describe, expect, test } from "bun:test"
import { createStandaloneOmoRuntime, runStandaloneOmo } from "./index"

describe("standalone OMO runtime", () => {
  test("runs ULW without OpenCode or Codex adapters", async () => {
    const result = await runStandaloneOmo()

    expect(result.prompts).toHaveLength(3)
    expect(result.prompts[0]).toBe("ULTRAWORK MODE ENABLED!")
    expect(result.prompts[1]).toContain("RALPH LOOP 2/500")
    expect(result.prompts[2]).toContain("ULTRAWORK LOOP VERIFICATION")
    expect(result.skillNames).toContain("git-master")
    expect(result.skillNames).toContain("team-mode")
    expect(result.hookSummary).toEqual({ "behavior-mapped": 61, "adapter-bound": 0, missing: 0 })
    expect(result.finalState).toBeNull()
  })

  test("keeps engine state in standalone memory host", async () => {
    const runtime = createStandaloneOmoRuntime()

    await runtime.submitUserMessage({ sessionID: "runtime-session", text: "please ulw" })
    await runtime.emitIdle("runtime-session")

    expect(runtime.loopState.getState()?.iteration).toBe(2)
    expect(await runtime.host.readMessages("missing-session")).toEqual([])
    expect(await runtime.host.readTodos("runtime-session")).toEqual([])
    expect(await runtime.host.readStatus("runtime-session")).toBe("idle")
    expect(await runtime.host.abort("runtime-session")).toBeUndefined()
    expect(runtime.skills.some((skill) => skill.name === "review-work" && skill.template.includes("5-Agent"))).toBe(true)
    expect(runtime.hooks.some((hook) => hook.name === "model-fallback" && hook.standalonePackage === "@oh-my-opencode/model-core")).toBe(true)
    expect(runtime.readMessages("runtime-session").some((message) => message.text.includes("RALPH LOOP 2/500"))).toBe(true)
    runtime.stop()
  })
})
