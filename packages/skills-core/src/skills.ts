import type { BuiltinSkill } from "./types"

export type BrowserAutomationProvider = "playwright" | "playwright-cli" | "agent-browser" | "dev-browser"

import {
  playwrightSkill,
  agentBrowserSkill,
  playwrightCliSkill,
  frontendUiUxSkill,
  gitMasterSkill,
  devBrowserSkill,
  reviewWorkSkill,
  aiSlopRemoverSkill,
  teamModeSkill,
} from "./skills/index"

export interface CreateBuiltinSkillsOptions {
  browserProvider?: BrowserAutomationProvider
  disabledSkills?: Set<string>
  teamModeEnabled?: boolean
}

export function createBuiltinSkills(options: CreateBuiltinSkillsOptions = {}): BuiltinSkill[] {
  const { browserProvider = "playwright", disabledSkills, teamModeEnabled = false } = options

  let browserSkill: BuiltinSkill
	if (browserProvider === "agent-browser") {
		browserSkill = agentBrowserSkill
	} else if (browserProvider === "dev-browser") {
		browserSkill = devBrowserSkill
	} else if (browserProvider === "playwright-cli") {
		browserSkill = playwrightCliSkill
	} else {
		browserSkill = playwrightSkill
	}

	const skills = [browserSkill, frontendUiUxSkill, gitMasterSkill, reviewWorkSkill, aiSlopRemoverSkill]

  if (teamModeEnabled && !disabledSkills?.has("team-mode")) {
    skills.push(teamModeSkill)
  }

  if (!disabledSkills) {
    return skills
  }

  return skills.filter((skill) => !disabledSkills.has(skill.name))
}
