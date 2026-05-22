namespace Omodot.ModelCore;

public static class ModelRequirements
{
    public static readonly Dictionary<string, ModelRequirement> AgentModelRequirements = new()
    {
        ["sisyphus"] = new([
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-opus-4-7", "max"),
            new(["opencode-go", "vercel"], "kimi-k2.6"),
            new(["kimi-for-coding"], "k2p5"),
            new(["opencode", "moonshotai", "moonshotai-cn", "firmware", "ollama-cloud", "aihubmix", "vercel"], "kimi-k2.5"),
            new(["openai", "github-copilot", "opencode", "vercel"], "gpt-5.5", "medium"),
            new(["zai-coding-plan", "opencode", "vercel"], "glm-5"),
            new(["opencode"], "big-pickle"),
        ], RequiresAnyModel: true),
        ["oracle"] = new([
            new(["openai", "github-copilot", "opencode", "vercel"], "gpt-5.5", "high"),
            new(["google", "github-copilot", "opencode", "vercel"], "gemini-3.1-pro", "high"),
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-opus-4-7", "max"),
            new(["opencode-go", "vercel"], "glm-5.1"),
        ]),
        ["librarian"] = new([
            new(["openai"], "gpt-5.4-mini-fast"),
            new(["opencode-go"], "qwen3.5-plus"),
            new(["vercel"], "minimax-m2.7-highspeed"),
            new(["opencode-go", "vercel"], "minimax-m2.7"),
            new(["anthropic", "opencode", "vercel"], "claude-haiku-4-5"),
            new(["openai", "opencode", "vercel"], "gpt-5.4-nano"),
        ]),
        ["explore"] = new([
            new(["openai"], "gpt-5.4-mini-fast"),
            new(["opencode-go"], "qwen3.5-plus"),
            new(["vercel"], "minimax-m2.7-highspeed"),
            new(["opencode-go", "vercel"], "minimax-m2.7"),
            new(["anthropic", "opencode", "vercel"], "claude-haiku-4-5"),
            new(["openai", "opencode", "vercel"], "gpt-5.4-nano"),
        ]),
    };

    public static readonly Dictionary<string, ModelRequirement> CategoryModelRequirements = new()
    {
        ["visual-engineering"] = new([
            new(["google", "github-copilot", "opencode", "vercel"], "gemini-3.1-pro", "high"),
            new(["zai-coding-plan", "opencode", "vercel"], "glm-5"),
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-opus-4-7", "max"),
            new(["opencode-go", "vercel"], "glm-5.1"),
            new(["kimi-for-coding"], "k2p5"),
        ]),
        ["ultrabrain"] = new([
            new(["openai", "opencode", "vercel"], "gpt-5.5", "xhigh"),
            new(["google", "github-copilot", "opencode", "vercel"], "gemini-3.1-pro", "high"),
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-opus-4-7", "max"),
            new(["opencode-go", "vercel"], "glm-5.1"),
        ]),
        ["deep"] = new([
            new(["openai", "github-copilot", "venice", "opencode", "vercel"], "gpt-5.5", "medium"),
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-opus-4-7", "max"),
            new(["google", "github-copilot", "opencode", "vercel"], "gemini-3.1-pro", "high"),
            new(["opencode-go", "vercel"], "kimi-k2.6"),
            new(["opencode-go", "vercel"], "glm-5.1"),
        ]),
        ["quick"] = new([
            new(["openai", "github-copilot", "opencode", "vercel"], "gpt-5.4-mini"),
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-haiku-4-5"),
            new(["google", "github-copilot", "opencode", "vercel"], "gemini-3-flash"),
            new(["opencode-go", "vercel"], "minimax-m2.7"),
            new(["opencode", "vercel"], "gpt-5-nano"),
        ]),
        ["writing"] = new([
            new(["google", "github-copilot", "opencode", "vercel"], "gemini-3-flash"),
            new(["opencode-go", "vercel"], "kimi-k2.6"),
            new(["anthropic", "github-copilot", "opencode", "vercel"], "claude-sonnet-4-6"),
            new(["opencode-go", "vercel"], "minimax-m2.7"),
        ]),
    };

    public static List<string> GetBuiltInRequirementModelIDs()
    {
        var ids = new HashSet<string>();
        foreach (var req in AgentModelRequirements.Values)
            foreach (var entry in req.FallbackChain)
                ids.Add(entry.Model);
        foreach (var req in CategoryModelRequirements.Values)
            foreach (var entry in req.FallbackChain)
                ids.Add(entry.Model);
        return ids.OrderBy(x => x).ToList();
    }
}
