# OMO Rollout Phases and Ownership Map

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Scope:** Consolidated rollout plan reflecting single-core `omodot/` layout

---

## Purpose

This document describes the rollout phases and ownership map for the OMO platform. The original five-phase plan targeting dual TypeScript/.NET implementations has been superseded by a consolidation around `omodot/` as the sole core tree.

## Revision 2 narrative (SDK-first)

PRD Revision 2 explicitly sets the product boundary and story:

- `omodot` is a **modular .NET Agent OS SDK**.
- OMO is the upstream source architecture + first inspiration set.
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

---

## Phase History

### Phase 1: Architecture + Protocol Freeze ✅ COMPLETE

Architecture freeze, package classification (27 packages converted to .NET), protocol freeze, and boundary validation completed.

### Phase 2: omots + opencode-bridge Vertical Slice ✅ SUPERSEDED

The TypeScript/Bun toolkit (`omots/`) and host bridge (`hosts/opencode-bridge/`) were implemented and validated, then removed in the one-shot cutover to consolidate around `omodot/`.

### Phase 3: omodot Protocol-Compatible Vertical Slice ✅ COMPLETE

`omodot/` .NET Core toolkit implements all Phase 1 capabilities with 27 packages, 59 projects, and full test coverage.

### Phase 4: Cross-Implementation Conformance ✅ COMPLETE (Single Implementation)

With the removal of `omots/`, conformance is validated through `omodot/` alone. Golden transcripts under `protocol-fixtures/golden/` and `protocol-fixtures/phase1/` remain as the conformance baseline.

### Phase 5: Codex Integration ✅ COMPLETE

Codex integration delivered via `Omodot.CodexMcpBridge` — an MCP-compatible tool server wrapping `CodexUlwHost`. Native .NET internal plugin loading was investigated and found unsupported by Codex CLI; the MCP bridge is the supported path.

---

## Current Ownership Map

| Owner Category | Owns | Scope |
|----------------|------|-------|
| `omodot owner` | `omodot/` | .NET Core toolkit implementation. Responsible for protocol-compatible behavior, packaging, and Codex integration. |
| `codex integration owner` | `omodot/src/Omodot.CodexMcpBridge/`, `omodot/src/Omodot.CodexAdapter/` | MCP tool surface and Codex CLI process management. |
| `protocol owner` | `docs/protocol/*` | Canonical JSON-RPC surface, error envelope, versioning, conformance fixtures. |

### Historical Owners (Surfaces Removed)

- `shared-sdk owner` — previously owned `packages/*` (converted to .NET, now within `omodot/`)
- `omots owner` — previously owned `omots/` (removed in cutover)
- `opencode bridge owner` — previously owned `hosts/opencode-bridge/` (removed in cutover)

---

## Codex Integration Status

Codex integration is **delivered** via `Omodot.CodexMcpBridge`:

- 4 MCP tools: `codex_dispatch`, `codex_read_status`, `codex_read_messages`, `codex_abort`
- Architecture: `CodexMcpToolServer` → `CodexUlwHost` → `CodexProcessRunner` → Codex CLI binary
- Native .NET internal plugin loading: **NOT supported** by Codex CLI
- MCP bridge is the **sole supported** integration path

---

## Protocol References

- Frozen protocol spec: `docs/protocol/omo-stdio-jsonrpc.md`
- Versioning policy: `docs/protocol/omo-protocol-versioning.md`
- Compatibility policy: `docs/architecture/omo-compatibility-policy.md`
- Codex adapter transport ADR: `omodot/docs/ADR-001-codex-adapter-transport.md`

## OMO inheritance ledger (PRD §17)

Every inherited OMO idea must be tracked:

| OMO idea | omodot package/preset | Status | Adapter-bound exclusions |
|---|---|---|---|
| ULW loop | `Omodot.UlwKernel`, `Omodot.UlwLoopState` | experimental public | host-specific prompt injection |
| Host seam | `Omodot.UlwHostContract` | stable public v0 candidate | OpenCode session APIs |
| Skills | `Omodot.SkillsCore`, `Omodot.SkillMcp` | experimental public | host skill loading lifecycle |
| Hooks | `Omodot.Hooks` | experimental public | OpenCode hook registration |
| HashLine | `Omodot.HashLine` | stable public v0 candidate | host read-tool injection |
| Rules/AGENTS.md | `Omodot.RulesEngine`, `Omodot.AgentsMd` | stable public v0 candidate | host context injection |
| Team mode | `Omodot.TeamModeCore`, `Omodot.TmuxSubagent` | experimental public | host task spawning/UI |
| Background agents | `Omodot.BackgroundAgent` | experimental public | host-specific process/session management |
| MCP bridge ideas | `Omodot.SkillMcp`, `Omodot.AstGrepMcp`, `Omodot.CodexMcpBridge` | mixed | host MCP lifecycle/OAuth |
| Session memory/search | `Omodot.SessionManager` | experimental public | host storage/session APIs |
| OMO presets | `Omodot.AgentOs.OmoPreset` future | experimental preset | should remain replaceable |

---

**End of document.**
