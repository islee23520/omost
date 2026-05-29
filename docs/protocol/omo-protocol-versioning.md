# LFE Protocol Versioning Policy

**Status:** Frozen  
**Date:** 2026-05-24  
**Scope:** Phase 1 protocol surface (`lfe.initialize`, `lfe.session.start`, `lfe.run.dispatch`, `lfe.run.cancel`, `lfe.run.progress`, `lfe.run.result`, `lfe.run.error`)

---

## 1. Semantic Versioning

The LFE protocol follows semantic versioning: `major.minor.patch`.

- **Major** (X.0.0): Breaking changes.
- **Minor** (X.Y.0): Backward-compatible additions.
- **Patch** (X.Y.Z): No structural changes.

Both `lfets` and `lfe` MUST advertise identical protocol versions for a given release. Version negotiation occurs exclusively at `lfe.initialize` via `params.protocolVersion`.

---

## 2. Major Version Bump Rules

A major version bump (e.g., 1.x → 2.x) is REQUIRED when any of the following occur:

- Renaming, removing, or changing semantics of existing methods.
- Adding new required fields to request or result shapes.
- Removing methods or notifications.
- Changing the JSON-RPC error envelope structure.
- Altering the Content-Length framing rules.
- Changing capability names or their required semantics.

**Examples of major changes:**
- Renaming `lfe.run.dispatch` to `lfe.run.execute`.
- Making `prompt` optional in `lfe.run.dispatch` when it was previously required.
- Removing the `phase1.run-cancel` capability contract.

**Consequence:** All peer implementations (`lfets`, `lfe`) MUST update simultaneously. Shared fixtures under `protocol-fixtures/golden/` MUST be regenerated for the new major version.

---

## 3. Minor Version Bump Rules

A minor version bump (e.g., 1.0 → 1.1) is REQUIRED when any of the following occur:

- Adding new optional methods.
- Adding new optional fields to existing request or result shapes.
- Introducing new capabilities (e.g., `phase2.tool-invocation`).
- Adding new notification types.
- Extending error codes with new `lfeCode` values (without changing existing codes).

**Examples of minor changes:**
- Adding an optional `metadata` field to `lfe.session.start` result.
- Introducing a new capability `phase1.run-metadata`.
- Adding a new notification `lfe.run.log` for debug output.

**Consequence:** Existing clients that do not request the new capabilities remain compatible. The server MUST still accept all prior capabilities.

---

## 4. Patch Version Bump Rules

A patch version bump (e.g., 1.0.0 → 1.0.1) is used for:

- Documentation clarifications.
- Error message wording improvements.
- Typo fixes in specification text.
- Non-structural comments or examples.

**Examples of patch changes:**
- Clarifying that `reason` in `lfe.run.cancel` is optional.
- Improving the wording of `LFE_RUN_FAILED` description.
- Adding an example JSON snippet to the spec appendix.

**Consequence:** No implementation changes required. Patch bumps do not affect conformance fixtures.

---

## 5. Version Negotiation

Version negotiation is performed once per stdio connection via `lfe.initialize`:

1. Client sends `params.protocolVersion` (e.g., `"1.0.0"`).
2. Server responds with `result.protocolVersion` indicating the version it will use.
3. If the server rejects the requested version, it returns error `-32001` / `LFE_VERSION_MISMATCH`.

**Rule:** Both client and server MUST agree on a single protocol version for the lifetime of the connection. There is no mid-session version upgrade or downgrade.

---

## 6. Peer Implementation Rule

`lfets` (TypeScript/Bun) and `lfe` (.NET Core) are peer implementations of the same protocol. Neither is a "reference" or "source of truth." Both MUST:

- Advertise the same `protocolVersion` for a given release.
- Accept the same set of capabilities for that version.
- Pass identical shared fixtures and golden transcripts under `protocol-fixtures/golden/`.

Cross-implementation conformance is defined by fixture parity, not by code-level equivalence.

---

## 7. Release Gate

A protocol release is valid only when:

- Both `lfets` and `lfe` pass the same golden transcripts.
- The advertised `protocolVersion` matches across both implementations.
- No structural changes exist outside the rules defined above.

See `docs/architecture/lfe-compatibility-policy.md` for the full conformance policy.

---

**End of document.**
