using System.Text.Json.Serialization;

namespace Lfe.SkillsCore;

/// <summary>
/// Static definitions of all builtin skills. Template content is the skill prompt/instructions.
/// </summary>
public static class SkillDefinitions
{
    public static BuiltinSkill Playwright => new(
        Name: "playwright",
        Description: "MUST USE for any browser-related tasks. Browser automation via Playwright MCP.",
        Template: """
            # Playwright Browser Automation
            This skill provides browser automation capabilities via the Playwright MCP server.
            Use Playwright MCP tools for browser automation, verification, browsing, web scraping, testing, and screenshots.
            """,
        McpConfig: new(new Dictionary<string, SkillMcpServerConfig>
        {
            ["playwright"] = new(Command: "npx", Args: ["@playwright/mcp@latest"]),
        }));

    public static BuiltinSkill AgentBrowser => new(
        Name: "agent-browser",
        Description: "MUST USE for any browser-related tasks. Browser automation via agent-browser CLI.",
        Template: """
            # Browser Automation with agent-browser
            agent-browser open <url>        # Navigate to page
            agent-browser snapshot -i       # Get interactive elements with refs
            agent-browser click @e1         # Click element by ref
            agent-browser fill @e2 "text"   # Fill input by ref
            agent-browser close             # Close browser
            """,
        AllowedTools: ["Bash(agent-browser:*)"]);

    public static BuiltinSkill PlaywrightCli => new(
        Name: "playwright-cli",
        Description: "MUST USE for any browser-related tasks. Browser automation via Playwright CLI.",
        Template: """
            # Playwright CLI Browser Automation
            Use Playwright CLI commands for browser automation without MCP server.
            """);

    public static BuiltinSkill DevBrowser => new(
        Name: "dev-browser",
        Description: "Persistent page state browser for development work.",
        Template: """
            # Dev Browser
            Persistent browser sessions for development and testing workflows.
            """);

    public static BuiltinSkill FrontendUiUx => new(
        Name: "frontend-ui-ux",
        Description: "Designer-turned-developer who crafts stunning UI/UX even without design mockups",
        Template: """
            # Role: Designer-Turned-Developer
            You are a designer who learned to code. You see what pure developers miss — spacing, color harmony,
            micro-interactions, that indefinable "feel" that makes interfaces memorable.
            
            ## Aesthetic Guidelines
            - Typography: Choose distinctive fonts. Avoid Arial, Inter, Roboto, system fonts.
            - Color: Cohesive palette with CSS variables. Avoid purple gradients on white.
            - Motion: High-impact moments with CSS-first animations.
            - Spatial Composition: Unexpected layouts, asymmetry, generous negative space.
            
            ## Anti-Patterns (NEVER)
            - Generic fonts (Inter, Roboto, Arial, system fonts)
            - Cliched color schemes (purple gradients on white)
            - Predictable layouts and component patterns
            """);

    public static BuiltinSkill GitMaster => new(
        Name: "git-master",
        Description: "MUST USE for ANY git operations. Atomic commits, rebase/squash, history search.",
        Template: """
            # Git Master Agent
            Git expert combining: Commit Architect, Rebase Surgeon, History Archaeologist.
            
            ## MODE DETECTION
            - "commit" / "커밋" → COMMIT mode (Phase 0-6)
            - "rebase" / "squash" → REBASE mode (Phase R1-R4)
            - "find when" / "blame" → HISTORY_SEARCH mode (Phase H1-H3)
            
            ## CORE PRINCIPLE: MULTIPLE COMMITS BY DEFAULT
            3+ files changed → MUST be 2+ commits
            5+ files changed → MUST be 3+ commits
            10+ files changed → MUST be 5+ commits
            """);

    public static BuiltinSkill ReviewWork => new(
        Name: "review-work",
        Description: "Post-implementation review orchestrator. Launches 5 parallel background sub-agents.",
        Template: """
            # Review Work - 5-Agent Parallel Review Orchestrator
            Launch 5 specialized sub-agents in parallel:
            1. Goal Verifier (Oracle) - Did we build what was asked?
            2. QA Executor (unspecified-high) - Does it actually work?
            3. Code Reviewer (Oracle) - Is the code well-written?
            4. Security Auditor (Oracle) - Is it secure?
            5. Context Miner (unspecified-high) - Did we miss any context?
            All 5 must pass for review to pass.
            """);

    public static BuiltinSkill AiSlopRemover => new(
        Name: "ai-slop-remover",
        Description: "Removes AI-generated code smells from a SINGLE file while preserving functionality.",
        Template: """
            # AI Slop Remover
            Remove AI-generated code smells from source files:
            - Overly defensive code (unnecessary null checks, try-catch)
            - Unnecessary comments explaining obvious code
            - Redundant type annotations
            - Over-abstracted factory patterns
            - Verbose error handling for trivial operations
            """);

    public static BuiltinSkill TeamMode => new(
        Name: "team-mode",
        Description: "Team orchestration — create and manage parallel agent teams (OFF by default).",
        Template: """
            # Team Mode
            Parallel multi-agent coordination. Off by default; enable via team_mode.enabled.
            
            ## Lifecycle
            1. team_create → 2. assign work → 3. members report → 4. team_delete when phase done
            
            ## Tools
            - team_create, team_delete (lead-only)
            - team_send_message, team_task_create/update/list/get (universal)
            - team_status, team_list (universal)
            
            ## Bounds: Max 8 members, 4 parallel workers, 32KB/message, 256KB inbox.
            """);
}
