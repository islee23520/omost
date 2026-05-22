# OMO Platform Architecture Decision Record

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Decision Makers:** Architecture freeze per omost-platform-sdk plan (Task 1)
**Review:** Oracle reviewed and gave final GO

## Layer taxonomy

The repository is organized around `omodot/` as the sole core implementation tree.

- `omodot/` is the .NET Core implementation toolkit. It provides a protocol-compatible implementation of the OMO orchestration surface. All packages, tests, and tooling live under this tree.
- `Omodot.CodexMcpBridge` (within `omodot/src/`) is the supported Codex integration path. It exposes Codex adapter functionality as MCP-compatible tools.
- `Omodot.CodexAdapter` (within `omodot/src/`) provides the underlying Codex CLI process management, JSON-RPC parsing, and ULW host implementation.

Architecture and protocol artifacts live under `docs/architecture/*` and `docs/protocol/*`. These directories are the single source of truth for frozen decisions.

### Historical note

Previous versions of this document described a four-root layout (`packages/*`, `omots/`, `omodot/`, `hosts/opencode-bridge/`). The TypeScript/Bun runtime surfaces (`omots/`, `packages/`, `hosts/`) were removed in a one-shot cutover. Only `omodot/` and documentation survive.

## Ownership

- omodot owner: owns the .NET Core toolkit implementation under `omodot/`. Responsible for protocol-compatible behavior, packaging, and Codex integration.
- Codex integration owner: owns `Omodot.CodexMcpBridge` and `Omodot.CodexAdapter` within `omodot/src/`. Responsible for MCP tool surface and Codex CLI process management.
- Protocol owner: owns `docs/protocol/*` and the canonical JSON-RPC surface, error envelope, versioning, and conformance fixtures.

## Transport decision

Stdio + JSON-RPC 2.0 with Content-Length framing is the canonical cross-runtime interop transport. All Phase 1 methods, notifications, error codes, and capability negotiation are defined over this transport.

## Codex integration path

The supported Codex integration is `Omodot.CodexMcpBridge`, which wraps `CodexUlwHost` with four MCP-compatible tools:

1. `codex_dispatch` — dispatches a prompt to Codex via ULW host
2. `codex_read_status` — reads session status
3. `codex_read_messages` — reads session messages
4. `codex_abort` — aborts a running session

Architecture: `CodexMcpToolServer` → `CodexUlwHost` → `CodexProcessRunner` → Codex CLI binary

Codex supports a manifest/config-driven plugin package surface (`.codex-plugin/plugin.json`, `.mcp.json`, hooks, skills). Native direct .NET assembly/plugin loading is **not supported** by Codex CLI. The supported path is Codex plugin package → `.mcp.json` → `Omodot.CodexMcpBridge` → `Omodot.CodexAdapter`.

## Versioning

Protocol versioning is defined separately in `docs/protocol/omo-protocol-versioning.md`. Version negotiation occurs at `omo.initialize`.

## Boundary rules

- `omodot/` is self-contained with zero external references outside the tree.
- `Omodot.CodexMcpBridge` depends on `Omodot.CodexAdapter` only.
- `Omodot.CodexAdapter` depends on `Omodot.UlwHostContract` and `Omodot.Utils` only.
- No package within `omodot/` depends on any TypeScript runtime asset or external SDK.

## Out of scope

- HTTP/gRPC transport in Phase 1.
- Direct TS package import (TS runtime surfaces have been removed).
- Orchestration logic or policy ownership inside host bridges.
- Native Codex internal plugin loading (unsupported by Codex CLI).
