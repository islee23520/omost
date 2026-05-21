export type UlwIntentType = "ultrawork" | "hyperplan" | "hyperplan-ultrawork"

export type UlwIntent = {
  type: UlwIntentType
  prompt: string
}

const codeBlockPattern = /```[\s\S]*?```/g
const inlineCodePattern = /`[^`]+`/g
const ultraworkPattern = /\b(ultrawork|ulw)\b/i
const hyperplanPattern = /\b(hyperplan|hpp)\b/i
const hyperplanUltraworkPattern =
  /\b(?:hpp|hyperplan)\s+(?:ulw|ultrawork)\b|\b(?:ulw|ultrawork)\s+(?:hpp|hyperplan)\b/i

export function removeCode(text: string): string {
  return text.replace(codeBlockPattern, "").replace(inlineCodePattern, "")
}

export function detectUlwIntent(text: string): UlwIntent[] {
  const normalized = removeCode(text)
  const intents: UlwIntent[] = []

  if (hyperplanUltraworkPattern.test(normalized)) {
    intents.push({ type: "hyperplan-ultrawork", prompt: getUlwIntentPrompt("hyperplan-ultrawork") })
    return intents
  }

  if (ultraworkPattern.test(normalized)) {
    intents.push({ type: "ultrawork", prompt: getUlwIntentPrompt("ultrawork") })
  }

  if (hyperplanPattern.test(normalized)) {
    intents.push({ type: "hyperplan", prompt: getUlwIntentPrompt("hyperplan") })
  }

  return intents
}

export function getUlwIntentPrompt(type: UlwIntentType): string {
  if (type === "hyperplan-ultrawork") return "HYPERPLAN ULTRAWORK MODE ENABLED!"
  if (type === "hyperplan") return "HYPERPLAN MODE ENABLED!"
  return "ULTRAWORK MODE ENABLED!"
}
