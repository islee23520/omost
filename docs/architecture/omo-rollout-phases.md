# OMO Rollout Phases and Ownership Map

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Scope:** Consolidated rollout plan reflecting single-core `omodot/` layout

---

## Purpose

This document describes the rollout phases and ownership map for the OMO platform. The original five-phase plan targeting dual TypeScript/.NET implementations has been superseded by a consolidation around `omodot/` as the sole core tree.

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

---

**End of document.**
