import { describe, expect, test } from "bun:test"
import { detectUlwIntent, removeCode } from "./index"

describe("ulw intent", () => {
  test("detects ultrawork aliases outside code spans", () => {
    expect(detectUlwIntent("please ulw this")).toEqual([
      { type: "ultrawork", prompt: "ULTRAWORK MODE ENABLED!" },
    ])
    expect(detectUlwIntent("`ulw` only in code")).toEqual([])
  })

  test("prefers the combined hyperplan ultrawork intent for adjacent aliases", () => {
    expect(detectUlwIntent("hyperplan ulw")).toEqual([
      { type: "hyperplan-ultrawork", prompt: "HYPERPLAN ULTRAWORK MODE ENABLED!" },
    ])
  })

  test("removes fenced and inline code before matching", () => {
    expect(removeCode("```ulw\nignore\n``` run ulw")).toBe(" run ulw")
  })
})
