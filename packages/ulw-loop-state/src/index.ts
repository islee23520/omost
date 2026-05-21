import { closeSync, constants, existsSync, lstatSync, mkdirSync, openSync, readFileSync, realpathSync, unlinkSync, writeSync } from "node:fs"
import { dirname, isAbsolute, relative, resolve } from "node:path"
import { parseFrontmatter } from "@oh-my-opencode/utils"

export const DEFAULT_COMPLETION_PROMISE = "DONE"
export const ULTRAWORK_VERIFICATION_PROMISE = "VERIFIED"
export const DEFAULT_MAX_ITERATIONS = 100
export const ULTRAWORK_MAX_ITERATIONS = 500
export const DEFAULT_STATE_FILE = ".omo/ulw-loop.local.md"

export type UlwLoopStrategy = "reset" | "continue"

export type UlwLoopState = {
  active: boolean
  iteration: number
  maxIterations: number
  completionPromise: string
  initialCompletionPromise: string
  startedAt: string
  prompt: string
  sessionID: string
  messageCountAtStart?: number
  verificationPending?: boolean
  verificationAttemptID?: string
  verificationSessionID?: string
  strategy: UlwLoopStrategy
  ultrawork?: boolean
}

export type StartUlwLoopOptions = {
  sessionID: string
  prompt: string
  maxIterations?: number
  completionPromise?: string
  messageCountAtStart?: number
  ultrawork?: boolean
  strategy?: UlwLoopStrategy
  now?: () => string
}

export type IterationExpectation = {
  iteration: number
  sessionID: string
}

export type UlwLoopStateStore = {
  read(): UlwLoopState | null
  write(state: UlwLoopState): void
  clear(): void
}

export function getUlwLoopStateFilePath(directory: string, customPath?: string): string {
  const basePath = normalizeDarwinRealpath(resolve(directory))
  const statePath = normalizeDarwinRealpath(resolve(basePath, customPath ?? DEFAULT_STATE_FILE))
  if (!isPathInside(statePath, basePath)) throw new Error("ULW loop state path must stay inside the base directory")
  return statePath
}

export type UlwLoopStateController = {
  start(options: StartUlwLoopOptions): UlwLoopState
  cancel(sessionID: string): boolean
  getState(): UlwLoopState | null
  clear(): void
  incrementIteration(expected?: IterationExpectation): UlwLoopState | null
  markVerificationPending(sessionID: string, messageCountAtStart?: number): UlwLoopState | null
  setSessionID(sessionID: string, nextSessionID: string): UlwLoopState | null
  setMessageCountAtStart(sessionID: string, count: number, expectedStartedAt?: string): UlwLoopState | null
  setVerificationSessionID(sessionID: string, verificationSessionID: string): UlwLoopState | null
  restartAfterFailedVerification(sessionID: string, messageCountAtStart?: number): UlwLoopState | null
  clearVerificationState(sessionID: string, messageCountAtStart?: number): UlwLoopState | null
}

export function createMemoryUlwLoopStateStore(initialState: UlwLoopState | null = null): UlwLoopStateStore {
  let state = initialState
  return {
    read() {
      return state ? { ...state } : null
    },
    write(nextState) {
      state = { ...nextState }
    },
    clear() {
      state = null
    },
  }
}

export function createFileUlwLoopStateStore(directory: string, customPath?: string): UlwLoopStateStore {
  const basePath = normalizeDarwinRealpath(resolve(directory))
  const filePath = getUlwLoopStateFilePath(directory, customPath)
  return {
    read() {
      if (!isSafeStatePath(filePath, basePath)) return null
      return readUlwLoopStateFile(filePath)
    },
    write(state) {
      writeUlwLoopStateFile(filePath, state, basePath)
    },
    clear() {
      if (isSafeStatePath(filePath, basePath) && existsSync(filePath) && !lstatSync(filePath).isSymbolicLink()) unlinkSync(filePath)
    },
  }
}

export function readUlwLoopStateFile(filePath: string): UlwLoopState | null {
  if (!existsSync(filePath)) return null

  try {
    if (lstatSync(filePath).isSymbolicLink()) return null
    const content = readFileSync(filePath, "utf-8")
    const { data, body } = parseFrontmatter<Record<string, unknown>>(content)
    const active = data.active === true || data.active === "true"
    const iteration = toNumber(data.iteration)

    if (data.active === undefined || iteration === undefined) return null

    const maxIterations = toNumber(data.max_iterations) ?? DEFAULT_MAX_ITERATIONS
    const completionPromise = stripQuotes(data.completion_promise) || DEFAULT_COMPLETION_PROMISE
    const strategy = data.strategy === "reset" || data.strategy === "continue" ? data.strategy : "continue"
    return {
      active,
      iteration,
      maxIterations,
      completionPromise,
      initialCompletionPromise: stripQuotes(data.initial_completion_promise) || completionPromise,
      startedAt: stripQuotes(data.started_at) || new Date().toISOString(),
      prompt: body.trim(),
      sessionID: stripQuotes(data.session_id),
      messageCountAtStart: toNumber(data.message_count_at_start),
      verificationPending: data.verification_pending === true || data.verification_pending === "true" ? true : undefined,
      verificationAttemptID: stripQuotes(data.verification_attempt_id),
      verificationSessionID: stripQuotes(data.verification_session_id),
      strategy,
      ultrawork: data.ultrawork === true || data.ultrawork === "true" ? true : undefined,
    }
  } catch {
    return null
  }
}

export function writeUlwLoopStateFile(filePath: string, state: UlwLoopState, basePath = dirname(filePath)): void {
  mkdirSync(dirname(filePath), { recursive: true })
  if (!isPathInside(normalizeDarwinRealpath(realpathSync(dirname(filePath))), basePath)) throw new Error("ULW loop state parent path must stay inside the base directory")
  if (existsSync(filePath) && lstatSync(filePath).isSymbolicLink()) throw new Error("ULW loop state file must not be a symlink")
  const fd = openSync(filePath, constants.O_CREAT | constants.O_TRUNC | constants.O_WRONLY | constants.O_NOFOLLOW, 0o600)
  try {
    writeSync(fd, serializeUlwLoopState(state), undefined, "utf-8")
  } finally {
    closeSync(fd)
  }
}

export function serializeUlwLoopState(state: UlwLoopState): string {
  const lines = [
    "---",
    `active: ${state.active}`,
    `iteration: ${state.iteration}`,
    `max_iterations: ${state.maxIterations}`,
    `completion_promise: ${quoteYamlString(state.completionPromise)}`,
    `initial_completion_promise: ${quoteYamlString(state.initialCompletionPromise)}`,
    `started_at: ${quoteYamlString(state.startedAt)}`,
    `session_id: ${quoteYamlString(state.sessionID)}`,
    `strategy: ${quoteYamlString(state.strategy)}`,
  ]

  if (typeof state.messageCountAtStart === "number") lines.push(`message_count_at_start: ${state.messageCountAtStart}`)
  if (state.ultrawork) lines.push("ultrawork: true")
  if (state.verificationPending) lines.push("verification_pending: true")
  if (state.verificationAttemptID) lines.push(`verification_attempt_id: ${quoteYamlString(state.verificationAttemptID)}`)
  if (state.verificationSessionID) lines.push(`verification_session_id: ${quoteYamlString(state.verificationSessionID)}`)

  return `${lines.join("\n")}\n---\n${state.prompt}\n`
}

function toNumber(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) return value
  if (typeof value !== "string" || value.trim() === "") return undefined
  const number = Number(value)
  return Number.isFinite(number) ? number : undefined
}

function stripQuotes(value: unknown): string {
  return String(value ?? "").replace(/^["']|["']$/g, "")
}

function quoteYamlString(value: string): string {
  return JSON.stringify(value)
}

function isPathInside(targetPath: string, basePath: string): boolean {
  const relation = relative(basePath, targetPath)
  return relation === "" || (!relation.startsWith("..") && !isAbsolute(relation))
}

function isSafeStatePath(filePath: string, basePath: string): boolean {
  try {
    return isPathInside(normalizeDarwinRealpath(realpathSync(dirname(filePath))), basePath)
  } catch {
    return false
  }
}

function normalizeDarwinRealpath(filePath: string): string {
  return filePath.startsWith("/private/var/") ? filePath.slice("/private".length) : filePath
}

export function createUlwLoopStateController(store: UlwLoopStateStore): UlwLoopStateController {
  return {
    start(options) {
      const completionPromise = options.completionPromise ?? DEFAULT_COMPLETION_PROMISE
      const state: UlwLoopState = {
        active: true,
        iteration: 1,
        maxIterations: options.ultrawork ? ULTRAWORK_MAX_ITERATIONS : options.maxIterations ?? DEFAULT_MAX_ITERATIONS,
        completionPromise,
        initialCompletionPromise: completionPromise,
        startedAt: options.now?.() ?? new Date().toISOString(),
        prompt: options.prompt,
        sessionID: options.sessionID,
        messageCountAtStart: options.messageCountAtStart,
        strategy: options.strategy ?? "continue",
        ultrawork: options.ultrawork ? true : undefined,
      }
      store.write(state)
      return state
    },
    cancel(sessionID) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID) return false
      store.clear()
      return true
    },
    getState() {
      return store.read()
    },
    clear() {
      store.clear()
    },
    incrementIteration(expected) {
      const state = store.read()
      if (!state) return null
      if (expected && (state.iteration !== expected.iteration || state.sessionID !== expected.sessionID)) return null
      const nextState = { ...state, iteration: state.iteration + 1 }
      store.write(nextState)
      return nextState
    },
    markVerificationPending(sessionID, messageCountAtStart) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID || !state.ultrawork) return null
      const nextState = {
        ...state,
        completionPromise: ULTRAWORK_VERIFICATION_PROMISE,
        messageCountAtStart: messageCountAtStart ?? state.messageCountAtStart,
        verificationPending: true,
        verificationAttemptID: undefined,
        verificationSessionID: undefined,
      }
      store.write(nextState)
      return nextState
    },
    setSessionID(sessionID, nextSessionID) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID) return null
      const nextState = { ...state, sessionID: nextSessionID }
      store.write(nextState)
      return nextState
    },
    setMessageCountAtStart(sessionID, count, expectedStartedAt) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID) return null
      if (state.iteration !== 1 || state.verificationPending || state.messageCountAtStart !== undefined) return null
      if (expectedStartedAt && state.startedAt !== expectedStartedAt) return null
      const nextState = { ...state, messageCountAtStart: count }
      store.write(nextState)
      return nextState
    },
    setVerificationSessionID(sessionID, verificationSessionID) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID || !state.ultrawork || !state.verificationPending) return null
      const nextState = { ...state, verificationSessionID }
      store.write(nextState)
      return nextState
    },
    restartAfterFailedVerification(sessionID, messageCountAtStart) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID || !state.ultrawork || !state.verificationPending) return null
      const nextState: UlwLoopState = {
        ...state,
        iteration: state.iteration + 1,
        completionPromise: state.initialCompletionPromise,
        startedAt: new Date().toISOString(),
        verificationPending: undefined,
        verificationAttemptID: undefined,
        verificationSessionID: undefined,
        messageCountAtStart: messageCountAtStart ?? state.messageCountAtStart,
      }
      store.write(nextState)
      return nextState
    },
    clearVerificationState(sessionID, messageCountAtStart) {
      const state = store.read()
      if (!state || state.sessionID !== sessionID || !state.ultrawork || !state.verificationPending) return null
      const nextState: UlwLoopState = {
        ...state,
        completionPromise: state.initialCompletionPromise,
        startedAt: new Date().toISOString(),
        verificationPending: undefined,
        verificationAttemptID: undefined,
        verificationSessionID: undefined,
        messageCountAtStart: messageCountAtStart ?? state.messageCountAtStart,
      }
      store.write(nextState)
      return nextState
    },
  }
}
