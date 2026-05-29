# OMO Compatibility and Conformance Policy

**Status:** Updated (post-cutover)
**Date:** 2026-05-27 (updated)
**Original Date:** 2026-05-24
**Scope:** Phase 1 bounded vertical slice (single implementation)

---

## 1. Purpose

This document defines the exact compatibility surface for Phase 1 and the release gate that `lfe` must satisfy.

Compatibility is NOT defined by "full OMO parity." It is defined by passing a bounded, explicitly enumerated set of capabilities and their shared fixtures.

---

## 2. Phase 1 Capabilities (Frozen)

Phase 1 defines exactly six bounded capabilities. No additional capabilities may be added without a protocol version bump.

**Phase 1 capabilities:**
- `phase1.initialize`
- `phase1.session-start`
- `phase1.run-dispatch`
- `phase1.run-progress`
- `phase1.run-result`
- `phase1.run-cancel`

These six names are the complete and final Phase 1 capability set. Any server that advertises `acceptedCapabilities` MUST limit its list to a subset of these six. A client MUST NOT request capabilities outside this list during Phase 1.

---

## 3. Implementation Conformance

`lfe/` (.NET Core) is the sole implementation of the protocol. Conformance is measured by passing the golden transcript fixtures, not by matching any reference implementation structure.

- The implementation is authoritative for Phase 1 conformance.
- Conformance is measured by passing golden transcripts under `protocol-fixtures/golden/`.
- The implementation MUST NOT claim "full OMO parity" as a substitute for fixture-based conformance.

### Historical Note

The original policy defined `omots` (TypeScript/Bun) and `lfe` (.NET Core) as peer implementations. The TypeScript implementation was removed in the one-shot cutover. Cross-implementation fixture parity is no longer applicable.

---

## 4. Release Gate

A protocol release (any version bump) is valid only when the following conditions are met:

1. `lfe` passes the complete set of golden transcripts stored under `protocol-fixtures/golden/`.
2. The implementation advertises the correct `protocolVersion` string.
3. The implementation accepts exactly the correct subset of the six Phase 1 capabilities for that version.
4. No structural changes to methods, notifications, or error envelopes have occurred outside the rules defined in `docs/protocol/omo-protocol-versioning.md`.

**Golden transcripts are the single source of truth for release approval.**

---

## 5. Conformance Fixtures

Shared fixtures under `protocol-fixtures/golden/` cover:

- `omo.initialize` (version negotiation + capability exchange)
- `omo.session.start`
- `omo.run.dispatch`
- `omo.run.cancel`
- `omo.run.progress` (all phases: queued, running, tool, completed, failed, cancelled)
- `omo.run.result`
- `omo.run.error`
- Error envelope (all numeric codes + `omoCode` combinations)
- Framing edge cases (partial reads, large payloads, malformed headers)

---

## 6. Phase 2+ Extension Rules

Phase 2 and later phases MAY introduce new capabilities (e.g., `phase2.tool-invocation`, `phase2.multi-agent`).

**Rules:**

- New capabilities MUST be optional.
- Existing Phase 1 contracts MUST remain unchanged.
- A server that supports Phase 2 capabilities MUST still accept all Phase 1 capabilities without modification.
- Clients that only request Phase 1 capabilities MUST continue to work against Phase 2+ servers.

Phase 2+ cannot break Phase 1 contracts. Any breaking change requires a major protocol version bump.

---

## 7. Codex Integration

Codex integration is delivered via `Lfe.CodexMcpBridge`:

- The MCP bridge wraps `CodexUlwHost` with 4 MCP-compatible tools.
- Codex integration is NOT part of Phase 1 protocol conformance.
- Codex plugin packaging is manifest/config-driven; direct native .NET assembly/plugin loading is unsupported by Codex CLI.
- The supported path is Codex plugin package → `.mcp.json` → `Lfe.CodexMcpBridge` → `Lfe.CodexAdapter`.

---

## 8. QA Verification

To verify this policy is correctly documented:

```bash
grep -n "Phase 1 capabilities" docs/architecture/omo-compatibility-policy.md
```

Expected output lists exactly the six names above.

```bash
dotnet build lfe/Lfe.sln
dotnet test lfe/Lfe.sln
```

Both commands must exit 0.

---

**End of document.**
