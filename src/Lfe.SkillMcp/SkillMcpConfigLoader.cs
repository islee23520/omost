using System.Text.Json;
using System.Text.Json.Nodes;
using Lfe.Utils;

namespace Lfe.SkillMcp;

public static class SkillMcpConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
    };

    public static SkillMcpConfig? ParseSkillMcpConfigFromFrontmatter(string content)
    {
        var result = Frontmatter.Parse<JsonObject>(content);
        if (result.Data is not JsonObject data)
        {
            return null;
        }

        if (!data.TryGetPropertyValue("mcp", out var mcpNode) || mcpNode is not JsonObject mcpObject)
        {
            return null;
        }

        return DeserializeConfig(mcpObject);
    }

    public static async Task<SkillMcpConfig?> LoadMcpJsonFromDir(string skillDir)
    {
        var mcpJsonPath = Path.Combine(skillDir, "mcp.json");

        try
        {
            var content = await File.ReadAllTextAsync(mcpJsonPath).ConfigureAwait(false);
            var parsed = JsonNode.Parse(content);

            if (parsed is not JsonObject root)
            {
                return null;
            }

            if (root.TryGetPropertyValue("mcpServers", out var mcpServersNode) && mcpServersNode is JsonObject mcpServersObject)
            {
                return DeserializeConfig(mcpServersObject);
            }

            var hasCommandField = root.Any(pair => pair.Value is JsonObject obj && obj.ContainsKey("command"));
            if (hasCommandField)
            {
                return DeserializeConfig(root);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static SkillMcpConfig? DeserializeConfig(JsonObject value)
    {
        try
        {
            var servers = value.Deserialize<Dictionary<string, SkillMcpServerConfig>>(JsonOptions);
            return servers is null ? null : new SkillMcpConfig(servers);
        }
        catch
        {
            return null;
        }
    }
}
