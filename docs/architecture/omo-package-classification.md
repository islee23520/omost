# LFE Package Layer Ownership Classification

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Scope:** All packages now consolidated under `lfe/`

## Summary

All 27 original TypeScript packages under `packages/*` have been converted to .NET and consolidated under `lfe/src/`. The TypeScript runtime surfaces have been removed in a one-shot cutover.

## Revision 2 Boundary (SDK-first)

PRD Revision 2 clarifies the boundary:

- `lfe` is a **modular .NET Agent OS SDK**.
- LFE is the source architecture and first inspiration set (ideas/patterns), not a required product boundary.
- Codex is the **first reference host / first reference adapter**, not the product boundary.
- `Lfe.StandaloneRuntime` is a reference composition root (example wiring), not a mandatory architecture.

### Dependency direction

Allowed direction:

```text
Contracts -> Pure capability packages -> Runtime orchestration -> Composition SDK -> Host adapters -> Distribution
```

Forbidden direction:

```text
Core/capability/runtime -> adapters
```

> Note: The “Composition SDK” (builder + presets) is an architectural layer even if the package is not fully materialized yet.

## Current Package Layout

All packages live under `lfe/src/` as .NET projects targeting `net10.0`.

## Required package boundary matrix (PRD §12)

Before implementation handoff, every `lfe/src/*` project must be classified.

| Package | Status | Boundary rule |
|---|---|---|
| `Lfe.UlwHostContract` | stable public v0 candidate | may not depend on runtime/adapters |
| `Lfe.Protocol` | stable public v0 candidate | protocol DTO/framing only; no host policy |
| `Lfe.HashLine` | stable public v0 candidate | pure capability package |
| `Lfe.RulesEngine` | stable public v0 candidate | pure capability package |
| `Lfe.AgentsMd` | stable public v0 candidate | may depend on rules/core utilities only |
| `Lfe.ModelCore` | experimental public | model/provider policy evolves quickly |
| `Lfe.SkillMcp` | experimental public | skill-embedded MCP parsing, no host lifecycle ownership |
| `Lfe.SkillsCore` | experimental public | LFE-inspired defaults; should be replaceable |
| `Lfe.UlwKernel` | experimental public | runtime policy; depends on contracts/state |
| `Lfe.UlwLoopState` | stable/experimental public | state abstractions and stores |
| `Lfe.Hooks` | experimental public | LFE-inspired hook catalog; no host-specific APIs |
| `Lfe.TeamModeCore` | experimental public | portable team data/model only |
| `Lfe.Tmux` | experimental public | local terminal capability, no host ownership |
| `Lfe.TmuxSubagent` | experimental public | portable subagent decision logic |
| `Lfe.SearchTools` | experimental public | grep/glob process helpers |
| `Lfe.AstGrep` | experimental public | ast-grep core helpers |
| `Lfe.AstGrepMcp` | adapter/tooling | MCP wrapper over ast-grep core |
| `Lfe.LspTools` | adapter/tooling | LSP/MCP-oriented tooling |
| `Lfe.CommandExecutor` | experimental/internal | command execution helpers; security review before stable |
| `Lfe.CommentChecker` | experimental public | portable quality gate |
| `Lfe.GitWorktree` | experimental public | git porcelain/diff helpers |
| `Lfe.SessionManager` | experimental public | session formatting/search |
| `Lfe.SlashCommand` | experimental public | host-neutral command discovery/conversion only |
| `Lfe.Utils` | internal or stable primitives | split stable primitives from kitchen-sink utilities if needed |
| `Lfe.StandaloneRuntime` | reference composition | not the SDK product boundary |
| `Lfe.CodexAdapter` | host adapter | may depend on core contracts, not vice versa |
| `Lfe.CodexMcpBridge` | host adapter/distribution | Codex MCP bridge; no core dependency reversal |
| `Lfe.CodexAdapter.Demo` | demo | not public SDK surface |
| `Lfe.AgentOs` | experimental composition SDK | host-neutral builder, module system, host adapter registry; no adapter dependencies |
| `Lfe.Sidecar` | distribution/tooling | protocol process host; uses injectable executor seam |
| `Lfe.BoulderState` | experimental public | workflow state; package status depends on API review |
| `Lfe.BackgroundAgent` | experimental public | portable lifecycle only; host spawning remains adapter-owned |
| `Lfe.UlwIntent` | experimental public | keyword/intent policy; replaceable |

### Core Packages (formerly `shared-sdk`)

| .NET Project | Former TS Package | Purpose |
|---|---|---|
| `Lfe.AgentsMd` | `agents-md-core` | AGENTS.md discovery, formatting, cache, injection |
| `Lfe.AstGrep` | `ast-grep-core` | ast-grep argument building, result formatting, runner |
| `Lfe.CommandExecutor` | `command-executor-core` | Shell-path resolution, command execution helpers |
| `Lfe.CommentChecker` | `comment-checker-core` | Apply-patch parsing, comment-checker runner |
| `Lfe.GitWorktree` | `git-worktree-core` | Git porcelain/diff parsing and formatting |
| `Lfe.HashLine` | `hashline-core` | Hash-anchored edit, diff, validation, normalization |
| `Lfe.ModelCore` | `model-core` | Model normalization, availability, resolution |
| `Lfe.RulesEngine` | `rules-engine` | Rule discovery, AGENTS.md lookup, matching, parsing |
| `Lfe.SearchTools` | `search-tools-core` | Glob/grep CLI resolution, process collection, formatting |
| `Lfe.SessionManager` | `session-manager-core` | Session record formatting, filtering, search |
| `Lfe.SkillMcp` | `skill-mcp-core` | Skill-embedded MCP config parsing, argument coercion |
| `Lfe.SkillsCore` | `skills-core` | Built-in skill catalog and definitions |
| `Lfe.SlashCommand` | `slashcommand-core` | Slash-command discovery and hook-shape conversion |
| `Lfe.UlwHostContract` | `ulw-host-contract` | ULW host seam: `IUlwHost`, prompt receipts, messages |
| `Lfe.Utils` | `utils` | General utilities: frontmatter, JSONC, logging, paths |

### Runtime Packages (formerly `lfets-only`)

| .NET Project | Former TS Package | Purpose |
|---|---|---|
| `Lfe.BackgroundAgent` | `background-agent-core` | Background-task lifecycle, polling, concurrency |
| `Lfe.BoulderState` | `boulder-state` | Boulder/Prometheus workflow state management |
| `Lfe.Hooks` | `hooks-core` | LFE hook catalog, guards, recovery behavior |
| `Lfe.StandaloneRuntime` | `standalone-runtime` | Composition root wiring together all packages |
| `Lfe.TeamModeCore` | `team-mode-core` | Team registry, tasklist, mailbox, session registry |
| `Lfe.Tmux` | `tmux-core` | Tmux detection, runner, layout, pane management |
| `Lfe.TmuxSubagent` | `tmux-subagent-core` | Tmux subagent action execution, spawn decisions |
| `Lfe.UlwIntent` | `ulw-intent` | ULW/Hyperplan keyword detection, intent handling |
| `Lfe.UlwKernel` | `ulw-kernel` | ULW loop engine, continuation, verification |
| `Lfe.UlwLoopState` | `ulw-loop-state` | ULW state persistence, verification markers |

### Bridge Packages (formerly `bridge-only`)

| .NET Project | Former TS Package | Purpose |
|---|---|---|
| `Lfe.AstGrepMcp` | `ast-grep-mcp` | MCP stdio/JSON-RPC surface for ast-grep |
| `Lfe.LspTools` | `lsp-tools-mcp` | MCP stdio server for LSP diagnostics |

### Codex Integration Packages (new)

| .NET Project | Purpose |
|---|---|
| `Lfe.CodexAdapter` | Codex CLI process management, JSON-RPC parsing, ULW host |
| `Lfe.CodexMcpBridge` | MCP-compatible tool server wrapping CodexUlwHost |
| `Lfe.CodexAdapter.Demo` | Demo console application for Codex adapter |

### Protocol Package

| .NET Project | Purpose |
|---|---|
| `Lfe.Protocol` | JSON-RPC 2.0 framing, methods, notifications, error types |
| `Lfe.Sidecar` | Stdio sidecar entry point and dispatch loop |

## Historical Note

The original classification distinguished between `shared-sdk`, `lfets-only`, and `bridge-only` ownership under `packages/*`. With the TypeScript-to-.NET conversion and consolidation, all packages share a single ownership model under `lfe/`. The original ownership distinctions are preserved above for archival reference only.
