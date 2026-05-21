export type CoverageSpec = {
  directory: string
  args: string[]
}

export type LocalCoverage = {
  sourceFile: string
  linesFound: number
  linesHit: number
  functionsFound: number
  functionsHit: number
}

export const coverageSpecs: CoverageSpec[] = [
  { directory: "packages/hooks-core", args: ["test", "./src/index.test.ts", "--coverage", "--coverage-reporter=lcov"] },
  { directory: "packages/skills-core", args: ["test", "./src/skills.test.ts", "./src/skills/team-mode.test.ts", "--coverage", "--coverage-reporter=lcov"] },
  { directory: "packages/adapter-codex", args: ["test", "./src/index.test.ts", "--coverage", "--coverage-reporter=lcov"] },
  { directory: "packages/adapter-opencode", args: ["test", "./src/index.test.ts", "--coverage", "--coverage-reporter=lcov"] },
  { directory: "packages/standalone-runtime", args: ["test", "./src/index.test.ts", "--coverage", "--coverage-reporter=lcov"] },
]

export function parseLocalSourceCoverage(lcov: string): LocalCoverage[] {
  return lcov.split("end_of_record").flatMap((section) => {
    const sourceFile = findValue(section, "SF")
    if (!sourceFile?.startsWith("src/")) return []
    return [{
      sourceFile,
      linesFound: Number(findValue(section, "LF") ?? 0),
      linesHit: Number(findValue(section, "LH") ?? 0),
      functionsFound: Number(findValue(section, "FNF") ?? 0),
      functionsHit: Number(findValue(section, "FNH") ?? 0),
    }]
  })
}

export function findUnderCoveredLocalSources(lcov: string): LocalCoverage[] {
  return parseLocalSourceCoverage(lcov).filter((coverage) => coverage.linesFound !== coverage.linesHit || coverage.functionsFound !== coverage.functionsHit)
}

export function selectCoverageSpecs(changedPaths: string[], specs: readonly CoverageSpec[] = coverageSpecs): CoverageSpec[] {
  if (changedPaths.length === 0) return [...specs]
  if (changedPaths.some((path) => path === "package.json" || path === "bun.lock" || path.startsWith("scripts/"))) return [...specs]
  const selected = specs.filter((spec) => changedPaths.some((path) => path === spec.directory || path.startsWith(`${spec.directory}/`)))
  return selected.length > 0 ? selected : [...specs]
}

export function formatCoverageFailure(directory: string, file: LocalCoverage): string {
  return `${directory}/${file.sourceFile} coverage is not 100%: lines ${file.linesHit}/${file.linesFound}, functions ${file.functionsHit}/${file.functionsFound}`
}

export function getChangedPaths(): string[] {
  const tracked = Bun.spawnSync(["git", "diff", "--name-only", "--diff-filter=ACMRT", "HEAD"], { stdout: "pipe", stderr: "pipe" })
  const untracked = Bun.spawnSync(["git", "ls-files", "--others", "--exclude-standard"], { stdout: "pipe", stderr: "pipe" })
  if (tracked.exitCode !== 0 || untracked.exitCode !== 0) return []
  return [...decodeLines(tracked.stdout), ...decodeLines(untracked.stdout)]
}

export async function runCoverageGate(specs: readonly CoverageSpec[] = selectCoverageSpecs(getChangedPaths())): Promise<number> {
  for (const spec of specs) {
    const result = Bun.spawnSync(["bun", ...spec.args], { cwd: spec.directory, stdout: "inherit", stderr: "inherit" })
    if (result.exitCode !== 0) return result.exitCode

    const lcov = await Bun.file(`${spec.directory}/coverage/lcov.info`).text()
    const failedFiles = findUnderCoveredLocalSources(lcov)
    if (failedFiles.length > 0) {
      for (const file of failedFiles) console.error(formatCoverageFailure(spec.directory, file))
      return 1
    }
  }
  return 0
}

function findValue(section: string, key: string): string | undefined {
  const prefix = `${key}:`
  return section.split("\n").find((line) => line.startsWith(prefix))?.slice(prefix.length)
}

function decodeLines(output: Uint8Array): string[] {
  return new TextDecoder().decode(output).split("\n").filter(Boolean)
}

if (import.meta.main) {
  process.exit(await runCoverageGate())
}
