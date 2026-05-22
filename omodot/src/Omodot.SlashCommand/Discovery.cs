using System.Text.Json;
using Omodot.Utils;

namespace Omodot.SlashCommand;

public static class Discovery
{
    private static readonly HashSet<string> ExcludedDirs = ["node_modules", ".git", "dist", "build", ".turbo", ".next", "coverage"];

    public static List<SlashCommandInfo> DiscoverSlashCommandsSync(DiscoverSlashCommandsOptions? options = null)
    {
        options ??= new DiscoverSlashCommandsOptions();
        var root = options.Directory ?? Directory.GetCurrentDirectory();
        var projectCommands = DiscoverCommandsFromDir(Path.Join(root, ".claude", "commands"), CommandScope.Project);
        var userCommands = options.IncludeUserCommands
            ? DiscoverCommandsFromDir(Path.Join(options.UserHomeDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "commands"), CommandScope.User)
            : [];
        var extraCommands = (options.ExtraCommandDirs ?? [])
            .SelectMany(d => DiscoverCommandsFromDir(d, CommandScope.Extra))
            .ToList();
        return DeduplicateByName([.. projectCommands, .. userCommands, .. extraCommands]);
    }

    public static HookSlashCommandInfo ToHookSlashCommandInfo(SlashCommandInfo command) => new(
        command.Name,
        command.Scope.ToString().ToLowerInvariant(),
        command.Content,
        command.Metadata.Description,
        command.Metadata.Model,
        command.Metadata.Agent
    );

    public static List<HookSlashCommandInfo> ToHookSlashCommandInfos(IEnumerable<SlashCommandInfo> commands)
        => commands.Select(ToHookSlashCommandInfo).ToList();

    private static List<SlashCommandInfo> DiscoverCommandsFromDir(string commandsDir, CommandScope scope, string prefix = "")
    {
        var result = new List<SlashCommandInfo>();
        if (!Directory.Exists(commandsDir)) return result;

        foreach (var entry in Directory.EnumerateFileSystemEntries(commandsDir))
        {
            var name = Path.GetFileName(entry);
            if (Directory.Exists(entry))
            {
                if (ExcludedDirs.Contains(name) || name.StartsWith(".", StringComparison.Ordinal)) continue;
                var nestedPrefix = string.IsNullOrEmpty(prefix) ? name : $"{prefix}/{name}";
                result.AddRange(DiscoverCommandsFromDir(entry, scope, nestedPrefix));
                continue;
            }

            if (name.StartsWith(".", StringComparison.Ordinal) || !name.EndsWith(".md", StringComparison.Ordinal)) continue;

            var baseCommandName = Path.GetFileNameWithoutExtension(name);
            var commandName = string.IsNullOrEmpty(prefix) ? baseCommandName : $"{prefix}/{baseCommandName}";

            try
            {
                var content = File.ReadAllText(entry);
                var parsed = Frontmatter.Parse(content);
                var data = parsed.Data ?? new System.Text.Json.Nodes.JsonObject();
                result.Add(new SlashCommandInfo(
                    commandName,
                    entry,
                    BuildMetadata(commandName, data),
                    parsed.Body,
                    scope
                ));
            }
            catch { continue; }
        }
        return result;
    }

    private static CommandMetadata BuildMetadata(string name, System.Text.Json.Nodes.JsonObject data)
    {
        return new CommandMetadata(
            name,
            GetString(data, "description") ?? "",
            GetString(data, "argument-hint"),
            SanitizeModelField(GetString(data, "model")),
            GetString(data, "agent"),
            data.TryGetPropertyValue("subtask", out var subtask) && subtask?.GetValueKind() == JsonValueKind.True
        );
    }

    private static string? SanitizeModelField(string? model)
    {
        if (model is not null && model.Trim().Length > 0) return model.Trim();
        return null;
    }

    private static string? GetString(System.Text.Json.Nodes.JsonObject obj, string key)
        => obj.TryGetPropertyValue(key, out var node) && node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>().Trim() is { Length: > 0 } s ? s : null : null;

    private static List<SlashCommandInfo> DeduplicateByName(List<SlashCommandInfo> commands)
    {
        var seen = new HashSet<string>();
        var result = new List<SlashCommandInfo>();
        foreach (var cmd in commands)
        {
            if (seen.Add(cmd.Name)) result.Add(cmd);
        }
        return result;
    }
}
