using System.Text.Json;
using System.Text.Json.Serialization;

namespace Omodot.CodexMcpBridge;

public static class CodexPluginAssets
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static string CreateMarketplaceJson(
        string marketplaceName = "omodot-codex-plugins",
        string pluginName = "omodot")
    {
        ValidatePathSegment(marketplaceName, nameof(marketplaceName));
        ValidatePathSegment(pluginName, nameof(pluginName));

        var manifest = new
        {
            name = marketplaceName,
            @interface = new { displayName = "Omodot Codex Plugins" },
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
        string pluginName = "omodot",
        string version = "0.1.0",
        string mcpServersPath = "./.mcp.json")
    {
        ValidatePathSegment(pluginName, nameof(pluginName));
        ValidatePathSegment(version, nameof(version));

        var manifest = new
        {
            name = pluginName,
            version,
            description = "Omodot Codex MCP bridge plugin.",
            license = "MIT",
            keywords = new[] { "codex", "codex-plugin", "omodot", "mcp" },
            mcpServers = mcpServersPath,
            @interface = new
            {
                displayName = "Omodot",
                shortDescription = "Omodot Codex MCP bridge",
                longDescription = "Omodot exposes Codex adapter capabilities through MCP tools backed by the .NET omodot implementation.",
                developerName = "Omodot",
                category = "Developer Tools",
                capabilities = new[] { "MCP Tools", "Workflow", "Codex Adapter" },
                defaultPrompt = new[]
                {
                    "Use omodot codex_dispatch for Codex-backed ULW work.",
                    "Use omodot codex_read_status to inspect a session.",
                    "Use omodot codex_read_messages to read session output.",
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
        args ??= ["Omodot.CodexMcpBridge.dll", "mcp"];
        var manifest = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["omodot-codex"] = new { command, args, cwd },
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
