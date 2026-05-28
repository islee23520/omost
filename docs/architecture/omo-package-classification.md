# OMO Package Layer Ownership Classification

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Scope:** All packages now consolidated under `omodot/`

## Summary

All 27 original TypeScript packages under `packages/*` have been converted to .NET and consolidated under `omodot/src/`. The TypeScript runtime surfaces have been removed in a one-shot cutover.

## Revision 2 boundary narrative (SDK-first)

PRD Revision 2 clarifies the boundary story:

- `omodot` is a **modular .NET Agent OS SDK**.
- OMO is the source architecture and first inspiration set (ideas/patterns), not a required product boundary.
- Codex is the **first reference host / first reference adapter**, not the product boundary.
- `Omodot.StandaloneRuntime` is a reference composition root (example wiring), not a mandatory architecture.

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

All packages live under `omodot/src/` as .NET projects targeting `net10.0`.

## Required package boundary matrix (PRD §12)

Before implementation handoff, every `omodot/src/*` project must be classified.

| Package | Status | Boundary rule |
|---|---|---|
| `Omodot.UlwHostContract` | stable public v0 candidate | may not depend on runtime/adapters |
| `Omodot.Protocol` | stable public v0 candidate | protocol DTO/framing only; no host policy |
| `Omodot.HashLine` | stable public v0 candidate | pure capability package |
| `Omodot.RulesEngine` | stable public v0 candidate | pure capability package |
| `Omodot.AgentsMd` | stable public v0 candidate | may depend on rules/core utilities only |
| `Omodot.ModelCore` | experimental public | model/provider policy evolves quickly |
| `Omodot.SkillMcp` | experimental public | skill-embedded MCP parsing, no host lifecycle ownership |
| `Omodot.SkillsCore` | experimental public | OMO-inspired defaults; should be replaceable |
| `Omodot.UlwKernel` | experimental public | runtime policy; depends on contracts/state |
| `Omodot.UlwLoopState` | stable/experimental public | state abstractions and stores |
| `Omodot.Hooks` | experimental public | OMO-inspired hook catalog; no host-specific APIs |
| `Omodot.TeamModeCore` | experimental public | portable team data/model only |
| `Omodot.Tmux` | experimental public | local terminal capability, no host ownership |
| `Omodot.TmuxSubagent` | experimental public | portable subagent decision logic |
| `Omodot.SearchTools` | experimental public | grep/glob process helpers |
| `Omodot.AstGrep` | experimental public | ast-grep core helpers |
| `Omodot.AstGrepMcp` | adapter/tooling | MCP wrapper over ast-grep core |
| `Omodot.LspTools` | adapter/tooling | LSP/MCP-oriented tooling |
| `Omodot.CommandExecutor` | experimental/internal | command execution helpers; security review before stable |
| `Omodot.CommentChecker` | experimental public | portable quality gate |
| `Omodot.GitWorktree` | experimental public | git porcelain/diff helpers |
| `Omodot.SessionManager` | experimental public | session formatting/search |
| `Omodot.SlashCommand` | experimental public | host-neutral command discovery/conversion only |
| `Omodot.Utils` | internal or stable primitives | split stable primitives from kitchen-sink utilities if needed |
| `Omodot.StandaloneRuntime` | reference composition | not the SDK product boundary |
| `Omodot.CodexAdapter` | host adapter | may depend on core contracts, not vice versa |
| `Omodot.CodexMcpBridge` | host adapter/distribution | Codex MCP bridge; no core dependency reversal |
| `Omodot.CodexAdapter.Demo` | demo | not public SDK surface |
| `Omodot.Sidecar` | distribution/tooling | protocol process host; uses injectable executor seam |
| `Omodot.BoulderState` | experimental public | workflow state; package status depends on API review |
| `Omodot.BackgroundAgent` | experimental public | portable lifecycle only; host spawning remains adapter-owned |
| `Omodot.UlwIntent` | experimental public | keyword/intent policy; replaceable |

### Core Packages (formerly `shared-sdk`)

| .NET Project | Former TS Package | Purpose |
|---|---|---|
| `Omodot.AgentsMd` | `agents-md-core` | AGENTS.md discovery, formatting, cache, injection |
| `Omodot.AstGrep` | `ast-grep-core` | ast-grep argument building, result formatting, runner |
| `Omodot.CommandExecutor` | `command-executor-core` | Shell-path resolution, command execution helpers |
| `Omodot.CommentChecker` | `comment-checker-core` | Apply-patch parsing, comment-checker runner |
| `Omodot.GitWorktree` | `git-worktree-core` | Git porcelain/diff parsing and formatting |
| `Omodot.HashLine` | `hashline-core` | Hash-anchored edit, diff, validation, normalization |
| `Omodot.ModelCore` | `model-core` | Model normalization, availability, resolution |
| `Omodot.RulesEngine` | `rules-engine` | Rule discovery, AGENTS.md lookup, matching, parsing |
| `Omodot.SearchTools` | `search-tools-core` | Glob/grep CLI resolution, process collection, formatting |
| `Omodot.SessionManager` | `session-manager-core` | Session record formatting, filtering, search |
| `Omodot.SkillMcp` | `skill-mcp-core` | Skill-embedded MCP config parsing, argument coercion |
| `Omodot.SkillsCore` | `skills-core` | Built-in skill catalog and definitions |
| `Omodot.SlashCommand` | `slashcommand-core` | Slash-command discovery and hook-shape conversion |
| `Omodot.UlwHostContract` | `ulw-host-contract` | ULW host seam: `IUlwHost`, prompt receipts, messages |
| `Omodot.Utils` | `utils` | General utilities: frontmatter, JSONC, logging, paths |

### Runtime Packages (formerly `omots-only`)

| .NET Project | Former TS Package | Purpose |
|---|---|---|
| `Omodot.BackgroundAgent` | `background-agent-core` | Background-task lifecycle, polling, concurrency |
| `Omodot.BoulderState` | `boulder-state` | Boulder/Prometheus workflow state management |
| `Omodot.Hooks` | `hooks-core` | OMO hook catalog, guards, recovery behavior |
| `Omodot.StandaloneRuntime` | `standalone-runtime` | Composition root wiring together all packages |
| `Omodot.TeamModeCore` | `team-mode-core` | Team registry, tasklist, mailbox, session registry |
| `Omodot.Tmux` | `tmux-core` | Tmux detection, runner, layout, pane management |
| `Omodot.TmuxSubagent` | `tmux-subagent-core` | Tmux subagent action execution, spawn decisions |
| `Omodot.UlwIntent` | `ulw-intent` | ULW/Hyperplan keyword detection, intent handling |
| `Omodot.UlwKernel` | `ulw-kernel` | ULW loop engine, continuation, verification |
| `Omodot.UlwLoopState` | `ulw-loop-state` | ULW state persistence, verification markers |

### Bridge Packages (formerly `bridge-only`)

| .NET Project | Former TS Package | Purpose |
|---|---|---|
| `Omodot.AstGrepMcp` | `ast-grep-mcp` | MCP stdio/JSON-RPC surface for ast-grep |
| `Omodot.LspTools` | `lsp-tools-mcp` | MCP stdio server for LSP diagnostics |

### Codex Integration Packages (new)

| .NET Project | Purpose |
|---|---|
| `Omodot.CodexAdapter` | Codex CLI process management, JSON-RPC parsing, ULW host |
| `Omodot.CodexMcpBridge` | MCP-compatible tool server wrapping CodexUlwHost |
| `Omodot.CodexAdapter.Demo` | Demo console application for Codex adapter |

### Protocol Package

| .NET Project | Purpose |
|---|---|
| `Omodot.Protocol` | JSON-RPC 2.0 framing, methods, notifications, error types |
| `Omodot.Sidecar` | Stdio sidecar entry point and dispatch loop |

## Historical Note

The original classification distinguished between `shared-sdk`, `omots-only`, and `bridge-only` ownership under `packages/*`. With the TypeScript-to-.NET conversion and consolidation, all packages share a single ownership model under `omodot/`. The original ownership distinctions are preserved above for archival reference only.
