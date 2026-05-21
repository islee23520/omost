import type { UlwHost, UlwPromptReceipt } from "@oh-my-opencode/ulw-host-contract"
import { detectUlwIntent, type UlwIntentType } from "@oh-my-opencode/ulw-intent"
import type { UlwLoopState, UlwLoopStateController } from "@oh-my-opencode/ulw-loop-state"

export type RunUlwInput = {
  host: UlwHost
  sessionID: string
  text: string
  agentName?: string
  modelID?: string
}

export type RunUlwResult = {
  dispatched: boolean
  intents: string[]
  receipts: UlwPromptReceipt[]
}

export type RunTrackedUlwInput = RunUlwInput & {
  loopState: UlwLoopStateController
  completionPromise?: string
}

export type UlwLoopEngineOptions = {
  host: UlwHost
  loopState: UlwLoopStateController
}

export type UlwLoopEngine = {
  stop(): void
}

export async function runUlw(input: RunUlwInput): Promise<RunUlwResult> {
  const intents = detectUlwIntent(input.text)
  const receipts: UlwPromptReceipt[] = []

  for (const intent of intents) {
    receipts.push(await input.host.dispatchPrompt({
      sessionID: input.sessionID,
      message: intent.prompt,
      agentName: input.agentName,
      modelID: input.modelID,
    }))
  }

  return {
    dispatched: receipts.some((receipt) => receipt.accepted),
    intents: intents.map((intent) => intent.type),
    receipts,
  }
}

export async function runTrackedUlw(input: RunTrackedUlwInput): Promise<RunUlwResult> {
  const messageCountAtStart = (await input.host.readMessages(input.sessionID)).length
  const result = await runUlw(input)
  if (hasAcceptedTrackedUlwIntent(result)) {
    input.loopState.start({
      sessionID: input.sessionID,
      prompt: input.text,
      completionPromise: input.completionPromise,
      messageCountAtStart,
      ultrawork: true,
    })
  }
  return result
}

function hasAcceptedTrackedUlwIntent(result: RunUlwResult): boolean {
  return result.intents.some((intent, index) => isTrackedUlwIntent(intent) && result.receipts[index]?.accepted === true)
}

function isTrackedUlwIntent(intent: string): intent is Extract<UlwIntentType, "ultrawork" | "hyperplan-ultrawork"> {
  return intent === "ultrawork" || intent === "hyperplan-ultrawork"
}

export function createUlwLoopEngine(options: UlwLoopEngineOptions): UlwLoopEngine {
  const unsubscribe = options.host.onEvent((event) => {
    if (event.type !== "idle") return
    void handleUlwLoopIdle(options, event.sessionID)
  })
  return { stop: unsubscribe }
}

export async function handleUlwLoopIdle(options: UlwLoopEngineOptions, sessionID: string): Promise<void> {
  const state = options.loopState.getState()
  if (!state?.active || state.sessionID !== sessionID) return

  if (await completionDetected(options.host, state, sessionID)) {
    await handleDetectedCompletion(options, state, sessionID)
    return
  }

  if (state.verificationPending) {
    await handlePendingVerification(options, state, sessionID)
    return
  }

  if (state.iteration >= state.maxIterations) {
    options.loopState.clear()
    return
  }

  const nextIteration = state.iteration + 1
  const receipt = await options.host.dispatchPrompt({
    sessionID,
    message: buildContinuationPrompt({ ...state, iteration: nextIteration }),
  })
  if (!receipt.accepted) {
    options.loopState.clear()
    return
  }
  options.loopState.incrementIteration({ iteration: state.iteration, sessionID })
}

export function buildContinuationPrompt(state: UlwLoopState): string {
  if (state.verificationPending) {
    return `ultrawork [SYSTEM DIRECTIVE: OH-MY-OPENCODE - ULTRAWORK LOOP VERIFICATION ${state.iteration}/${state.maxIterations}]\n\nYou already emitted <promise>${state.initialCompletionPromise}</promise>. This does NOT finish the loop yet.\n\nREQUIRED NOW:\n- Call Oracle using task(subagent_type="oracle", load_skills=[], run_in_background=false, ...)\n- Ask Oracle to verify whether the original task is actually complete\n- Include the original task in the Oracle request\n- Explicitly tell Oracle to review skeptically and critically, and to look for reasons the task may still be incomplete or wrong\n- The system will inspect the Oracle session directly for the verification result\n- If Oracle does not verify, continue fixing the task and do not consider it complete\n\nOriginal task:\n${state.prompt}`
  }

  return `ultrawork [SYSTEM DIRECTIVE: OH-MY-OPENCODE - RALPH LOOP ${state.iteration}/${state.maxIterations}]\nContinue. Output <promise>${state.completionPromise}</promise> when done.\n${state.prompt}`
}

export function buildVerificationFailurePrompt(state: UlwLoopState): string {
  return `ultrawork [SYSTEM DIRECTIVE: OH-MY-OPENCODE - ULTRAWORK LOOP VERIFICATION FAILED ${state.iteration}/${state.maxIterations}]\n\nOracle did not emit <promise>VERIFIED</promise>. Verification failed.\n\nREQUIRED NOW:\n- Verification failed. Fix the task until Oracle's review is satisfied\n- Oracle does not lie. Treat the verification result as ground truth\n- Do not claim completion early or argue with the failed verification\n- After fixing the remaining issues, request Oracle review again using task(subagent_type="oracle", load_skills=[], run_in_background=false, ...)\n- Include the original task in the Oracle request and tell Oracle to review skeptically and critically\n- Only when the work is ready for review again, output: <promise>${state.initialCompletionPromise}</promise>\n\nOriginal task:\n${state.prompt}`
}

async function completionDetected(host: UlwHost, state: UlwLoopState, sessionID: string): Promise<boolean> {
  const messages = await host.readMessages(sessionID)
  return messages.slice(state.messageCountAtStart ?? 0).some((message) => message.role === "assistant" && message.text.includes(`<promise>${state.completionPromise}</promise>`))
}

async function handleDetectedCompletion(options: UlwLoopEngineOptions, state: UlwLoopState, sessionID: string): Promise<void> {
  if (state.ultrawork && !state.verificationPending) {
    const verificationMessageCountAtStart = (await options.host.readMessages(sessionID)).length
    const verificationState = options.loopState.markVerificationPending(sessionID, verificationMessageCountAtStart)
    if (!verificationState) return
    const receipt = await options.host.dispatchPrompt({
      sessionID,
      message: buildContinuationPrompt(verificationState),
    })
    if (!receipt.accepted) options.loopState.clear()
    return
  }

  options.loopState.clear()
}

async function handlePendingVerification(options: UlwLoopEngineOptions, state: UlwLoopState, sessionID: string): Promise<void> {
  if (await oracleVerified(options.host, state, sessionID)) {
    options.loopState.clear()
    return
  }

  if (state.iteration >= state.maxIterations) {
    options.loopState.clear()
    return
  }

  const messageCountAtStart = (await options.host.readMessages(sessionID)).length
  const previewState: UlwLoopState = {
    ...state,
    iteration: state.iteration + 1,
    verificationPending: undefined,
    verificationSessionID: undefined,
    messageCountAtStart,
  }
  const receipt = await options.host.dispatchPrompt({
    sessionID,
    message: buildVerificationFailurePrompt(previewState),
  })
  if (!receipt.accepted) {
    options.loopState.clear()
    return
  }

  const cleared = options.loopState.clearVerificationState(sessionID, messageCountAtStart)
  if (!cleared) {
    options.loopState.clear()
    return
  }
  if (!options.loopState.incrementIteration({ iteration: cleared.iteration, sessionID })) options.loopState.clear()
}

async function oracleVerified(host: UlwHost, state: UlwLoopState, sessionID: string): Promise<boolean> {
  const messages = await host.readMessages(state.verificationSessionID ?? sessionID)
  return messages.slice(state.messageCountAtStart ?? 0).some((message) => message.role === "assistant" && message.text.includes("<promise>VERIFIED</promise>"))
}
