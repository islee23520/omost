# LFE Platform Architecture Decision Record

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Decision Makers:** Architecture freeze per lfest-platform-sdk plan (Task 1)
**Review:** Oracle reviewed and gave final GO

## Layer taxonomy

The repository is organized around `lfe/` as the sole core implementation tree.

- `lfe/` is the .NET Core implementation toolkit. It provides a protocol-compatible implementation of the LFE orchestration surface. All packages, tests, and tooling live under this tree.
- `Lfe.CodexMcpBridge` (within `lfe/src/`) is the supported Codex integration path. It exposes Codex adapter functionality as MCP-compatible tools.
- `Lfe.CodexAdapter` (within `lfe/src/`) provides the underlying Codex CLI process management, JSON-RPC parsing, and ULW host implementation.

Architecture and protocol artifacts live under `docs/architecture/*` and `docs/protocol/*`. These directories are the single source of truth for frozen decisions.

### Historical note

Previous versions of this document described a four-root layout (`packages/*`, `lfets/`, `lfe/`, `hosts/opencode-bridge/`). The TypeScript/Bun runtime surfaces (`lfets/`, `packages/`, `hosts/`) were removed in a one-shot cutover. Only `lfe/` and documentation survive.

## Ownership

- lfe owner: owns the .NET Core toolkit implementation under `lfe/`. Responsible for protocol-compatible behavior, packaging, and Codex integration.
- Codex integration owner: owns `Lfe.CodexMcpBridge` and `Lfe.CodexAdapter` within `lfe/src/`. Responsible for MCP tool surface and Codex CLI process management.
- Protocol owner: owns `docs/protocol/*` and the canonical JSON-RPC surface, error envelope, versioning, and conformance fixtures.

## Transport decision

Stdio + JSON-RPC 2.0 with Content-Length framing is the canonical cross-runtime interop transport. All Phase 1 methods, notifications, error codes, and capability negotiation are defined over this transport.

## Codex integration path

The supported Codex integration is `Lfe.CodexMcpBridge`, which wraps `CodexUlwHost` with four MCP-compatible tools:

1. `codex_dispatch` — dispatches a prompt to Codex via ULW host
2. `codex_read_status` — reads session status
3. `codex_read_messages` — reads session messages
4. `codex_abort` — aborts a running session

Architecture: `CodexMcpToolServer` → `CodexUlwHost` → `CodexProcessRunner` → Codex CLI binary

Codex supports a manifest/config-driven plugin package surface (`.codex-plugin/plugin.json`, `.mcp.json`, hooks, skills). Native direct .NET assembly/plugin loading is **not supported** by Codex CLI. The supported path is Codex plugin package → `.mcp.json` → `Lfe.CodexMcpBridge` → `Lfe.CodexAdapter`.

## Versioning

Protocol versioning is defined separately in `docs/protocol/lfe-protocol-versioning.md`. Version negotiation occurs at `lfe.initialize`.

## Boundary rules

- `lfe/` is self-contained with zero external references outside the tree.
- `Lfe.CodexMcpBridge` depends on `Lfe.CodexAdapter` only.
- `Lfe.CodexAdapter` depends on `Lfe.UlwHostContract` and `Lfe.Utils` only.
- No package within `lfe/` depends on any TypeScript runtime asset or external SDK.

## Out of scope

- HTTP/gRPC transport in Phase 1.
- Direct TS package import (TS runtime surfaces have been removed).
- Orchestration logic or policy ownership inside host bridges.
- Native Codex internal plugin loading (unsupported by Codex CLI).
