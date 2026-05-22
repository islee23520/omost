using System.Text.RegularExpressions;

namespace Omodot.HashLine;

public static class HashlineConstants
{
    public const string NibbleString = "ZPMQVRWSNKTXJBYH";

    public static readonly IReadOnlyList<string> HashlineDictionary = Enumerable
        .Range(0, 256)
        .Select(index => $"{NibbleString[index >> 4]}{NibbleString[index & 0x0F]}")
        .ToArray();

    public static readonly Regex HashlineRefPattern = new(
        @"^([0-9]+)#([ZPMQVRWSNKTXJBYH]{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly Regex HashlineOutputPattern = new(
        @"^([0-9]+)#([ZPMQVRWSNKTXJBYH]{2})\|(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
