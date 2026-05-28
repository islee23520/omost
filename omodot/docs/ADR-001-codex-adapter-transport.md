# ADR-001: Omodot.CodexAdapter — Spawn+JSONL Transport

## Status

Accepted

## Context

The omodot toolkit needs a runtime adapter that drives the Codex CLI
(`codex exec --experimental-json`) as a child process.  The adapter must:

- live as an **internal** omodot package (no public NuGet in v1)
- implement `IUlwHost` from `Omodot.UlwHostContract`
- remain testable without a real Codex installation

## Decision

Package `Omodot.CodexAdapter` uses **spawn + JSONL** transport:

1. `CodexProcessRunner` spawns `codex exec --experimental-json` via
   `System.Diagnostics.Process`.
2. Stdout is streamed line-by-line through `CodexJsonlParser`, which maps
   each JSON object to a typed `CodexAdapterEvent`.
3. `CodexUlwHost` implements `IUlwHost`, forwarding Idle/Completed/Error
   events and mapping process exit codes to session status strings.
4. `CodexAdapterFactory.Create(CodexAdapterOptions)` returns a
   `CodexAdapterRuntime` holding both the `IUlwHost` and the resolved
   `CodexResolvedConfig`.

### Canonical composition path

`CodexComposedOmoRuntime.CreateFromAdapter(CodexAdapterOptions)` in
`Omodot.StandaloneRuntime` is the recommended entrypoint.  The legacy
`Create(ICodexConversationClient)` overload is retained for backward
compatibility.

## Out of Scope (v1)

- **app-server-protocol** — no daemon / persistent server mode
- **Codex installation management** — the binary must be pre-installed
- **Live-auth automation** — authentication is the caller's responsibility
- **NuGet distribution** — internal package only
- **Todo extraction** — `ReadTodosAsync` returns an empty list (explicit stub)

## Consequences

- Consumers add one project reference (`Omodot.CodexAdapter`) and call
  `CodexAdapterFactory.Create`.
- Tests use shell scripts as mock Codex processes; no network access needed.
- Adding a new transport (e.g., HTTP long-poll) requires a separate adapter
  package, not changes to this one.

## Boundary note (Revision 2)

This ADR is a transport decision for the Codex edge adapter. PRD Revision 2 also clarifies the boundary narrative:

- `omodot` is a **modular .NET Agent OS SDK**.
- Codex is the **first reference host / first reference adapter**, not the product boundary.
- `Omodot.StandaloneRuntime` is a reference composition root, not a mandatory architecture.

Dependency direction reminder:

```text
Contracts -> Pure capability packages -> Runtime orchestration -> Composition SDK -> Host adapters -> Distribution
```

Forbidden direction:

```text
Core/capability/runtime -> adapters
```
