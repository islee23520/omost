import { lstatSync, readdirSync, readFileSync, realpathSync } from "node:fs"
import { join, relative, sep } from "node:path"

const root = process.cwd()
const allowedSegments = [
  ["packages", "adapter-opencode"],
  ["packages", "runtime-opencode"],
]

const scanRoots = [join(root, "packages")]
const ignored = new Set(["node_modules", "dist"])
const violations: string[] = []
const visited = new Set<string>()
const opencodePackageScope = ["@", "opencode-ai"].join("")
const forbiddenImports = [`${opencodePackageScope}/plugin`, `${opencodePackageScope}/sdk`]
const scanRootRealpaths = scanRoots.map((scanRoot) => realpathSync(scanRoot))

function isInsideScanRoot(path: string): boolean {
  const realpath = realpathSync(path)
  return scanRootRealpaths.some((scanRoot) => realpath === scanRoot || realpath.startsWith(`${scanRoot}${sep}`))
}

function isAllowed(path: string): boolean {
  const parts = relative(root, path).split(sep)
  return allowedSegments.some((segments) => segments.every((segment, index) => parts[index] === segment))
}

function scan(dir: string): void {
  if (!isInsideScanRoot(dir)) return

  const dirRealpath = realpathSync(dir)
  if (visited.has(dirRealpath)) return
  visited.add(dirRealpath)

  for (const entry of readdirSync(dir)) {
    if (ignored.has(entry)) continue

    const path = join(dir, entry)
    const stat = lstatSync(path)

    if (stat.isSymbolicLink()) continue

    if (stat.isDirectory()) {
      scan(path)
      continue
    }

    if (!/\.(ts|tsx|js|jsx|mjs|cjs)$/.test(entry)) continue
    if (isAllowed(path)) continue
    if (!isInsideScanRoot(path)) continue

    const content = readFileSync(path, "utf8")
    if (forbiddenImports.some((importName) => content.includes(importName))) {
      violations.push(relative(root, path))
    }
  }
}

for (const scanRoot of scanRoots) scan(scanRoot)

if (violations.length > 0) {
  console.error("OpenCode imports are only allowed in adapter/runtime packages:")
  for (const violation of violations) console.error(`- ${violation}`)
  process.exit(1)
}

console.log("OpenCode boundary check passed")
