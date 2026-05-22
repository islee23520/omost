using System.Text.Json.Serialization;

namespace Omodot.LspTools;

public static class LspDefaults
{
    public const int DefaultMaxReferences = 200;
    public const int DefaultMaxSymbols = 200;
    public const int DefaultMaxDiagnostics = 200;
    public const int DefaultMaxDirectoryFiles = 50;
    public const int RequestTimeoutMs = 15_000;
    public const int InitTimeoutMs = 60_000;
    public const int IdleTimeoutMs = 5 * 60_000;
    public const int ReaperIntervalMs = 60_000;
    public const int StopHardKillTimeoutMs = 5_000;
    public const int StopSigkillGraceMs = 1_000;
}

public static class ServerLookupStatuses
{
    public const string Found = "found";
    public const string NotConfigured = "not_configured";
    public const string NotInstalled = "not_installed";
}

public static class SeverityFilters
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Information = "information";
    public const string Hint = "hint";
    public const string All = "all";

    public static IReadOnlyList<string> Values { get; } =
        new[] { Error, Warning, Information, Hint, All };
}

public static class LspSymbolScopes
{
    public const string Document = "document";
    public const string Workspace = "workspace";

    public static IReadOnlyList<string> Values { get; } =
        new[] { Document, Workspace };
}

public static class LspDiagnosticModes
{
    public const string File = "file";
    public const string Directory = "directory";
}

public static class LspToolErrorKinds
{
    public const string MissingDependency = "missing_dependency";
    public const string NoFiles = "no_files";
    public const string InvalidPath = "invalid_path";
    public const string MissingQuery = "missing_query";
}

public enum SymbolKind
{
    File = 1,
    Module = 2,
    Namespace = 3,
    Package = 4,
    Class = 5,
    Method = 6,
    Property = 7,
    Field = 8,
    Constructor = 9,
    Enum = 10,
    Interface = 11,
    Function = 12,
    Variable = 13,
    Constant = 14,
    String = 15,
    Number = 16,
    Boolean = 17,
    Array = 18,
    Object = 19,
    Key = 20,
    Null = 21,
    EnumMember = 22,
    Struct = 23,
    Event = 24,
    Operator = 25,
    TypeParameter = 26,
}

public enum DiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4,
}

public static class LspProtocolMaps
{
    public static IReadOnlyDictionary<int, string> SymbolKindMap { get; } = new Dictionary<int, string>
    {
        [1] = "File",
        [2] = "Module",
        [3] = "Namespace",
        [4] = "Package",
        [5] = "Class",
        [6] = "Method",
        [7] = "Property",
        [8] = "Field",
        [9] = "Constructor",
        [10] = "Enum",
        [11] = "Interface",
        [12] = "Function",
        [13] = "Variable",
        [14] = "Constant",
        [15] = "String",
        [16] = "Number",
        [17] = "Boolean",
        [18] = "Array",
        [19] = "Object",
        [20] = "Key",
        [21] = "Null",
        [22] = "EnumMember",
        [23] = "Struct",
        [24] = "Event",
        [25] = "Operator",
        [26] = "TypeParameter",
    };

    public static IReadOnlyDictionary<int, string> SeverityMap { get; } = new Dictionary<int, string>
    {
        [1] = SeverityFilters.Error,
        [2] = SeverityFilters.Warning,
        [3] = SeverityFilters.Information,
        [4] = SeverityFilters.Hint,
    };

    public static IReadOnlyDictionary<string, string> ExtensionToLanguageIdMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [".abap"] = "abap",
        [".bat"] = "bat",
        [".bib"] = "bibtex",
        [".bibtex"] = "bibtex",
        [".clj"] = "clojure",
        [".cljs"] = "clojure",
        [".cljc"] = "clojure",
        [".edn"] = "clojure",
        [".coffee"] = "coffeescript",
        [".c"] = "c",
        [".cpp"] = "cpp",
        [".cxx"] = "cpp",
        [".cc"] = "cpp",
        [".c++"] = "cpp",
        [".cs"] = "csharp",
        [".css"] = "css",
        [".d"] = "d",
        [".pas"] = "pascal",
        [".pascal"] = "pascal",
        [".diff"] = "diff",
        [".patch"] = "diff",
        [".dart"] = "dart",
        [".dockerfile"] = "dockerfile",
        [".ex"] = "elixir",
        [".exs"] = "elixir",
        [".erl"] = "erlang",
        [".hrl"] = "erlang",
        [".fs"] = "fsharp",
        [".fsi"] = "fsharp",
        [".fsx"] = "fsharp",
        [".fsscript"] = "fsharp",
        [".gitcommit"] = "git-commit",
        [".gitrebase"] = "git-rebase",
        [".go"] = "go",
        [".groovy"] = "groovy",
        [".gleam"] = "gleam",
        [".hbs"] = "handlebars",
        [".handlebars"] = "handlebars",
        [".hs"] = "haskell",
        [".html"] = "html",
        [".htm"] = "html",
        [".ini"] = "ini",
        [".java"] = "java",
        [".js"] = "javascript",
        [".jsx"] = "javascriptreact",
        [".json"] = "json",
        [".jsonc"] = "jsonc",
        [".tex"] = "latex",
        [".latex"] = "latex",
        [".less"] = "less",
        [".lua"] = "lua",
        [".makefile"] = "makefile",
        ["makefile"] = "makefile",
        [".md"] = "markdown",
        [".markdown"] = "markdown",
        [".m"] = "objective-c",
        [".mm"] = "objective-cpp",
        [".pl"] = "perl",
        [".pm"] = "perl",
        [".pm6"] = "perl6",
        [".php"] = "php",
        [".ps1"] = "powershell",
        [".psm1"] = "powershell",
        [".pug"] = "jade",
        [".jade"] = "jade",
        [".py"] = "python",
        [".pyi"] = "python",
        [".r"] = "r",
        [".cshtml"] = "razor",
        [".razor"] = "razor",
        [".rb"] = "ruby",
        [".rake"] = "ruby",
        [".gemspec"] = "ruby",
        [".ru"] = "ruby",
        [".erb"] = "erb",
        [".html.erb"] = "erb",
        [".js.erb"] = "erb",
        [".css.erb"] = "erb",
        [".json.erb"] = "erb",
        [".rs"] = "rust",
        [".scss"] = "scss",
        [".sass"] = "sass",
        [".scala"] = "scala",
        [".shader"] = "shaderlab",
        [".sh"] = "shellscript",
        [".bash"] = "shellscript",
        [".zsh"] = "shellscript",
        [".ksh"] = "shellscript",
        [".sql"] = "sql",
        [".svelte"] = "svelte",
        [".swift"] = "swift",
        [".ts"] = "typescript",
        [".tsx"] = "typescriptreact",
        [".mts"] = "typescript",
        [".cts"] = "typescript",
        [".mtsx"] = "typescriptreact",
        [".ctsx"] = "typescriptreact",
        [".xml"] = "xml",
        [".xsl"] = "xsl",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",
        [".vue"] = "vue",
        [".zig"] = "zig",
        [".zon"] = "zig",
        [".astro"] = "astro",
        [".ml"] = "ocaml",
        [".mli"] = "ocaml",
        [".tf"] = "terraform",
        [".tfvars"] = "terraform-vars",
        [".hcl"] = "hcl",
        [".nix"] = "nix",
        [".typ"] = "typst",
        [".typc"] = "typst",
        [".ets"] = "typescript",
        [".lhs"] = "haskell",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin",
        [".prisma"] = "prisma",
        [".h"] = "c",
        [".hpp"] = "cpp",
        [".hh"] = "cpp",
        [".hxx"] = "cpp",
        [".h++"] = "cpp",
        [".objc"] = "objective-c",
        [".objcpp"] = "objective-cpp",
        [".fish"] = "fish",
        [".graphql"] = "graphql",
        [".gql"] = "graphql",
    };

    public static string GetLanguageId(string extension)
    {
        return ExtensionToLanguageIdMap.TryGetValue(extension, out var languageId)
            ? languageId
            : "plaintext";
    }
}

public sealed record LspServerConfig
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; init; } = Array.Empty<string>();

    [JsonPropertyName("extensions")]
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();

    [JsonPropertyName("disabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Disabled { get; init; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    [JsonPropertyName("initialization")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Initialization { get; init; }
}

public sealed record ResolvedServer
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; init; } = Array.Empty<string>();

    [JsonPropertyName("extensions")]
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    [JsonPropertyName("initialization")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Initialization { get; init; }
}

public sealed record ServerLookupInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; init; } = Array.Empty<string>();

    [JsonPropertyName("extensions")]
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "status")]
[JsonDerivedType(typeof(FoundServerLookupResult), ServerLookupStatuses.Found)]
[JsonDerivedType(typeof(NotConfiguredServerLookupResult), ServerLookupStatuses.NotConfigured)]
[JsonDerivedType(typeof(NotInstalledServerLookupResult), ServerLookupStatuses.NotInstalled)]
public abstract record ServerLookupResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

public sealed record FoundServerLookupResult : ServerLookupResult
{
    public FoundServerLookupResult()
    {
        Status = ServerLookupStatuses.Found;
    }

    [JsonPropertyName("server")]
    public ResolvedServer Server { get; init; } = new();
}

public sealed record NotConfiguredServerLookupResult : ServerLookupResult
{
    public NotConfiguredServerLookupResult()
    {
        Status = ServerLookupStatuses.NotConfigured;
    }

    [JsonPropertyName("extension")]
    public string Extension { get; init; } = string.Empty;

    [JsonPropertyName("availableServers")]
    public IReadOnlyList<string> AvailableServers { get; init; } = Array.Empty<string>();
}

public sealed record NotInstalledServerLookupResult : ServerLookupResult
{
    public NotInstalledServerLookupResult()
    {
        Status = ServerLookupStatuses.NotInstalled;
    }

    [JsonPropertyName("server")]
    public ServerLookupInfo Server { get; init; } = new();

    [JsonPropertyName("installHint")]
    public string InstallHint { get; init; } = string.Empty;
}

public sealed record Position
{
    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("character")]
    public int Character { get; init; }
}

public sealed record Range
{
    [JsonPropertyName("start")]
    public Position Start { get; init; } = new();

    [JsonPropertyName("end")]
    public Position End { get; init; } = new();
}

public sealed record Location
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;

    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();
}

public sealed record LocationLink
{
    [JsonPropertyName("targetUri")]
    public string TargetUri { get; init; } = string.Empty;

    [JsonPropertyName("targetRange")]
    public Range TargetRange { get; init; } = new();

    [JsonPropertyName("targetSelectionRange")]
    public Range TargetSelectionRange { get; init; } = new();

    [JsonPropertyName("originSelectionRange")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Range? OriginSelectionRange { get; init; }
}

public sealed record SymbolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public int Kind { get; init; }

    [JsonPropertyName("location")]
    public Location Location { get; init; } = new();

    [JsonPropertyName("containerName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContainerName { get; init; }
}

public sealed record DocumentSymbol
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public int Kind { get; init; }

    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();

    [JsonPropertyName("selectionRange")]
    public Range SelectionRange { get; init; } = new();

    [JsonPropertyName("children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DocumentSymbol>? Children { get; init; }
}

public sealed record Diagnostic
{
    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();

    [JsonPropertyName("severity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Severity { get; init; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Code { get; init; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

public record TextDocumentIdentifier
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;
}

public sealed record VersionedTextDocumentIdentifier : TextDocumentIdentifier
{
    [JsonPropertyName("version")]
    public int? Version { get; init; }
}

public sealed record TextEdit
{
    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();

    [JsonPropertyName("newText")]
    public string NewText { get; init; } = string.Empty;
}

public sealed record CreateFileOptions
{
    [JsonPropertyName("overwrite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Overwrite { get; init; }

    [JsonPropertyName("ignoreIfExists")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IgnoreIfExists { get; init; }
}

public sealed record DeleteFileOptions
{
    [JsonPropertyName("recursive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Recursive { get; init; }

    [JsonPropertyName("ignoreIfNotExists")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IgnoreIfNotExists { get; init; }
}

public sealed record WorkspaceDocumentChange
{
    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; init; }

    [JsonPropertyName("textDocument")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VersionedTextDocumentIdentifier? TextDocument { get; init; }

    [JsonPropertyName("edits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TextEdit>? Edits { get; init; }

    [JsonPropertyName("uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uri { get; init; }

    [JsonPropertyName("oldUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldUri { get; init; }

    [JsonPropertyName("newUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NewUri { get; init; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Options { get; init; }

    public static WorkspaceDocumentChange ForTextDocumentEdit(VersionedTextDocumentIdentifier document, IReadOnlyList<TextEdit> edits)
    {
        return new WorkspaceDocumentChange { TextDocument = document, Edits = edits };
    }

    public static WorkspaceDocumentChange ForCreateFile(string uri, CreateFileOptions? options = null)
    {
        return new WorkspaceDocumentChange { Kind = "create", Uri = uri, Options = options };
    }

    public static WorkspaceDocumentChange ForRenameFile(string oldUri, string newUri, CreateFileOptions? options = null)
    {
        return new WorkspaceDocumentChange { Kind = "rename", OldUri = oldUri, NewUri = newUri, Options = options };
    }

    public static WorkspaceDocumentChange ForDeleteFile(string uri, DeleteFileOptions? options = null)
    {
        return new WorkspaceDocumentChange { Kind = "delete", Uri = uri, Options = options };
    }
}

public sealed record WorkspaceEdit
{
    [JsonPropertyName("changes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, IReadOnlyList<TextEdit>>? Changes { get; init; }

    [JsonPropertyName("documentChanges")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WorkspaceDocumentChange>? DocumentChanges { get; init; }
}

[JsonPolymorphic]
[JsonDerivedType(typeof(PrepareRenameResult))]
[JsonDerivedType(typeof(PrepareRenameDefaultBehavior))]
[JsonDerivedType(typeof(PrepareRenameRangeOnly))]
public abstract record PrepareRenameValue;

public sealed record PrepareRenameResult : PrepareRenameValue
{
    [JsonPropertyName("range")]
    public Range Range { get; init; } = new();

    [JsonPropertyName("placeholder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Placeholder { get; init; }
}

public sealed record PrepareRenameDefaultBehavior : PrepareRenameValue
{
    [JsonPropertyName("defaultBehavior")]
    public bool DefaultBehavior { get; init; }
}

public sealed record PrepareRenameRangeOnly : PrepareRenameValue
{
    [JsonPropertyName("start")]
    public Position Start { get; init; } = new();

    [JsonPropertyName("end")]
    public Position End { get; init; } = new();
}

public sealed record ApplyResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("filesModified")]
    public IReadOnlyList<string> FilesModified { get; init; } = Array.Empty<string>();

    [JsonPropertyName("totalEdits")]
    public int TotalEdits { get; init; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed record ClientSnapshot
{
    [JsonPropertyName("root")]
    public string Root { get; init; } = string.Empty;

    [JsonPropertyName("serverId")]
    public string ServerId { get; init; } = string.Empty;

    [JsonPropertyName("refCount")]
    public int RefCount { get; init; }

    [JsonPropertyName("pendingWaiters")]
    public int PendingWaiters { get; init; }

    [JsonPropertyName("lastUsedAt")]
    public long LastUsedAt { get; init; }

    [JsonPropertyName("isInitializing")]
    public bool IsInitializing { get; init; }

    [JsonPropertyName("alive")]
    public bool Alive { get; init; }

    [JsonPropertyName("command")]
    public IReadOnlyList<string> Command { get; init; } = Array.Empty<string>();
}

public sealed record ServerStatus
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("installed")]
    public bool Installed { get; init; }

    [JsonPropertyName("extensions")]
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; init; }
}
