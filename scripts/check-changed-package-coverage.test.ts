import { describe, expect, test } from "bun:test"
import { findUnderCoveredLocalSources, formatCoverageFailure, parseLocalSourceCoverage, selectCoverageSpecs } from "./check-changed-package-coverage"

const lcov = `TN:
SF:src/index.ts
FN:1,run
FNF:1
FNH:1
LF:3
LH:3
end_of_record
TN:
SF:../external.ts
FNF:1
FNH:0
LF:2
LH:1
end_of_record
TN:
SF:src/missed.ts
FNF:2
FNH:1
LF:4
LH:3
end_of_record
`

describe("changed package coverage gate", () => {
  test("parses only package-local source coverage records", () => {
    expect(parseLocalSourceCoverage(lcov)).toEqual([
      { sourceFile: "src/index.ts", linesFound: 3, linesHit: 3, functionsFound: 1, functionsHit: 1 },
      { sourceFile: "src/missed.ts", linesFound: 4, linesHit: 3, functionsFound: 2, functionsHit: 1 },
    ])
  })

  test("finds local files below 100 percent line or function coverage", () => {
    expect(findUnderCoveredLocalSources(lcov)).toEqual([
      { sourceFile: "src/missed.ts", linesFound: 4, linesHit: 3, functionsFound: 2, functionsHit: 1 },
    ])
    expect(formatCoverageFailure("packages/example", findUnderCoveredLocalSources(lcov)[0])).toBe("packages/example/src/missed.ts coverage is not 100%: lines 3/4, functions 1/2")
  })

  test("selects touched package specs and falls back to all for root or script changes", () => {
    const specs = [
      { directory: "packages/a", args: ["test"] },
      { directory: "packages/b", args: ["test"] },
    ]

    expect(selectCoverageSpecs(["packages/a/src/index.ts"], specs)).toEqual([{ directory: "packages/a", args: ["test"] }])
    expect(selectCoverageSpecs(["scripts/check-changed-package-coverage.ts"], specs)).toEqual(specs)
    expect(selectCoverageSpecs(["package.json"], specs)).toEqual(specs)
    expect(selectCoverageSpecs([], specs)).toEqual(specs)
  })
})
