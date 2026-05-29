# lfe

`lfe` is a **modular .NET Agent OS SDK**.

- **OMO** is the source architecture + first inspiration set (ideas, naming, patterns) — not a boundary you must preserve.
- **Codex** is the **first reference host / first reference adapter**, not the product boundary.
- `Lfe.StandaloneRuntime` is a **reference composition root** (an example wiring), not the mandatory architecture.

This repository’s docs follow PRD Revision 2: SDK-first, adapters at the edge, and explicit dependency direction.

## Layout

- `Lfe.sln` wires 61 projects: protocol, sidecar, 27 converted packages, Codex integration, and tests.
- `src/Lfe.Protocol` — frozen Phase 1 contract types, JSON-RPC envelopes, error shapes, Content-Length framing.
- `src/Lfe.Sidecar` — stdio sidecar entry and dispatch loop for frozen JSON-RPC methods.
- `src/Lfe.CodexAdapter` — Codex CLI process management, JSON-RPC parsing, ULW host implementation.
- `src/Lfe.CodexMcpBridge` — MCP-compatible tool server wrapping CodexUlwHost (supported Codex integration path).
- `src/Lfe.CodexAdapter.Demo` — demo console application for Codex adapter.
- `src/Lfe.StandaloneRuntime` — composition root wiring together all packages.
- `tests/` — 28 test projects covering all packages.

## Build & Test

```bash
dotnet build Lfe.sln
dotnet test Lfe.sln
```

Publish the sidecar:
```bash
dotnet publish src/Lfe.Sidecar/Lfe.Sidecar.csproj -c Release -o artifacts/sidecar
```

## Codex Integration

The supported Codex integration is `Lfe.CodexMcpBridge`, which is designed for Codex's manifest/config-driven plugin surface (`.codex-plugin/plugin.json` + `.mcp.json`) and exposes 4 MCP tools:

1. `codex_dispatch` — dispatches a prompt to Codex via ULW host
2. `codex_read_status` — reads session status
3. `codex_read_messages` — reads session messages
4. `codex_abort` — aborts a running session

Architecture: `CodexMcpToolServer` → `CodexUlwHost` → `CodexProcessRunner` → Codex CLI binary

Codex plugin packaging is supported through manifests and MCP server declarations. Native direct .NET assembly/plugin loading is **not supported** by Codex CLI. The supported path is Codex plugin package → `.mcp.json` → `Lfe.CodexMcpBridge` → `Lfe.CodexAdapter`.

## Protocol notes

- Transport: stdio only.
- Envelope: JSON-RPC 2.0.
- Framing: `Content-Length` headers.
- Frozen methods: `omo.initialize`, `omo.session.start`, `omo.run.dispatch`, `omo.run.cancel`.
- Frozen notifications: `omo.run.progress`, `omo.run.result`, `omo.run.error`.

## Packaging notes

- All projects target `net10.0` with `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`.
- The solution is self-contained with zero external references outside the `lfe/` tree.
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

- `Lfe.UlwHostContract` is the host seam and must remain adapter-neutral.
- `Lfe.CodexAdapter` / `Lfe.CodexMcpBridge` are edge adapters/distribution; core packages must not depend on them.

## Agent OS Builder (Experimental)

The `AgentOsBuilder` API is Phase A and experimental. It is not a stable public NuGet v0 surface, and it can change without notice.

The snippets below are minimal, design oriented examples. They use simplified placeholder types (`MyModule`, `MyAgent`, `MyWorkflow`). For compiled, real patterns, see `tests/Lfe.AgentOs.Tests/AgentOsBuilderTests.cs`.

### Design-time composition (no host)

Example (conceptual):

```csharp
using Lfe.AgentOs;

var agentOs = new AgentOsBuilder()
    .AddModule(new MyModule("kernel"))
    .BuildDesignTime();
```

### Codex-backed composition (with Codex adapter extension)

Example (conceptual):

```csharp
using Lfe.AgentOs;
using Lfe.CodexAdapter;

var agentOs = new AgentOsBuilder()
    .AddModule(new MyModule("kernel"))
    .UseCodexReferenceHost()
    .Build();
```

### Custom module registration

Example (conceptual):

```csharp
public sealed class MyModule : IAgentOsModule
{
    public string Id => "my-module";
    public string? Version => "1.0.0";
    public IReadOnlyList<string> Requires => [];
    public IReadOnlyList<string> ConflictsWith => [];
    public bool IsPreset => false;
}
```

### Agent and workflow registration

Example (conceptual):

```csharp
var agentOs = new AgentOsBuilder()
    .AddModule(new MyModule("core"))
    .AddAgent(new MyAgent("planner", "Planner"))
    .AddWorkflow(new MyWorkflow("main", "Main Workflow", isDefault: true))
    .UseCodexReferenceHost()
    .Build();
```

### Notes

- Preset modules (`IsPreset = true`) cannot be silently overridden. Use `ReplaceModule()` for explicit replacement.
- `UseCodexReferenceHost()` lives in `Lfe.CodexAdapter`, not in the host neutral SDK. Generic hosts should call `UseHost(IHostAdapter)`.
