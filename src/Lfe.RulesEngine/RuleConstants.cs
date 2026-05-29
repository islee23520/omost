using System.Text.RegularExpressions;

namespace Lfe.RulesEngine;

public static class RuleConstants
{
    public static readonly string[] ProjectMarkers = [".git", "pyproject.toml", "package.json", "Cargo.toml", "go.mod", ".venv"];

    public static readonly (string Parent, string Subdir)[] ProjectRuleSubdirs =
    [
        (".lfe", "rules"),
        (".claude", "rules"),
        (".cursor", "rules"),
        (".github", "instructions"),
        (".sisyphus", "rules"),
    ];

    public static readonly string[] ProjectRuleFiles = [".github/copilot-instructions.md"];
    public static readonly string[] OpencodeUserRuleDirs = [".lfe/rules", ".opencode/rules", ".sisyphus/rules"];
    public const string UserRuleDir = ".claude/rules";
    public static readonly string[] RuleExtensions = [".md", ".mdc"];

    public static readonly Regex GitHubInstructionsPattern = new(@"\.instructions\.md$", RegexOptions.Compiled);

    public const string AgentsFilename = "AGENTS.md";
    public const int GlobalDistance = 9999;

    public static readonly HashSet<string> ExcludedDirs = new(StringComparer.Ordinal)
    {
        "node_modules", ".git", "dist", "build", ".turbo", ".next", "coverage"
    };

    public static readonly Dictionary<string, int> SourcePriority = new()
    {
        [".lfe/rules"] = 0,
        [".claude/rules"] = 1,
        [".cursor/rules"] = 2,
        [".github/instructions"] = 3,
        [".github/copilot-instructions.md"] = 4,
        [".sisyphus/rules"] = 5,
        ["~/.lfe/rules"] = 100,
        ["~/.opencode/rules"] = 101,
        ["~/.claude/rules"] = 102,
        ["~/.sisyphus/rules"] = 103,
    };
}
