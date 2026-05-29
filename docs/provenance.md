# Provenance

This workspace was bootstrapped from the local OMO plugin source as the first
milestone for a standalone ULW extraction.

## Source

- Path: local OMO plugin source outside this repository
- Branch: `feat/gorkphus`
- Commit: `aa5f38da22b8e4203898f6be0a8733f16cddb5af`

## Target

- Path: this repository
- Current state: `lfe/` as sole core tree, TypeScript runtime surfaces removed

## Evolution

1. **Initial import**: TypeScript packages under `packages/*` with `omots/` toolkit
2. **Conversion**: All 27 TypeScript packages converted to .NET under `lfe/src/`
3. **Consolidation**: TypeScript runtime surfaces (`omots/`, `packages/`, `hosts/`) removed in one-shot cutover
4. **Codex integration**: `Lfe.CodexMcpBridge` delivered as MCP-compatible tool server

## Current Layout

- `lfe/` — sole core tree (59 .NET projects, 27 packages)
- `docs/` — architecture and protocol documentation
- `protocol-fixtures/` — golden transcripts and conformance fixtures

## Explicitly Excluded

- OpenCode plugin adapter runtime from `src/`
- Generated `dist/`
- `node_modules/`
- Platform binary packages
- Marketing web app
- Source runtime state from `.omo/`
