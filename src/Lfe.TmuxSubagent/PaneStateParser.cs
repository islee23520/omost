using System.Text.RegularExpressions;

namespace Lfe.TmuxSubagent;

public sealed record ParsedPaneState(
    int WindowWidth,
    int WindowHeight,
    bool WindowActive,
    bool SessionAttached,
    IReadOnlyList<TmuxPaneInfo> Panes);

public static partial class PaneStateParser
{
    private const int MandatoryPaneFieldCount = 10;

    public static ParsedPaneState? ParsePaneStateOutput(string stdout)
    {
        var lines = stdout.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0) return null;

        var parsedLines = lines
            .Select(ParsePaneLine)
            .Where(p => p is not null)
            .Cast<(TmuxPaneInfo Pane, int Ww, int Wh, bool Wa, bool Sa)>()
            .ToList();

        if (parsedLines.Count == 0) return null;

        var latest = parsedLines[^1];
        return new ParsedPaneState(
            latest.Ww,
            latest.Wh,
            latest.Wa,
            latest.Sa,
            parsedLines.Select(p => p.Pane).ToArray());
    }

    private static (TmuxPaneInfo Pane, int Ww, int Wh, bool Wa, bool Sa)? ParsePaneLine(string line)
    {
        var fields = line.Split('\t');
        if (fields.Length < MandatoryPaneFieldCount) return null;

        var paneId = fields[0];
        if (!TryParseInt(fields[1], out var width)) return null;
        if (!TryParseInt(fields[2], out var height)) return null;
        if (!TryParseInt(fields[3], out var left)) return null;
        if (!TryParseInt(fields[4], out var top)) return null;
        if (TryParseActive(fields[5]) is not { } isActive) return null;
        if (!TryParseInt(fields[6], out var windowWidth)) return null;
        if (!TryParseInt(fields[7], out var windowHeight)) return null;
        if (TryParseActive(fields[8]) is not { } windowActive) return null;
        if (TryParseAttached(fields[9]) is not { } sessionAttached) return null;

        var title = fields.Length > MandatoryPaneFieldCount
            ? string.Join("\t", fields[MandatoryPaneFieldCount..])
            : string.Empty;

        return (new TmuxPaneInfo(paneId, width, height, left, top, title, isActive), windowWidth, windowHeight, windowActive, sessionAttached);
    }

    private static bool TryParseInt(string s, out int value)
    {
        value = 0;
        return IntegerRegex().IsMatch(s) && int.TryParse(s, out value);
    }

    private static bool? TryParseActive(string s) => s switch
    {
        "1" => true,
        "0" => false,
        _ => null
    };

    private static bool? TryParseAttached(string s) =>
        IntegerRegex().IsMatch(s) && int.TryParse(s, out var v) ? v > 0 : null;

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IntegerRegex();
}
