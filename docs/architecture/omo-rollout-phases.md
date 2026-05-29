# LFE Rollout Phases and Ownership Map

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Scope:** Consolidated rollout plan reflecting single-core `lfe/` layout

---

## Purpose

This document describes the rollout phases and ownership map for the LFE platform. The original five-phase plan targeting dual TypeScript/.NET implementations has been superseded by a consolidation around `lfe/` as the sole core tree.

## Revision 2 Boundary (SDK-first)

PRD Revision 2 explicitly sets the product boundary:

- `lfe` is a **modular .NET Agent OS SDK**.
- LFE is the upstream source architecture + first inspiration set.
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

---

## Phase History

### Phase 1: Architecture + Protocol Freeze ✅ COMPLETE

Architecture freeze, package classification (27 packages converted to .NET), protocol freeze, and boundary validation completed.

### Phase 2: lfets + opencode-bridge Vertical Slice ✅ SUPERSEDED

The TypeScript/Bun toolkit (`lfets/`) and host bridge (`hosts/opencode-bridge/`) were implemented and validated, then removed in the one-shot cutover to consolidate around `lfe/`.

### Phase 3: lfe Protocol-Compatible Vertical Slice ✅ COMPLETE

`lfe/` .NET Core toolkit implements all Phase 1 capabilities with 27 packages, 59 projects, and full test coverage.

### Phase 4: Cross-Implementation Conformance ✅ COMPLETE (Single Implementation)

With the removal of `lfets/`, conformance is validated through `lfe/` alone. Golden transcripts under `protocol-fixtures/golden/` and `protocol-fixtures/phase1/` remain as the conformance baseline.

### Phase 5: Codex Integration ✅ COMPLETE

Codex integration delivered via `Lfe.CodexMcpBridge` — an MCP-compatible tool server wrapping `CodexUlwHost`. Native .NET internal plugin loading was investigated and found unsupported by Codex CLI; the MCP bridge is the supported path.

---

## Current Ownership Map

| Owner Category | Owns | Scope |
|----------------|------|-------|
| `lfe owner` | `lfe/` | .NET Core toolkit implementation. Responsible for protocol-compatible behavior, packaging, and Codex integration. |
| `codex integration owner` | `lfe/src/Lfe.CodexMcpBridge/`, `lfe/src/Lfe.CodexAdapter/` | MCP tool surface and Codex CLI process management. |
| `protocol owner` | `docs/protocol/*` | Canonical JSON-RPC surface, error envelope, versioning, conformance fixtures. |

### Historical Owners (Surfaces Removed)

- `shared-sdk owner` — previously owned `packages/*` (converted to .NET, now within `lfe/`)
- `lfets owner` — previously owned `lfets/` (removed in cutover)
- `opencode bridge owner` — previously owned `hosts/opencode-bridge/` (removed in cutover)

---

## Codex Integration Status

Codex integration is **delivered** via `Lfe.CodexMcpBridge`:

- 4 MCP tools: `codex_dispatch`, `codex_read_status`, `codex_read_messages`, `codex_abort`
- Architecture: `CodexMcpToolServer` → `CodexUlwHost` → `CodexProcessRunner` → Codex CLI binary
- Native .NET internal plugin loading: **NOT supported** by Codex CLI
- MCP bridge is the **sole supported** integration path

---

## Protocol References

- Frozen protocol spec: `docs/protocol/lfe-stdio-jsonrpc.md`
- Versioning policy: `docs/protocol/lfe-protocol-versioning.md`
- Compatibility policy: `docs/architecture/lfe-compatibility-policy.md`
- Codex adapter transport ADR: `lfe/docs/ADR-001-codex-adapter-transport.md`

## LFE inheritance ledger (PRD §17)

Every inherited LFE idea must be tracked:

| LFE idea | lfe package/preset | Status | Adapter-bound exclusions |
|---|---|---|---|
| ULW loop | `Lfe.UlwKernel`, `Lfe.UlwLoopState` | experimental public | host-specific prompt injection |
| Host seam | `Lfe.UlwHostContract` | stable public v0 candidate | OpenCode session APIs |
| Skills | `Lfe.SkillsCore`, `Lfe.SkillMcp` | experimental public | host skill loading lifecycle |
| Hooks | `Lfe.Hooks` | experimental public | OpenCode hook registration |
| HashLine | `Lfe.HashLine` | stable public v0 candidate | host read-tool injection |
| Rules/AGENTS.md | `Lfe.RulesEngine`, `Lfe.AgentsMd` | stable public v0 candidate | host context injection |
| Team mode | `Lfe.TeamModeCore`, `Lfe.TmuxSubagent` | experimental public | host task spawning/UI |
| Background agents | `Lfe.BackgroundAgent` | experimental public | host-specific process/session management |
| MCP bridge ideas | `Lfe.SkillMcp`, `Lfe.AstGrepMcp`, `Lfe.CodexMcpBridge` | mixed | host MCP lifecycle/OAuth |
| Session memory/search | `Lfe.SessionManager` | experimental public | host storage/session APIs |
| LFE presets | `Lfe.AgentOs.LfePreset` future | experimental preset | should remain replaceable |

---

**End of document.**
