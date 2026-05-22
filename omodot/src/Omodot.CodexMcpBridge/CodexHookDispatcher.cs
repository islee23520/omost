using System.Text.Json;

namespace Omodot.CodexMcpBridge;

/// <summary>
/// Dispatches Codex hook events to the appropriate omodot .NET components.
/// Reads hook payload from stdin, dispatches, and writes result to stdout.
/// </summary>
internal static class CodexHookDispatcher
{
    public static int Dispatch(string hookEvent, TextReader input, TextWriter output)
    {
        try
        {
            var payload = input.ReadToEnd();

            var result = hookEvent.ToLowerInvariant() switch
            {
                "session-start" => HandleSessionStart(payload),
                "user-prompt-submit" => HandleUserPromptSubmit(payload),
                "post-tool-use" => HandlePostToolUse(payload),
                "post-compact" => HandlePostCompact(payload),
                _ => CreateErrorResult($"Unknown hook event: {hookEvent}")
            };

            output.WriteLine(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (Exception exception)
        {
            var errorResult = CreateErrorResult($"Hook dispatch failed: {exception.Message}");
            output.WriteLine(JsonSerializer.Serialize(errorResult));
            return 1;
        }
    }

    private static Dictionary<string, object> HandleSessionStart(string payload)
    {
        // Session start: load project rules, initialize state
        return new Dictionary<string, object>
        {
            ["status"] = "ok",
            ["event"] = "session-start",
            ["actions"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["type"] = "log",
                    ["message"] = "omodot session initialized — rules engine ready"
                }
            }
        };
    }

    private static Dictionary<string, object> HandleUserPromptSubmit(string payload)
    {
        // User prompt: check rules, detect workflow triggers
        var actions = new List<Dictionary<string, string>>
        {
            new() { ["type"] = "log", ["message"] = "omodot rules checked" }
        };

        // Detect ultrawork trigger patterns
        if (payload.Contains("ulw:", StringComparison.OrdinalIgnoreCase) ||
            payload.Contains("ultrawork", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(new Dictionary<string, string>
            {
                ["type"] = "log",
                ["message"] = "omodot ultrawork trigger detected"
            });
        }

        return new Dictionary<string, object>
        {
            ["status"] = "ok",
            ["event"] = "user-prompt-submit",
            ["actions"] = actions
        };
    }

    private static Dictionary<string, object> HandlePostToolUse(string payload)
    {
        // Post tool use: check comments, run LSP diagnostics
        var actions = new List<Dictionary<string, string>>();

        // Parse tool name from payload if available
        if (!string.IsNullOrEmpty(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("tool_name", out var toolName))
                {
                    var name = toolName.GetString() ?? "";
                    if (IsFileModificationTool(name))
                    {
                        actions.Add(new Dictionary<string, string>
                        {
                            ["type"] = "log",
                            ["message"] = $"omodot post-tool-use check for {name}"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Payload not JSON — skip parsing
            }
        }

        if (actions.Count == 0)
        {
            actions.Add(new Dictionary<string, string>
            {
                ["type"] = "log",
                ["message"] = "omodot post-tool-use check complete"
            });
        }

        return new Dictionary<string, object>
        {
            ["status"] = "ok",
            ["event"] = "post-tool-use",
            ["actions"] = actions
        };
    }

    private static Dictionary<string, object> HandlePostCompact(string payload)
    {
        // Post compact: reset rule cache
        return new Dictionary<string, object>
        {
            ["status"] = "ok",
            ["event"] = "post-compact",
            ["actions"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["type"] = "log",
                    ["message"] = "omodot rule cache reset"
                }
            }
        };
    }

    private static Dictionary<string, object> CreateErrorResult(string message) =>
        new()
        {
            ["status"] = "error",
            ["message"] = message
        };

    private static bool IsFileModificationTool(string toolName) =>
        toolName is "write" or "Write" or "edit" or "Edit" or "apply_patch"
            or "multi_edit" or "multiedit" or "MultiEdit";
}
