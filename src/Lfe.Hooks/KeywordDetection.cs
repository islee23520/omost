namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class KeywordDetection
{
    #region Keyword Detection

    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex CodeBlockPattern();
    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"^\s*\/[a-zA-Z][\w-]*(?:\s|$)")]
    private static partial Regex SlashCommandLeadPattern();

    [GeneratedRegex(@"^\/([a-zA-Z@][\w.:@/-]*)\s*(.*)")]
    private static partial Regex SlashCommandPattern();

    private static readonly HashSet<string> ExcludedSlashCommands = ["ralph-loop", "cancel-ralph", "ulw-loop"];

    [GeneratedRegex(@"\b(search|find|locate|lookup|look\s*up|explore|discover|scan|grep|query|browse|detect|trace|seek|track|pinpoint|hunt)\b|where\s+is|show\s+me|list\s+all|검색|찾아|탐색|조회|스캔|서치|뒤져|찾기|어디|추적|탐지|찾아봐|찾아내|보여줘|목록|検索|探して|見つけて|サーチ|探索|スキャン|どこ|発見|捜索|見つけ出す|一覧|搜索|查找|寻找|查询|检索|定位|扫描|发现|在哪里|找出来|列出|tìm kiếm|tra cứu|định vị|quét|phát hiện|truy tìm|tìm ra|ở đâu|liệt kê", RegexOptions.IgnoreCase)]
    private static partial Regex SearchPattern();

    [GeneratedRegex(@"\bteam[\s_-]?mode\b|(?<![가-힣])(?:팀\s*모드|팀으로)", RegexOptions.IgnoreCase)]
    private static partial Regex TeamPattern();

    [GeneratedRegex(@"\b(hyperplan|hpp)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HyperplanPattern();

    [GeneratedRegex(@"\b(?:hpp|hyperplan)\s+(?:ulw|ultrawork)\b|\b(?:ulw|ultrawork)\s+(?:hpp|hyperplan)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HyperplanUltraworkPattern();

    [GeneratedRegex(@"\b(analyze|analyse|investigate|examine|research|study|deep[\s-]?dive|inspect|audit|evaluate|assess|review|diagnose|debug|understand)\b|why\s+is|how\s+does|how\s+to|분석|조사|파악|검토|진단|이해|설명|원인|이유|왜|어떻게", RegexOptions.IgnoreCase)]
    private static partial Regex AnalyzePattern();

    public static string RemoveKeywordCodeBlocks(string text) =>
        InlineCodePattern().Replace(CodeBlockPattern().Replace(text, ""), "");

    public static bool LooksLikeSlashCommand(string text) =>
        SlashCommandLeadPattern().IsMatch(text);

    public static List<string> DetectKeywords(string text, string? agentName = null,
        string? modelID = null, KeywordType[]? disabledKeywords = null) =>
        DetectKeywordsWithType(text, agentName, modelID, disabledKeywords)
            .Select(k => k.Message).ToList();

    public static List<DetectedKeyword> DetectKeywordsWithType(string text,
        string? agentName = null, string? modelID = null, KeywordType[]? disabledKeywords = null)
    {
        var textWithoutCode = RemoveKeywordCodeBlocks(text);
        var disabled = new HashSet<KeywordType>(disabledKeywords ?? []);
        if (disabled.Contains(KeywordType.Ultrawork) || disabled.Contains(KeywordType.Hyperplan))
            disabled.Add(KeywordType.HyperplanUltrawork);

        var detectors = new (KeywordType Type, Regex Pattern, string Message)[]
        {
            (KeywordType.Ultrawork, new Regex(@"\b(ultrawork|ulw)\b", RegexOptions.IgnoreCase),
                GetUltraworkDirective(agentName, modelID)),
            (KeywordType.Search, SearchPattern(),
                "[search-mode]\nMAXIMIZE SEARCH EFFORT. Launch multiple background agents IN PARALLEL."),
            (KeywordType.Analyze, AnalyzePattern(),
                "[analyze-mode]\nANALYSIS MODE. Gather context before diving deep."),
            (KeywordType.Team, TeamPattern(),
                "[team-mode]\nTeam mode reference detected. If user wants team-mode work, MUST orchestrate via team_* tools."),
            (KeywordType.Hyperplan, HyperplanPattern(),
                "<hyperplan-mode>\n**MANDATORY**: Say \"HYPERPLAN MODE ENABLED!\" as your first response, exactly once.\n</hyperplan-mode>"),
            (KeywordType.HyperplanUltrawork, HyperplanUltraworkPattern(),
                "<hyperplan-ultrawork-mode>\n**MANDATORY**: Say \"HYPERPLAN ULTRAWORK MODE ENABLED!\" exactly once as your first response.\n</hyperplan-ultrawork-mode>"),
        };

        return detectors
            .Where(d => d.Pattern.IsMatch(textWithoutCode) && !disabled.Contains(d.Type))
            .Select(d => new DetectedKeyword(d.Type, d.Message))
            .ToList();
    }

    private static string GetUltraworkDirective(string? agentName, string? modelID) =>
        (agentName, modelID?.ToLowerInvariant()) switch
        {
            ("prometheus", _) or ("plan", _) =>
                "<ultrawork-mode>\nPlanner ultrawork mode activated. Say \"ULTRAWORK MODE ENABLED!\" first.\n</ultrawork-mode>",
            (_, var m) when m?.Contains("gpt") == true =>
                "<ultrawork-mode>\n**MANDATORY**: You MUST say \"ULTRAWORK MODE ENABLED!\" to the user as your first response when this mode activates.\n[CODE RED] Maximum precision required.\n</ultrawork-mode>",
            (_, var m) when m?.Contains("gemini") == true =>
                "<ultrawork-mode>\n**MANDATORY**: Say \"ULTRAWORK MODE ENABLED!\" first. Gemini ultrawork protocol active.\n</ultrawork-mode>",
            _ => "<ultrawork-mode>\n**MANDATORY**: You MUST say \"ULTRAWORK MODE ENABLED!\" to the user as your first response when this mode activates.\n</ultrawork-mode>",
        };

    #endregion

    #region Slash Command Parsing

    public static ParsedSlashCommand? ParseSlashCommand(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("/")) return null;
        var match = SlashCommandPattern().Match(trimmed);
        if (!match.Success) return null;
        return new ParsedSlashCommand(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Value.Trim(), match.Value);
    }

    public static ParsedSlashCommand? DetectSlashCommand(string text)
    {
        var parsed = ParseSlashCommand(CodeBlockPattern().Replace(text, "").Trim());
        return parsed is not null && !ExcludedSlashCommands.Contains(parsed.Command) ? parsed : null;
    }

    public static int FindSlashCommandPartIndex(List<MessagePartSlim> parts)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.Type == "text" && part.Synthetic != true && (part.Text ?? "").TrimStart().StartsWith("/"))
                return i;
        }
        return -1;
    }

    public static string FormatSlashCommandTemplate(SlashCommandInfo command, string args)
    {
        var sections = new List<string> { $"# /{command.Name} Command\n" };
        if (command.Description is not null) sections.Add($"**Description**: {command.Description}\n");
        if (args.Length > 0) sections.Add($"**User Arguments**: {args}\n");
        if (command.Model is not null) sections.Add($"**Model**: {command.Model}\n");
        if (command.Agent is not null) sections.Add($"**Agent**: {command.Agent}\n");
        sections.Add($"**Scope**: {command.Scope}\n");
        sections.Add("---\n");
        sections.Add("## Command Instructions\n");
        sections.Add((command.Content ?? "").Replace("${user_message}", args).Replace("$ARGUMENTS", args).Trim());
        if (args.Length > 0)
        {
            sections.Add("\n\n---\n");
            sections.Add("## User Request\n");
            sections.Add(args);
        }
        return string.Join("\n", sections);
    }

    #endregion
}

public sealed record MessagePartSlim(string Type, string? Text = null, bool? Synthetic = null);
