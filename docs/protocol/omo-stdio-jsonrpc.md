# OMO Stdio + JSON-RPC 2.0 Protocol Specification

**Status:** Frozen  
**Date:** 2026-05-24  
**Transport:** stdio  
**RPC Envelope:** JSON-RPC 2.0  
**Framing:** Content-Length headers (NOT NDJSON)

---

## 1. Transport

**Canonical transport for Phase 1:** stdio (standard input/output streams).

**Rationale for stdio in Phase 1:**
- Universal availability across all runtimes (Node.js, .NET, Python, Go, Rust).
- No network stack required.
- Simple process lifecycle semantics (parent spawns child, pipes stdin/stdout).
- Matches existing patterns in LSP (Language Server Protocol) and MCP (Model Context Protocol).
- Enables cross-runtime interop between `omots` (TypeScript/Bun) and `lfe` (.NET Core) without HTTP/gRPC complexity.

**Out of scope for Phase 1:**
- HTTP transport (REST, WebSocket, Server-Sent Events).
- gRPC transport.
- Any network-based protocol.

HTTP/gRPC may be considered in Phase 2+ for remote orchestration scenarios, but stdio remains the canonical interop transport for local subprocess communication.

---

## 2. Framing

**Message framing:** Content-Length headers (LSP-style).

**Format:**
```
Content-Length: <byte-length>\r\n
\r\n
<JSON body>
```

**Rules:**
- Every message MUST be preceded by a `Content-Length` header indicating the exact byte length of the UTF-8 encoded JSON body.
- Header terminator is `\r\n\r\n` (CRLF CRLF).
- The body MUST be valid UTF-8 JSON.
- No trailing newline after the JSON body is required or expected.
- Messages are strictly delimited by Content-Length; there is no reliance on newlines within the body.

**Why NOT NDJSON:**
- NDJSON (newline-delimited JSON) is rejected for Phase 1 because:
  - It cannot safely embed newlines within JSON strings without escaping complexity.
  - It lacks explicit length information, making partial reads harder to recover from.
  - Content-Length is the established standard in LSP and provides robust framing for binary-safe transport.
- NDJSON appears in this document only as an explicitly rejected alternative.

**Reference implementation:** `packages/lsp-tools-mcp/src/lsp/json-rpc-connection.ts` demonstrates the Content-Length framing logic (header parsing, buffer management, message dispatch).

---

## 3. JSON-RPC 2.0 Envelope

All messages conform to JSON-RPC 2.0.

### Request
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "omo.initialize",
  "params": { ... }
}
```

### Response (success)
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": { ... }
}
```

### Response (error)
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32600,
    "message": "Invalid Request",
    "data": {
      "omoCode": "OMO_INVALID_REQUEST",
      "retryable": false
    }
  }
}
```

### Notification
```json
{
  "jsonrpc": "2.0",
  "method": "omo.run.progress",
  "params": { ... }
}
```

**Idempotency note:** Request IDs are managed by the client. Servers MUST echo the same `id` in responses. Notifications have no `id` and require no response.

---

## 4. Methods

Exactly four methods are defined for Phase 1. No additional methods may be added without a protocol version bump.

### 4.1 `omo.initialize`

**Purpose:** Version negotiation, capability negotiation, and server identification.

**Request params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `protocolVersion` | string | yes | Client's protocol version (e.g., `"1.0.0"`) |
| `hostName` | string | yes | Host application name (e.g., `"opencode"`) |
| `hostVersion` | string | yes | Host application version |
| `clientKind` | string | yes | Client type: `"host-bridge"` or `"implementation-toolkit"` |
| `requestedCapabilities` | string[] | yes | Capabilities the client wishes to use (see Section 8) |

**Result:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `protocolVersion` | string | yes | Server's negotiated protocol version |
| `implementationName` | string | yes | Implementation name (e.g., `"omots"`, `"lfe"`) |
| `acceptedCapabilities` | string[] | yes | Capabilities the server accepts for this session |
| `serverMode` | string | yes | `"standalone"` or `"bridge"` |

**Error cases:**
- Version mismatch → `-32001` with `omoCode: "OMO_VERSION_MISMATCH"`
- Invalid request shape → `-32600` with `omoCode: "OMO_INVALID_REQUEST"`

---

### 4.2 `omo.session.start`

**Purpose:** Create a new orchestration session.

**Request params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `sessionId` | string | yes | Client-provided session identifier (UUID recommended) |
| `cwd` | string | yes | Working directory for the session |
| `arguments` | string[] | no | Command-line style arguments for session initialization |
| `metadata` | object | no | Arbitrary metadata (host-specific, passed through) |

**Result:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `sessionId` | string | yes | Echo of the requested sessionId |
| `accepted` | boolean | yes | Whether the session was accepted |

**Error cases:**
- Invalid request → `-32600`
- Session creation failure → `-32010` with `omoCode: "OMO_RUN_FAILED"`

---

### 4.3 `omo.run.dispatch`

**Purpose:** Dispatch a prompt/run to an agent within a session.

**Request params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | Client-provided run identifier (UUID recommended) |
| `sessionId` | string | yes | Target session (must have been started via `omo.session.start`) |
| `prompt` | string | yes | The user prompt / instruction |
| `agent` | string | no | Agent name (implementation-specific) |
| `model` | string | no | Model identifier (implementation-specific) |
| `continuationToken` | string | no | Opaque token for multi-turn state preservation |

**Result:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | Echo of the requested runId |
| `accepted` | boolean | yes | Whether the run was accepted for execution |

**Error cases:**
- Unknown session → `-32602`
- Dispatch failure → `-32010` with `omoCode: "OMO_RUN_FAILED"`

---

### 4.4 `omo.run.cancel`

**Purpose:** Request cancellation of a running dispatch.

**Request params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | The run to cancel |
| `reason` | string | no | Human-readable cancellation reason |

**Result:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | Echo of the requested runId |
| `status` | string | yes | Terminal status after cancellation: `"cancelled"` |

**Semantics:**
- Cancellation is best-effort.
- If the run has already reached a terminal state (`completed`, `failed`, `cancelled`), the server SHOULD return the current status.
- The server MUST eventually emit a terminal notification (`omo.run.result` or `omo.run.error`) for every dispatched run.

---

## 5. Notifications

Exactly three notifications are defined for Phase 1. Notifications are one-way server-to-client messages.

### 5.1 `omo.run.progress`

**Purpose:** Stream incremental progress updates for a run.

**Params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | The run this progress applies to |
| `phase` | string | yes | One of: `"queued"`, `"running"`, `"tool"`, `"completed"`, `"failed"`, `"cancelled"` |
| `message` | string | no | Human-readable progress message |
| `completed` | number | no | Items completed (for progress bars) |
| `total` | number | no | Total items (for progress bars) |

**Phase semantics:**
- `queued`: Run accepted but not yet started.
- `running`: Agent is actively processing.
- `tool`: Agent invoked a tool (sub-phase of running).
- `completed` / `failed` / `cancelled`: Terminal phases (server SHOULD also emit `omo.run.result` or `omo.run.error`).

---

### 5.2 `omo.run.result`

**Purpose:** Deliver the final successful or terminal result of a run.

**Params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | The completed run |
| `status` | string | yes | One of: `"completed"`, `"failed"`, `"cancelled"` |
| `outputText` | string | no | Final text output from the agent |
| `outputJson` | object | no | Structured JSON output (if any) |
| `finalSessionId` | string | yes | Session ID after run completion (may be unchanged or compacted) |

**Guarantee:** Every `omo.run.dispatch` that is accepted MUST eventually produce exactly one of:
- `omo.run.result` (terminal), or
- `omo.run.error` (terminal error).

---

### 5.3 `omo.run.error`

**Purpose:** Report a terminal error for a run.

**Params:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `runId` | string | yes | The failed run |
| `code` | number | yes | Numeric JSON-RPC error code |
| `message` | string | yes | Human-readable error message |
| `retryable` | boolean | yes | Whether the client may retry this run |

**Relationship to error envelope:** This notification carries the same shape as `error.data` in the JSON-RPC error envelope (see Section 6).

---

## 6. Error Envelope

Errors follow JSON-RPC 2.0 with OMO-specific extensions.

**Shape:**
```json
{
  "code": <number>,
  "message": "<string>",
  "data": {
    "omoCode": "<string>",
    "retryable": <boolean>
  }
}
```

**Numeric codes (in `error.code`):**
| Code | Name | Description |
|------|------|-------------|
| `-32700` | Parse error | Invalid JSON |
| `-32600` | Invalid Request | Malformed request object |
| `-32601` | Method not found | Unknown method |
| `-32602` | Invalid params | Parameter validation failure |
| `-32603` | Internal error | Unhandled server error |
| `-32001` | Version mismatch | `omo.initialize` protocolVersion rejected |
| `-32010` | Run failure | Dispatch or execution failure |

**String OMO codes (in `error.data.omoCode`):**
| Code | Description |
|------|-------------|
| `OMO_VERSION_MISMATCH` | Protocol version negotiation failed |
| `OMO_INVALID_REQUEST` | Request shape or required fields missing |
| `OMO_RUN_FAILED` | Run dispatch or execution failed |

**Convention:** Numeric codes live in `error.code`. String OMO codes live in `error.data.omoCode`. Both SHOULD be populated for OMO-specific errors.

---

## 7. Version Negotiation

Version negotiation occurs exclusively via `omo.initialize`.

**Flow:**
1. Client sends `omo.initialize` with `params.protocolVersion`.
2. Server responds with `result.protocolVersion` indicating the version it will use for this session.
3. If the server cannot support the requested version, it returns error `-32001` / `OMO_VERSION_MISMATCH`.

**Rule:** Both client and server MUST agree on a single protocol version for the lifetime of the stdio connection. There is no mid-session version upgrade.

See also: `docs/protocol/omo-protocol-versioning.md` (separate document).

---

## 8. Capability Negotiation

Capabilities are exchanged during `omo.initialize`.

**Client advertises intent:**
```json
"requestedCapabilities": ["phase1.initialize", "phase1.session-start", "phase1.run-dispatch", ...]
```

**Server responds with acceptance:**
```json
"acceptedCapabilities": ["phase1.initialize", "phase1.session-start", "phase1.run-dispatch", ...]
```

**Frozen Phase 1 capabilities:**
- `phase1.initialize`
- `phase1.session-start`
- `phase1.run-dispatch`
- `phase1.run-progress`
- `phase1.run-result`
- `phase1.run-cancel`

A server that does not accept a requested capability MUST NOT advertise it in `acceptedCapabilities`. A client MUST NOT send requests for capabilities not accepted by the server.

---

## 9. Cancellation

Cancellation is requested via `omo.run.cancel`.

**Semantics:**
- Cancellation is advisory; the server makes a best-effort attempt to stop the run.
- A run that is cancelled MUST eventually reach terminal state `"cancelled"`.
- If the run has already completed or failed before cancellation is processed, the server returns the actual terminal status.
- Every dispatched run MUST produce a terminal notification (`omo.run.result` or `omo.run.error`), even if cancelled.

**Terminal states:** `completed`, `failed`, `cancelled`.

---

## 10. Required Fixture Paths

The following fixture paths are required for conformance testing (all fixtures live under `protocol-fixtures/`):

- `protocol-fixtures/phase1/initialize.request.json`
- `protocol-fixtures/phase1/initialize.response.json`
- `protocol-fixtures/phase1/session.start.request.json`
- `protocol-fixtures/phase1/session.start.response.json`
- `protocol-fixtures/phase1/run.dispatch.request.json`
- `protocol-fixtures/phase1/run.dispatch.response.json`
- `protocol-fixtures/phase1/run.progress.notification.json`
- `protocol-fixtures/phase1/run.result.notification.json`
- `protocol-fixtures/phase1/run.cancel.request.json`
- `protocol-fixtures/phase1/run.cancel.response.json`
- `protocol-fixtures/phase1/error.version-mismatch.response.json`
- `protocol-fixtures/phase1/error.invalid-request.response.json`
- `protocol-fixtures/golden/omots-phase1-success.json`
- `protocol-fixtures/golden/lfe-phase1-success.json`
- `protocol-fixtures/golden/phase1-cancel.json`

All implementations (`omots`, `lfe`) MUST pass the same fixture suite for a given protocol version.

---

## Appendix A: Example Session

```
Client → Server (initialize)
Content-Length: 142

{"jsonrpc":"2.0","id":1,"method":"omo.initialize","params":{"protocolVersion":"1.0.0","hostName":"opencode","hostVersion":"0.1.0","clientKind":"host-bridge","requestedCapabilities":["phase1.initialize","phase1.session-start"]}}

Server → Client (initialize result)
Content-Length: 168

{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"1.0.0","implementationName":"omots","acceptedCapabilities":["phase1.initialize","phase1.session-start"],"serverMode":"standalone"}}
```

---

**End of specification.**
