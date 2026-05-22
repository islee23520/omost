namespace Omodot.CommandExecutor;

public static class CommandResolution
{
    public static async Task<string> ResolveCommandsInTextAsync(
        string text,
        int depth = 0,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (depth >= maxDepth)
        {
            return text;
        }

        var matches = EmbeddedCommands.FindEmbeddedCommands(text);
        if (matches.Count == 0)
        {
            return text;
        }

        var tasks = matches.Select(match => ExecuteReplacementAsync(match.Command, cancellationToken)).ToArray();
        var replacements = await Task.WhenAll(tasks).ConfigureAwait(false);

        var resolved = text;
        for (var i = 0; i < matches.Count; i++)
        {
            resolved = resolved.Replace(matches[i].FullMatch, replacements[i], StringComparison.Ordinal);
        }

        return EmbeddedCommands.FindEmbeddedCommands(resolved).Count > 0
            ? await ResolveCommandsInTextAsync(resolved, depth + 1, maxDepth, cancellationToken).ConfigureAwait(false)
            : resolved;
    }

    private static async Task<string> ExecuteReplacementAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            return await CommandExecution.ExecuteCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"[error: {ex.Message}]";
        }
    }
}
