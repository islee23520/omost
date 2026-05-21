export type SkillMcpConfig = Record<string, { command: string; args?: string[]; env?: Record<string, string> }>

export interface BuiltinSkill {
  name: string
  description: string
  template: string
  license?: string
  compatibility?: string
  metadata?: Record<string, unknown>
  allowedTools?: string[]
  agent?: string
  model?: string
  subtask?: boolean
  argumentHint?: string
  mcpConfig?: SkillMcpConfig
}
