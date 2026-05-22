namespace Omodot.SkillsCore;

public enum BrowserAutomationProvider
{
    Playwright,
    PlaywrightCli,
    AgentBrowser,
    DevBrowser,
}

public sealed record CreateBuiltinSkillsOptions(
    BrowserAutomationProvider BrowserProvider = BrowserAutomationProvider.Playwright,
    HashSet<string>? DisabledSkills = null,
    bool TeamModeEnabled = false)
{
    public static CreateBuiltinSkillsOptions Default { get; } = new();
}
