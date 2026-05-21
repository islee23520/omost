# Provenance

This workspace was bootstrapped from the local OMO plugin source as the first
milestone for a standalone ULW extraction.

## Source

- Path: local OMO plugin source outside this repository
- Branch: `feat/gorkphus`
- Commit: `aa5f38da22b8e4203898f6be0a8733f16cddb5af`

## Target

- Path: this repository
- Initial state: empty git repository with `.git/` and `.omo/` runtime state only

## Imported Foundation

- Root package and TypeScript test scaffolding
- `bun.lock`
- `bunfig.toml`
- Existing pure core packages:
  - `packages/utils`
  - `packages/model-core`
  - `packages/rules-engine`
  - `packages/agents-md-core`
  - `packages/ast-grep-core`
  - `packages/comment-checker-core`
  - `packages/boulder-state`

## Explicitly Excluded

- OpenCode plugin adapter runtime from `src/`
- Generated `dist/`
- `node_modules/`
- platform binary packages
- marketing web app
- source runtime state from `.omo/`
