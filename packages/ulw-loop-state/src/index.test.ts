import { describe, expect, test } from "bun:test"
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs"
import { tmpdir } from "node:os"
import { dirname, join } from "node:path"
import {
  createFileUlwLoopStateStore,
  createMemoryUlwLoopStateStore,
  createUlwLoopStateController,
  getUlwLoopStateFilePath,
  readUlwLoopStateFile,
  ULTRAWORK_MAX_ITERATIONS,
  ULTRAWORK_VERIFICATION_PROMISE,
} from "./index"

describe("ulw loop state", () => {
  test("starts an ultrawork loop with a 500 iteration budget", () => {
    const controller = createUlwLoopStateController(createMemoryUlwLoopStateStore())

    const state = controller.start({ sessionID: "ses_1", prompt: "build", ultrawork: true, now: () => "2026-01-01T00:00:00.000Z" })

    expect(state.maxIterations).toBe(ULTRAWORK_MAX_ITERATIONS)
    expect(state.completionPromise).toBe("DONE")
    expect(state.iteration).toBe(1)
    expect(controller.getState()).toEqual(state)
  })

  test("increments only when the expected session and iteration match", () => {
    const controller = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    controller.start({ sessionID: "ses_1", prompt: "build" })

    expect(controller.incrementIteration({ sessionID: "other", iteration: 1 })).toBeNull()

    const state = controller.incrementIteration({ sessionID: "ses_1", iteration: 1 })

    expect(state?.iteration).toBe(2)
  })

  test("tracks verification pending and restart transitions", () => {
    const controller = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    controller.start({ sessionID: "ses_1", prompt: "build", completionPromise: "DONE", ultrawork: true })

    const pending = controller.markVerificationPending("ses_1")
    expect(pending?.verificationPending).toBe(true)
    expect(pending?.completionPromise).toBe(ULTRAWORK_VERIFICATION_PROMISE)

    const withVerifier = controller.setVerificationSessionID("ses_1", "ses_verify")
    expect(withVerifier?.verificationSessionID).toBe("ses_verify")

    const restarted = controller.restartAfterFailedVerification("ses_1", 10)
    expect(restarted?.iteration).toBe(2)
    expect(restarted?.completionPromise).toBe("DONE")
    expect(restarted?.verificationPending).toBeUndefined()
    expect(restarted?.messageCountAtStart).toBe(10)
  })

  test("updates reset session ownership and captures initial message count once", () => {
    const controller = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    const started = controller.start({ sessionID: "ses_1", prompt: "build", now: () => "2026-01-01T00:00:00.000Z" })

    expect(controller.setSessionID("ses_1", "ses_2")?.sessionID).toBe("ses_2")
    expect(controller.setMessageCountAtStart("ses_2", 7, started.startedAt)?.messageCountAtStart).toBe(7)
    expect(controller.setMessageCountAtStart("ses_2", 8, started.startedAt)).toBeNull()
    expect(controller.incrementIteration({ sessionID: "ses_2", iteration: 1 })?.iteration).toBe(2)
  })

  test("persists state as frontmatter with the prompt body intact", () => {
    const directory = mkdtempSync(join(tmpdir(), "ulw-loop-state-"))
    try {
      const store = createFileUlwLoopStateStore(directory)
      const controller = createUlwLoopStateController(store)
      controller.start({
        sessionID: "ses_file",
        prompt: "line one\nline two",
        ultrawork: true,
        now: () => "2026-01-01T00:00:00.000Z",
      })

      const filePath = getUlwLoopStateFilePath(directory)
      expect(readFileSync(filePath, "utf-8")).toContain("session_id: \"ses_file\"")
      expect(readFileSync(filePath, "utf-8")).toContain("ultrawork: true")
      expect(readUlwLoopStateFile(filePath)?.prompt).toBe("line one\nline two")
      expect(readUlwLoopStateFile(filePath)?.ultrawork).toBe(true)
      expect(createFileUlwLoopStateStore(directory).read()?.maxIterations).toBe(ULTRAWORK_MAX_ITERATIONS)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  test("rejects custom paths outside the base directory", () => {
    const directory = mkdtempSync(join(tmpdir(), "ulw-loop-state-"))
    try {
      expect(() => createFileUlwLoopStateStore(directory, "../escape.md")).toThrow()
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })

  test("does not follow a symlinked state file on write", () => {
    const directory = mkdtempSync(join(tmpdir(), "ulw-loop-state-"))
    const outsideFile = join(tmpdir(), `ulw-loop-state-outside-${Date.now()}.md`)
    try {
      writeFileSync(outsideFile, "outside", "utf-8")
      const statePath = getUlwLoopStateFilePath(directory)
      mkdirSync(dirname(statePath), { recursive: true })
      symlinkSync(outsideFile, statePath)
      const controller = createUlwLoopStateController(createFileUlwLoopStateStore(directory))

      expect(() => controller.start({ sessionID: "ses_file", prompt: "build" })).toThrow()
      expect(readFileSync(outsideFile, "utf-8")).toBe("outside")
    } finally {
      rmSync(directory, { recursive: true, force: true })
      rmSync(outsideFile, { force: true })
    }
  })

  test("does not write through a symlinked state directory", () => {
    const directory = mkdtempSync(join(tmpdir(), "ulw-loop-state-"))
    const outsideDirectory = mkdtempSync(join(tmpdir(), "ulw-loop-state-outside-dir-"))
    try {
      symlinkSync(outsideDirectory, join(directory, ".omo"))
      const controller = createUlwLoopStateController(createFileUlwLoopStateStore(directory))

      expect(() => controller.start({ sessionID: "ses_file", prompt: "build" })).toThrow()
    } finally {
      rmSync(directory, { recursive: true, force: true })
      rmSync(outsideDirectory, { recursive: true, force: true })
    }
  })

  test("does not read or clear through a symlinked state directory", () => {
    const directory = mkdtempSync(join(tmpdir(), "ulw-loop-state-"))
    const outsideDirectory = mkdtempSync(join(tmpdir(), "ulw-loop-state-outside-dir-"))
    try {
      writeFileSync(join(outsideDirectory, "ulw-loop.local.md"), "---\nactive: true\niteration: 1\nsession_id: outside\n---\noutside\n", "utf-8")
      symlinkSync(outsideDirectory, join(directory, ".omo"))
      const store = createFileUlwLoopStateStore(directory)

      expect(store.read()).toBeNull()
      store.clear()
      expect(readFileSync(join(outsideDirectory, "ulw-loop.local.md"), "utf-8")).toContain("outside")
    } finally {
      rmSync(directory, { recursive: true, force: true })
      rmSync(outsideDirectory, { recursive: true, force: true })
    }
  })

  test("verification transitions require an ultrawork loop", () => {
    const controller = createUlwLoopStateController(createMemoryUlwLoopStateStore())
    controller.start({ sessionID: "ses_1", prompt: "build" })

    expect(controller.markVerificationPending("ses_1")).toBeNull()
  })
})
