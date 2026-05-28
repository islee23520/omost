# omodot

`omodot` is a **modular .NET Agent OS SDK**.

- **OMO** is the source architecture + first inspiration set (ideas, naming, patterns) — not a boundary you must preserve.
- **Codex** is the **first reference host / first reference adapter**, not the product boundary.
- `Omodot.StandaloneRuntime` is a **reference composition root** (an example wiring), not the mandatory architecture.

This repository’s docs follow PRD Revision 2: SDK-first, adapters at the edge, and explicit dependency direction.

## Layout

- `Omodot.sln` wires 61 projects: protocol, sidecar, 27 converted packages, Codex integration, and tests.
- `src/Omodot.Protocol` — frozen Phase 1 contract types, JSON-RPC envelopes, error shapes, Content-Length framing.
- `src/Omodot.Sidecar` — stdio sidecar entry and dispatch loop for frozen JSON-RPC methods.
- `src/Omodot.CodexAdapter` — Codex CLI process management, JSON-RPC parsing, ULW host implementation.
- `src/Omodot.CodexMcpBridge` — MCP-compatible tool server wrapping CodexUlwHost (supported Codex integration path).
- `src/Omodot.CodexAdapter.Demo` — demo console application for Codex adapter.
- `src/Omodot.StandaloneRuntime` — composition root wiring together all packages.
- `tests/` — 28 test projects covering all packages.

## Build & Test

```bash
dotnet build Omodot.sln
dotnet test Omodot.sln
```

Publish the sidecar:
```bash
dotnet publish src/Omodot.Sidecar/Omodot.Sidecar.csproj -c Release -o artifacts/sidecar
```

## Codex Integration

The supported Codex integration is `Omodot.CodexMcpBridge`, which is designed for Codex's manifest/config-driven plugin surface (`.codex-plugin/plugin.json` + `.mcp.json`) and exposes 4 MCP tools:

1. `codex_dispatch` — dispatches a prompt to Codex via ULW host
2. `codex_read_status` — reads session status
3. `codex_read_messages` — reads session messages
4. `codex_abort` — aborts a running session

Architecture: `CodexMcpToolServer` → `CodexUlwHost` → `CodexProcessRunner` → Codex CLI binary

Codex plugin packaging is supported through manifests and MCP server declarations. Native direct .NET assembly/plugin loading is **not supported** by Codex CLI. The supported path is Codex plugin package → `.mcp.json` → `Omodot.CodexMcpBridge` → `Omodot.CodexAdapter`.

## Protocol notes

- Transport: stdio only.
- Envelope: JSON-RPC 2.0.
- Framing: `Content-Length` headers.
- Frozen methods: `omo.initialize`, `omo.session.start`, `omo.run.dispatch`, `omo.run.cancel`.
- Frozen notifications: `omo.run.progress`, `omo.run.result`, `omo.run.error`.

## Packaging notes

- All projects target `net10.0` with `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`.
- The solution is self-contained with zero external references outside the `omodot/` tree.
- The protocol layer stays runtime-neutral and depends only on the frozen docs plus local .NET code.

## Architecture boundary (Revision 2)

### Layering and dependency direction

Allowed direction:

```text
Contracts -> Pure capability packages -> Runtime orchestration -> Composition SDK -> Host adapters -> Distribution
```

Forbidden direction:

```text
Core/capability/runtime -> adapters
```

### Boundary reminders

- `Omodot.UlwHostContract` is the host seam and must remain adapter-neutral.
- `Omodot.CodexAdapter` / `Omodot.CodexMcpBridge` are edge adapters/distribution; core packages must not depend on them.
