namespace Omodot.AstGrepMcp;

public static class ToolDescriptions
{
    public const string SearchDescription = """
Search code by AST structure (25 languages). This is NOT regex.

Meta-variables (the only wildcards ast-grep understands):
  $VAR       - one AST node (an identifier, expression, statement, ...)
  $$$        - zero or more nodes (argument lists, function bodies, ...)

Patterns must be complete, parseable source code. Each meta-variable replaces a whole node, not a substring.

Regex syntax does NOT work - never pass these to pattern.

Examples:
  typescript  "function $NAME($$$) { $$$ }"
  python      "def $FUNC($$$)"
  go          "func $NAME($$$) { $$$ }"
  rust        "fn $NAME($$$) -> $RET { $$$ }"

On empty results the tool returns a hint naming the exact mistake.
""";

    public const string SearchPatternParam = "AST pattern - valid, parseable code using $VAR (one node) and $$$ (many nodes). NOT regex.";

    public const string ReplaceDescription = """
Rewrite code by AST pattern (25 languages). Dry-run by default.
Both pattern and rewrite use AST syntax ($VAR for one node, $$$ for many) - regex does NOT work.
Meta-variables captured in pattern can be reused in rewrite to preserve matched content.
""";
}
