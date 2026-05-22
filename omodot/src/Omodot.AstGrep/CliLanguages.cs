namespace Omodot.AstGrep;

public enum CliLanguage
{
    Bash,
    C,
    Cpp,
    Csharp,
    Css,
    Elixir,
    Go,
    Haskell,
    Html,
    Java,
    Javascript,
    Json,
    Kotlin,
    Lua,
    Nix,
    Php,
    Python,
    Ruby,
    Rust,
    Scala,
    Solidity,
    Swift,
    Typescript,
    Tsx,
    Yaml,
}

public static class CliLanguages
{
    public static IReadOnlyList<string> All { get; } =
    [
        "bash",
        "c",
        "cpp",
        "csharp",
        "css",
        "elixir",
        "go",
        "haskell",
        "html",
        "java",
        "javascript",
        "json",
        "kotlin",
        "lua",
        "nix",
        "php",
        "python",
        "ruby",
        "rust",
        "scala",
        "solidity",
        "swift",
        "typescript",
        "tsx",
        "yaml",
    ];

    public const int DefaultTimeoutMs = 300_000;
    public const int DefaultMaxOutputBytes = 1 * 1024 * 1024;
    public const int DefaultMaxMatches = 500;

    public static string ToCliName(this CliLanguage language)
    {
        return language switch
        {
            CliLanguage.Bash => "bash",
            CliLanguage.C => "c",
            CliLanguage.Cpp => "cpp",
            CliLanguage.Csharp => "csharp",
            CliLanguage.Css => "css",
            CliLanguage.Elixir => "elixir",
            CliLanguage.Go => "go",
            CliLanguage.Haskell => "haskell",
            CliLanguage.Html => "html",
            CliLanguage.Java => "java",
            CliLanguage.Javascript => "javascript",
            CliLanguage.Json => "json",
            CliLanguage.Kotlin => "kotlin",
            CliLanguage.Lua => "lua",
            CliLanguage.Nix => "nix",
            CliLanguage.Php => "php",
            CliLanguage.Python => "python",
            CliLanguage.Ruby => "ruby",
            CliLanguage.Rust => "rust",
            CliLanguage.Scala => "scala",
            CliLanguage.Solidity => "solidity",
            CliLanguage.Swift => "swift",
            CliLanguage.Typescript => "typescript",
            CliLanguage.Tsx => "tsx",
            CliLanguage.Yaml => "yaml",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
    }
}
