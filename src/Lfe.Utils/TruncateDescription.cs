namespace Lfe.Utils;

public static class TruncateDescription
{
    public static string Apply(string description, int maxLength = 120)
    {
        if (description.Length == 0 || description.Length <= maxLength)
        {
            return description;
        }

        return description[..Math.Max(0, maxLength - 3)] + "...";
    }
}
