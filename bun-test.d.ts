declare module "bun:test" {
  interface Matchers {
    toStartWith(expected: string): void
  }
}
