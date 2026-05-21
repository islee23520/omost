export type UlwSessionEventType = "idle" | "error" | "deleted" | "compacting" | "completed"

export type UlwSessionEvent = {
  type: UlwSessionEventType
  sessionID: string
  error?: string
}

export type UlwPromptRequest = {
  sessionID: string
  message: string
  agentName?: string
  modelID?: string
}

export type UlwPromptReceipt = {
  accepted: boolean
  sessionID: string
  dispatchID: string
}

export type UlwMessage = {
  role: "user" | "assistant" | "system" | "tool"
  text: string
}

export type UlwTodo = {
  content: string
  status: "pending" | "in_progress" | "completed" | "cancelled"
}

export type UlwHost = {
  dispatchPrompt(request: UlwPromptRequest): Promise<UlwPromptReceipt>
  readMessages(sessionID: string): Promise<UlwMessage[]>
  readTodos(sessionID: string): Promise<UlwTodo[]>
  readStatus(sessionID: string): Promise<string>
  abort(sessionID: string): Promise<void>
  onEvent(listener: (event: UlwSessionEvent) => void): () => void
}
