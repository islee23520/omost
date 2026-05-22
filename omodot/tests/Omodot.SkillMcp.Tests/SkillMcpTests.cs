using System.Text.Json;

namespace Omodot.SkillMcp.Tests;

public sealed class SkillMcpTests
{
    [Fact]
    public void ExportsStableConstants()
    {
        Assert.Equal("skill_mcp", SkillMcpConstants.SkillMcpToolName);
        Assert.Contains("skill-embedded MCPs", SkillMcpConstants.SkillMcpDescription);
        Assert.Contains("context7_query-docs", SkillMcpConstants.BuiltinMcpToolHints["context7"]);
    }

    [Fact]
    public void ParsesArgumentsFromObjectAndJsonString()
    {
        Assert.Empty(SkillMcpArgumentParser.ParseSkillMcpArguments(null));
        Assert.Equal(new Dictionary<string, object?> { ["key"] = "value" }, SkillMcpArgumentParser.ParseSkillMcpArguments(new Dictionary<string, object?> { ["key"] = "value" }));
        Assert.Equal(new Dictionary<string, object?> { ["key"] = "value" }, SkillMcpArgumentParser.ParseSkillMcpArguments("{\"key\":\"value\"}"));
        Assert.Equal(new Dictionary<string, object?> { ["key"] = "value" }, SkillMcpArgumentParser.ParseSkillMcpArguments("'{\"key\":\"value\"}'"));

        var arrayError = Assert.Throws<InvalidOperationException>(() => SkillMcpArgumentParser.ParseSkillMcpArguments("[]"));
        Assert.Contains("Arguments must be a JSON object", arrayError.Message);

        var invalidError = Assert.Throws<InvalidOperationException>(() => SkillMcpArgumentParser.ParseSkillMcpArguments("not json"));
        Assert.Contains("Invalid arguments JSON", invalidError.Message);
    }

    [Fact]
    public async Task ParsesMcpConfigFromFrontmatterAndMcpJson()
    {
        var frontmatter = SkillMcpConfigLoader.ParseSkillMcpConfigFromFrontmatter("---\nmcp:\n  playwright:\n    command: npx\n---\nbody");
        Assert.NotNull(frontmatter);
        Assert.True(frontmatter!.Servers.ContainsKey("playwright"));
        Assert.Equal("npx", frontmatter.Servers["playwright"].Command);
        Assert.Null(SkillMcpConfigLoader.ParseSkillMcpConfigFromFrontmatter("body only"));

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "mcp.json"), JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["mcpServers"] = new Dictionary<string, object?>
                {
                    ["playwright"] = new Dictionary<string, object?>
                    {
                        ["command"] = "npx",
                        ["args"] = new[] { "@playwright/mcp@latest" },
                    },
                },
            }));

            var mcpJson = await SkillMcpConfigLoader.LoadMcpJsonFromDir(root);
            Assert.NotNull(mcpJson);
            Assert.True(mcpJson!.Servers.ContainsKey("playwright"));
            Assert.Equal("npx", mcpJson.Servers["playwright"].Command);
            Assert.Equal(["@playwright/mcp@latest"], mcpJson.Servers["playwright"].Args);

            await File.WriteAllTextAsync(Path.Combine(root, "mcp.json"), JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["playwright"] = new Dictionary<string, object?>
                {
                    ["command"] = "npx",
                },
            }));

            mcpJson = await SkillMcpConfigLoader.LoadMcpJsonFromDir(root);
            Assert.NotNull(mcpJson);
            Assert.True(mcpJson!.Servers.ContainsKey("playwright"));
            Assert.Equal("npx", mcpJson.Servers["playwright"].Command);

            await File.WriteAllTextAsync(Path.Combine(root, "mcp.json"), JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["note"] = "no mcp servers here",
            }));

            Assert.Null(await SkillMcpConfigLoader.LoadMcpJsonFromDir(root));

            await File.WriteAllTextAsync(Path.Combine(root, "mcp.json"), "not json");
            Assert.Null(await SkillMcpConfigLoader.LoadMcpJsonFromDir(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(missingRoot);
        try
        {
            Assert.Null(await SkillMcpConfigLoader.LoadMcpJsonFromDir(missingRoot));
        }
        finally
        {
            Directory.Delete(missingRoot, recursive: true);
        }
    }

    [Fact]
    public void ValidatesOperationsAndFindsMcpServers()
    {
        Assert.Equal(new SkillMcpOperation("tool", "snapshot"), SkillMcpHelpers.ValidateSkillMcpOperation(new SkillMcpArgs("playwright", ToolName: "snapshot")));
        Assert.Equal(new SkillMcpOperation("resource", "memory://a"), SkillMcpHelpers.ValidateSkillMcpOperation(new SkillMcpArgs("playwright", ResourceName: "memory://a")));
        Assert.Equal(new SkillMcpOperation("prompt", "summarize"), SkillMcpHelpers.ValidateSkillMcpOperation(new SkillMcpArgs("playwright", PromptName: "summarize")));

        var missingError = Assert.Throws<InvalidOperationException>(() => SkillMcpHelpers.ValidateSkillMcpOperation(new SkillMcpArgs("playwright")));
        Assert.Contains("Missing operation", missingError.Message);

        var multipleError = Assert.Throws<InvalidOperationException>(() => SkillMcpHelpers.ValidateSkillMcpOperation(new SkillMcpArgs("playwright", ToolName: "a", PromptName: "b")));
        Assert.Contains("Multiple operations specified", multipleError.Message);

        var skills = new[]
        {
            new SkillMcpSkillLike("playwright", new SkillMcpConfig(new Dictionary<string, SkillMcpServerConfig>
            {
                ["playwright"] = new SkillMcpServerConfig("npx", ["@playwright/mcp@latest"]),
            })),
            new SkillMcpSkillLike("plain"),
        };

        var match = SkillMcpHelpers.FindSkillMcpServer("playwright", skills);
        Assert.NotNull(match);
        Assert.Equal("playwright", match!.Skill.Name);
        Assert.Equal("npx", match.Config.Command);
        Assert.Equal(["@playwright/mcp@latest"], match.Config.Args);
        Assert.Null(SkillMcpHelpers.FindSkillMcpServer("missing", skills));
    }

    [Fact]
    public void FormatsHintsAndGrepFiltering()
    {
        var skills = new[]
        {
            new SkillMcpSkillLike("playwright", new SkillMcpConfig(new Dictionary<string, SkillMcpServerConfig>
            {
                ["playwright"] = new SkillMcpServerConfig("npx"),
            })),
            new SkillMcpSkillLike("context7-skill", new SkillMcpConfig(new Dictionary<string, SkillMcpServerConfig>
            {
                ["context7helper"] = new SkillMcpServerConfig("node"),
            })),
        };

        Assert.Contains("\"playwright\" from skill \"playwright\"", SkillMcpHelpers.FormatAvailableSkillMcps(skills));
        Assert.Equal("  (none found)", SkillMcpHelpers.FormatAvailableSkillMcps(Array.Empty<SkillMcpSkillLike>()));
        Assert.Contains("context7_query-docs", SkillMcpHelpers.FormatBuiltinMcpHint("context7"));
        Assert.Null(SkillMcpHelpers.FormatBuiltinMcpHint("missing"));
        Assert.Equal("two", SkillMcpHelpers.ApplyGrepFilter("one\ntwo\nthree", "tw"));
        Assert.Contains("No lines matched", SkillMcpHelpers.ApplyGrepFilter("one\ntwo", "nomatch"));
        Assert.Equal("one\ntwo", SkillMcpHelpers.ApplyGrepFilter("one\ntwo", "["));
    }
}
