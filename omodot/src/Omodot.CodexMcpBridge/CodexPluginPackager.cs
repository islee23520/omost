using System.Text.Json;

namespace Omodot.CodexMcpBridge;

/// <summary>
/// Generates the full Codex plugin directory structure for omodot.
/// Creates marketplace.json, plugin.json, .mcp.json, hooks.json,
/// skills/ directory, and agents/ directory.
/// </summary>
internal static class CodexPluginPackager
{
    private const string PluginName = "omodot";
    private const string PluginVersion = "0.1.0";
    private const string MarketplaceName = "omodot-local";

    public static int Generate(string outputBasePath, TextWriter output)
    {
        try
        {
            var pluginsDir = Path.Combine(outputBasePath, "plugins", PluginName);

            output.WriteLine($"Generating omodot Codex plugin at: {outputBasePath}");

            // Create directory structure
            Directory.CreateDirectory(outputBasePath);
            Directory.CreateDirectory(Path.Combine(outputBasePath, "plugins"));
            Directory.CreateDirectory(pluginsDir);
            Directory.CreateDirectory(Path.Combine(pluginsDir, ".codex-plugin"));
            Directory.CreateDirectory(Path.Combine(pluginsDir, "hooks"));
            Directory.CreateDirectory(Path.Combine(pluginsDir, "skills"));
            Directory.CreateDirectory(Path.Combine(pluginsDir, "agents"));

            // Generate files
            WriteMarketplaceJson(outputBasePath);
            WritePluginJson(pluginsDir);
            WriteMcpJson(pluginsDir);
            WriteHooksJson(pluginsDir);
            WriteSkills(pluginsDir);
            WriteAgents(pluginsDir);

            output.WriteLine("Plugin generated successfully.");
            output.WriteLine($"  marketplace.json  → {outputBasePath}/marketplace.json");
            output.WriteLine($"  plugin.json       → {pluginsDir}/.codex-plugin/plugin.json");
            output.WriteLine($"  .mcp.json         → {pluginsDir}/.mcp.json");
            output.WriteLine($"  hooks.json        → {pluginsDir}/hooks/hooks.json");
            output.WriteLine($"  skills/           → {pluginsDir}/skills/");
            output.WriteLine($"  agents/           → {pluginsDir}/agents/");
            return 0;
        }
        catch (Exception exception)
        {
            output.WriteLine($"Plugin generation failed: {exception.Message}");
            return 1;
        }
    }

    private static void WriteMarketplaceJson(string basePath)
    {
        var json = new
        {
            name = MarketplaceName,
            @interface = new { displayName = "omodot Local Plugins" },
            plugins = new[]
            {
                new
                {
                    name = PluginName,
                    source = new { source = "local", path = $"./plugins/{PluginName}" },
                    policy = new { installation = "AVAILABLE", authentication = "ON_INSTALL" },
                    category = "Developer Tools"
                }
            }
        };
        File.WriteAllText(Path.Combine(basePath, "marketplace.json"),
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WritePluginJson(string pluginsDir)
    {
        var json = new
        {
            name = PluginName,
            version = PluginVersion,
            description = "omodot — .NET OMO toolkit as a full Codex plugin with hooks, skills, agents, and MCP.",
            authors = new[] { new { name = "Lina Lab" } },
            license = "MIT",
            keywords = new[] { "codex", "omodot", "hooks", "skills", "agents", "mcp" },
            skills = "./skills/",
            hooks = "./hooks/hooks.json",
            mcpServers = "./.mcp.json",
            @interface = new
            {
                displayName = "omodot",
                shortDescription = ".NET OMO toolkit for Codex",
                longDescription = "omodot exposes rules engine, comment checking, ultrawork, boulder tracking, AST search, and Codex dispatch as a unified Codex plugin.",
                developerName = "Lina Lab",
                category = "Developer Tools",
                capabilities = new[] { "Hooks", "MCP Tools", "Code Intelligence", "Workflow" },
                defaultPrompt = new[]
                {
                    "Run omodot rules check on this file",
                    "Show omodot LSP diagnostics",
                    "Dispatch a task via omodot codex"
                },
                brandColor = "#2D6A4F"
            }
        };
        File.WriteAllText(Path.Combine(pluginsDir, ".codex-plugin", "plugin.json"),
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteMcpJson(string pluginsDir)
    {
        var json = new
        {
            mcpServers = new
            {
                omodot_codex = new
                {
                    command = "omodot",
                    args = new[] { "mcp" },
                    description = "omodot MCP server: codex dispatch, session management, AST search"
                }
            }
        };
        File.WriteAllText(Path.Combine(pluginsDir, ".mcp.json"),
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteHooksJson(string pluginsDir)
    {
        var json = new
        {
            hooks = new
            {
                SessionStart = new[]
                {
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = "omodot hook session-start",
                                timeout = 10,
                                statusMessage = "loading omodot project rules"
                            }
                        }
                    }
                },
                UserPromptSubmit = new[]
                {
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = "omodot hook user-prompt-submit",
                                timeout = 10,
                                statusMessage = "checking omodot rules and workflow triggers"
                            }
                        }
                    }
                },
                PostToolUse = new[]
                {
                    new
                    {
                        matcher = "^(apply_patch|write|Write|edit|Edit|multi_edit|multiedit|MultiEdit)$",
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = "omodot hook post-tool-use",
                                timeout = 30,
                                statusMessage = "checking omodot comments and diagnostics"
                            }
                        }
                    }
                },
                PostCompact = new[]
                {
                    new
                    {
                        matcher = "manual|auto",
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = "omodot hook post-compact",
                                timeout = 10,
                                statusMessage = "resetting omodot rule cache"
                            }
                        }
                    }
                }
            }
        };
        File.WriteAllText(Path.Combine(pluginsDir, "hooks", "hooks.json"),
            JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteSkills(string pluginsDir)
    {
        var skills = new Dictionary<string, string>
        {
            ["rules"] = @"# omodot Rules Engine

## Description
Project-specific rules engine that loads and matches rules from `.omodot/rules/` directories.

## When to use
- When you need to check project-specific coding rules
- When loading rule context at session start
- When validating changes against project conventions

## Usage
Rules are loaded automatically on session start. They match against file patterns and inject context into the AI conversation.
",
            ["comment-checker"] = @"# omodot Comment Checker

## Description
Detects AI-generated code comments and flags them for review or removal.

## When to use
- After writing or editing code files
- When checking for stale AI comments
- During code review

## Patterns detected
- `// TODO:`, `// FIXME:`, `// HACK:`
- AI artifact comments like `// Add your code here`
- Boilerplate placeholder comments
",
            ["debugging"] = @"# omodot Debugging

## Description
Structured debugging methodology with investigation, oracle escalation, and fix verification.

## Workflow
1. Setup: Reproduce the issue reliably
2. Investigate: Gather evidence, form hypotheses
3. Oracle: Escalate to high-reasoning model after 2+ failed attempts
4. Fix: Apply minimal targeted fix
5. QA: Verify fix without regression
6. Cleanup: Remove debug artifacts
",
            ["review-work"] = @"# omodot Review Work

## Description
Post-implementation review orchestrator. Runs parallel verification agents.

## Checklist
- Goal/constraint verification
- Code quality review
- Security audit
- Hands-on QA execution
- Context mining from history
",
            ["refactor"] = @"# omodot Refactor

## Description
Intelligent refactoring with LSP, AST-grep, and TDD verification.

## Approach
1. Assess current structure
2. Plan transformation with verification gates
3. Apply changes incrementally
4. Verify with build + test at each step
",
            ["ast-search"] = @"# omodot AST Search

## Description
AST-based code search and transformation using AST-grep patterns.

## When to use
- Finding code patterns across the codebase
- Structural code transformation
- Pattern-based refactoring
",
            ["programming"] = @"# omodot Programming

## Description
General programming skill with language-specific references and best practices.

## Supported languages
- TypeScript/JavaScript
- C#/.NET
- Python
- Go
- Rust
",
            ["ultrawork"] = @"# omodot Ultrawork

## Description
Goal-oriented autonomous work loop with evidence capture and verification.

## Workflow
1. Define clear goal with success criteria
2. Execute in bounded iterations
3. Capture evidence at each step
4. Verify completion against criteria
5. Report results with proof
",
            ["boulder"] = @"# omodot Boulder

## Description
Task tracking and goal management system inspired by Sisyphus.

## Features
- Structured todo management
- Progress tracking with verification
- Evidence-based completion
- Session continuity across compactions
"
        };

        foreach (var (name, content) in skills)
        {
            var skillDir = Path.Combine(pluginsDir, "skills", name);
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), content.TrimStart());
        }
    }

    private static void WriteAgents(string pluginsDir)
    {
        var agents = new (string Name, AgentSpec Spec)[]
        {
            ("explorer", new AgentSpec("omodot-explorer", "Codebase search specialist for Codex sessions. Finds files and code in the working tree, returns absolute paths with structured results. Read-only.", "gpt-5.4-mini", "low", "fast", "Explorer")),
            ("librarian", new AgentSpec("omodot-librarian", "External open-source codebase and documentation researcher. Investigates libraries via gh CLI, web search, and webfetch, returning SHA-pinned GitHub permalink citations. Read-only.", "gpt-5.4-mini", "low", "fast", "Librarian")),
            ("plan", new AgentSpec("omodot-plan", "Strategic planning consultant. Produces a single executable work plan from a vague or large request. Planner only - never implements.", "gpt-5.5", "xhigh", "fast", "Planner")),
            ("codex-ultrawork-reviewer", new AgentSpec("codex-ultrawork-reviewer", "Strict ultrawork verification reviewer. Use after full QA evidence to audit the diff, goal, and scenario evidence before declaring done.", "gpt-5.2", "xhigh", null, "Verifier")),
            ("oracle", new AgentSpec("omodot-oracle", "Read-only high-reasoning consultation agent. Use for complex architecture decisions, hard debugging after 2+ failed attempts, and multi-system tradeoffs.", "gpt-5.5", "xhigh", null, "Oracle")),
            ("metis", new AgentSpec("omodot-metis", "Pre-planning consultant that analyzes requests to identify hidden intentions, ambiguities, and AI failure points before planning begins.", "gpt-5.5", "xhigh", null, "Metis")),
            ("momus", new AgentSpec("omodot-momus", "Expert reviewer for evaluating work plans against rigorous clarity, verifiability, and completeness standards.", "gpt-5.5", "xhigh", null, "Momus")),
            ("build", new AgentSpec("omodot-build", "Default task executor. Executes tools based on configured permissions. The workhorse agent for implementation tasks.", "gpt-5.5", "medium", null, "Builder")),
            ("comment-checker", new AgentSpec("omodot-comment-checker", "Detects and flags AI-generated code comments, stale TODOs, and boilerplate placeholders in source files.", "gpt-5.4-mini", "low", "fast", "CommentGuard")),
            ("lsp-diagnostics", new AgentSpec("omodot-lsp-diagnostics", "LSP diagnostics agent that checks type errors, warnings, and code quality issues across source files.", "gpt-5.4-mini", "low", "fast", "DiagnosticsGuard")),
            ("rules-engine", new AgentSpec("omodot-rules-engine", "Project rules matching agent. Loads rules from .omodot/rules/ directories and validates changes against project conventions.", "gpt-5.4-mini", "low", "fast", "RulesGuard")),
            ("session-manager", new AgentSpec("omodot-session-manager", "Session management and continuity agent. Handles context compaction recovery, session snapshots, and progress tracking.", "gpt-5.4-mini", "low", "fast", "SessionGuard")),
            ("background-agent", new AgentSpec("omodot-background-agent", "Background task execution agent. Manages long-running tasks, polling, and result collection.", "gpt-5.4-mini", "low", "fast", "BackgroundWorker")),
            ("ultragoal", new AgentSpec("omodot-ultragoal", "Goal tracking and steering agent. Manages ultrawork goals, quality gates, and evidence-based progress tracking.", "gpt-5.4-mini", "medium", null, "GoalKeeper")),
        };

        foreach (var (fileName, spec) in agents)
        {
            var toml = spec.ToToml();
            File.WriteAllText(Path.Combine(pluginsDir, "agents", $"{fileName}.toml"), toml);
        }
    }

    private sealed record AgentSpec(
        string Name,
        string Description,
        string Model,
        string ReasoningEffort,
        string? ServiceTier,
        string? Nickname)
    {
        public string ToToml()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[agent]");
            sb.AppendLine($"name = \"{Name}\"");
            sb.AppendLine($"description = \"{Description}\"");
            if (Nickname is not null)
                sb.AppendLine($"nickname_candidates = [\"{Nickname}\"]");
            sb.AppendLine($"model = \"{Model}\"");
            sb.AppendLine($"model_reasoning_effort = \"{ReasoningEffort}\"");
            if (ServiceTier is not null)
                sb.AppendLine($"service_tier = \"{ServiceTier}\"");
            return sb.ToString();
        }
    }
}
