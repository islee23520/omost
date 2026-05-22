using System.Text.RegularExpressions;

namespace Omodot.CommandExecutor;

public static partial class EmbeddedCommands
{
    [GeneratedRegex("!`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex CommandPattern();

    public static IReadOnlyList<CommandMatch> FindEmbeddedCommands(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var matches = CommandPattern().Matches(text);
        if (matches.Count == 0)
        {
            return Array.Empty<CommandMatch>();
        }

        var results = new List<CommandMatch>(matches.Count);
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            results.Add(new CommandMatch(
                match.Value,
                match.Groups[1].Value,
                match.Index,
                match.Index + match.Length));
        }

        return results;
    }
}
