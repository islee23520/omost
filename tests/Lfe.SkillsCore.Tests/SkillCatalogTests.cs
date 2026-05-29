using Lfe.SkillsCore;
using Xunit;

namespace Lfe.SkillsCore.Tests;

public class SkillCatalogTests
{
    [Fact]
    public void Default_Returns5CoreSkills()
    {
        var skills = SkillCatalog.CreateBuiltinSkills();
        Assert.Equal(5, skills.Count);
        Assert.Contains(skills, s => s.Name == "playwright");
        Assert.Contains(skills, s => s.Name == "frontend-ui-ux");
        Assert.Contains(skills, s => s.Name == "git-master");
        Assert.Contains(skills, s => s.Name == "review-work");
        Assert.Contains(skills, s => s.Name == "ai-slop-remover");
    }

    [Fact]
    public void TeamMode_NotIncluded_ByDefault()
    {
        var skills = SkillCatalog.CreateBuiltinSkills();
        Assert.DoesNotContain(skills, s => s.Name == "team-mode");
    }

    [Fact]
    public void TeamMode_Included_WhenEnabled()
    {
        var options = new CreateBuiltinSkillsOptions(TeamModeEnabled: true);
        var skills = SkillCatalog.CreateBuiltinSkills(options);
        Assert.Equal(6, skills.Count);
        Assert.Contains(skills, s => s.Name == "team-mode");
    }

    [Fact]
    public void BrowserProvider_Playwright_IsDefault()
    {
        var skills = SkillCatalog.CreateBuiltinSkills();
        Assert.Contains(skills, s => s.Name == "playwright");
    }

    [Fact]
    public void BrowserProvider_AgentBrowser_SelectsAgentBrowser()
    {
        var options = new CreateBuiltinSkillsOptions(BrowserProvider: BrowserAutomationProvider.AgentBrowser);
        var skills = SkillCatalog.CreateBuiltinSkills(options);
        Assert.Contains(skills, s => s.Name == "agent-browser");
        Assert.DoesNotContain(skills, s => s.Name == "playwright");
    }

    [Fact]
    public void BrowserProvider_DevBrowser_SelectsDevBrowser()
    {
        var options = new CreateBuiltinSkillsOptions(BrowserProvider: BrowserAutomationProvider.DevBrowser);
        var skills = SkillCatalog.CreateBuiltinSkills(options);
        Assert.Contains(skills, s => s.Name == "dev-browser");
    }

    [Fact]
    public void BrowserProvider_PlaywrightCli_SelectsPlaywrightCli()
    {
        var options = new CreateBuiltinSkillsOptions(BrowserProvider: BrowserAutomationProvider.PlaywrightCli);
        var skills = SkillCatalog.CreateBuiltinSkills(options);
        Assert.Contains(skills, s => s.Name == "playwright-cli");
    }

    [Fact]
    public void DisabledSkills_AreFiltered()
    {
        var disabled = new HashSet<string> { "git-master", "review-work" };
        var options = new CreateBuiltinSkillsOptions(DisabledSkills: disabled);
        var skills = SkillCatalog.CreateBuiltinSkills(options);

        Assert.DoesNotContain(skills, s => s.Name == "git-master");
        Assert.DoesNotContain(skills, s => s.Name == "review-work");
        Assert.Equal(3, skills.Count);
    }

    [Fact]
    public void TeamMode_CanBeDisabled()
    {
        var disabled = new HashSet<string> { "team-mode" };
        var options = new CreateBuiltinSkillsOptions(TeamModeEnabled: true, DisabledSkills: disabled);
        var skills = SkillCatalog.CreateBuiltinSkills(options);

        Assert.DoesNotContain(skills, s => s.Name == "team-mode");
    }

    [Fact]
    public void AllSkills_HaveRequiredFields()
    {
        var skills = SkillCatalog.CreateBuiltinSkills(new CreateBuiltinSkillsOptions(
            TeamModeEnabled: true,
            BrowserProvider: BrowserAutomationProvider.AgentBrowser));

        foreach (var skill in skills)
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.Name));
            Assert.False(string.IsNullOrWhiteSpace(skill.Description));
            Assert.False(string.IsNullOrWhiteSpace(skill.Template));
        }
    }

    [Fact]
    public void NoDuplicateNames()
    {
        var skills = SkillCatalog.CreateBuiltinSkills(new CreateBuiltinSkillsOptions(TeamModeEnabled: true));
        var names = skills.Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
