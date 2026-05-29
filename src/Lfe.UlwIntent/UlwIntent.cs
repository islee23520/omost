using System.Text.RegularExpressions;

namespace Lfe.UlwIntent;

public sealed record UlwIntent(UlwIntentType Type, string Prompt);

public enum UlwIntentType
{
    Ultrawork,
    Hyperplan,
    HyperplanUltrawork,
}

public static class UlwIntentDetector
{
    private static readonly Regex CodeBlockPattern = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex InlineCodePattern = new(@"`[^`]+`", RegexOptions.Compiled);
    private static readonly Regex UltraworkPattern = new(@"\b(ultrawork|ulw)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HyperplanPattern = new(@"\b(hyperplan|hpp)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HyperplanUltraworkPattern = new(
        @"\b(?:hpp|hyperplan)\s+(?:ulw|ultrawork)\b|\b(?:ulw|ultrawork)\s+(?:hpp|hyperplan)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RemoveCode(string text)
        => InlineCodePattern.Replace(CodeBlockPattern.Replace(text, string.Empty), string.Empty);

    public static List<UlwIntent> DetectUlwIntent(string text)
    {
        var normalized = RemoveCode(text);
        var intents = new List<UlwIntent>();

        if (HyperplanUltraworkPattern.IsMatch(normalized))
        {
            intents.Add(new UlwIntent(UlwIntentType.HyperplanUltrawork, GetUlwIntentPrompt(UlwIntentType.HyperplanUltrawork)));
            return intents;
        }

        if (UltraworkPattern.IsMatch(normalized))
            intents.Add(new UlwIntent(UlwIntentType.Ultrawork, GetUlwIntentPrompt(UlwIntentType.Ultrawork)));

        if (HyperplanPattern.IsMatch(normalized))
            intents.Add(new UlwIntent(UlwIntentType.Hyperplan, GetUlwIntentPrompt(UlwIntentType.Hyperplan)));

        return intents;
    }

    public static string GetUlwIntentPrompt(UlwIntentType type) => type switch
    {
        UlwIntentType.HyperplanUltrawork => "HYPERPLAN ULTRAWORK MODE ENABLED!",
        UlwIntentType.Hyperplan => "HYPERPLAN MODE ENABLED!",
        _ => "ULTRAWORK MODE ENABLED!",
    };
}
