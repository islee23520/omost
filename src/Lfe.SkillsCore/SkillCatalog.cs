namespace Lfe.SkillsCore;

public static class SkillCatalog
{
    public static List<BuiltinSkill> CreateBuiltinSkills(CreateBuiltinSkillsOptions? options = null)
    {
        options ??= CreateBuiltinSkillsOptions.Default;
        var disabled = options.DisabledSkills;
        var skills = new List<BuiltinSkill>();

        // Browser skill (select one based on provider)
        var browserSkill = options.BrowserProvider switch
        {
            BrowserAutomationProvider.AgentBrowser => SkillDefinitions.AgentBrowser,
            BrowserAutomationProvider.DevBrowser => SkillDefinitions.DevBrowser,
            BrowserAutomationProvider.PlaywrightCli => SkillDefinitions.PlaywrightCli,
            _ => SkillDefinitions.Playwright,
        };
        skills.Add(browserSkill);

        // Always-included skills
        skills.Add(SkillDefinitions.FrontendUiUx);
        skills.Add(SkillDefinitions.GitMaster);
        skills.Add(SkillDefinitions.ReviewWork);
        skills.Add(SkillDefinitions.AiSlopRemover);

        // Conditional: team-mode
        if (options.TeamModeEnabled && (disabled is null || !disabled.Contains("team-mode")))
        {
            skills.Add(SkillDefinitions.TeamMode);
        }

        // Filter disabled
        if (disabled is null || disabled.Count == 0)
            return skills;

        return skills.Where(s => !disabled.Contains(s.Name)).ToList();
    }
}
