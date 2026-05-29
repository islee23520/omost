using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lfe.CodexMcpBridge;

public static class CodexPluginAssets
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static string CreateMarketplaceJson(
        string marketplaceName = "lfe-codex-plugins",
        string pluginName = "lfe")
    {
        ValidatePathSegment(marketplaceName, nameof(marketplaceName));
        ValidatePathSegment(pluginName, nameof(pluginName));

        var manifest = new
        {
            name = marketplaceName,
            @interface = new { displayName = "Lfe Codex Plugins" },
            plugins = new[]
            {
                new
                {
                    name = pluginName,
                    source = $"./plugins/{pluginName}",
                    category = "Developer Tools",
                    policy = new { installation = "AVAILABLE", authentication = "ON_INSTALL" },
                },
            },
        };

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    public static string CreatePluginJson(
        string pluginName = "lfe",
        string version = "0.1.0",
        string mcpServersPath = "./.mcp.json")
    {
        ValidatePathSegment(pluginName, nameof(pluginName));
        ValidatePathSegment(version, nameof(version));

        var manifest = new
        {
            name = pluginName,
            version,
            description = "Lfe Codex MCP bridge plugin.",
            license = "MIT",
            keywords = new[] { "codex", "codex-plugin", "lfe", "mcp" },
            mcpServers = mcpServersPath,
            @interface = new
            {
                displayName = "Lfe",
                shortDescription = "Lfe Codex MCP bridge",
                longDescription = "Lfe exposes Codex adapter capabilities through MCP tools backed by the .NET lfe implementation.",
                developerName = "Lfe",
                category = "Developer Tools",
                capabilities = new[] { "MCP Tools", "Workflow", "Codex Adapter" },
                defaultPrompt = new[]
                {
                    "Use lfe codex_dispatch for Codex-backed ULW work.",
                    "Use lfe codex_read_status to inspect a session.",
                    "Use lfe codex_read_messages to read session output.",
                },
                brandColor = "#7C3AED",
                screenshots = Array.Empty<string>(),
            },
        };

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    public static string CreateMcpJson(
        string command = "dotnet",
        IReadOnlyList<string>? args = null,
        string cwd = ".")
    {
        args ??= ["Lfe.CodexMcpBridge.dll", "mcp"];
        var manifest = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["lfe-codex"] = new { command, args, cwd },
            },
        };

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    private static void ValidatePathSegment(string value, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value is "." or ".." || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '_' or '+' or '-')))
        {
            throw new ArgumentException($"{label} contains unsupported characters: {value}", label);
        }
    }
}
