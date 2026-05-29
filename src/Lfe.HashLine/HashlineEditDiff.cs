namespace Lfe.HashLine;

public static class HashlineEditDiff
{
    public static string GenerateHashlineDiff(string oldContent, string newContent, string filePath)
    {
        var oldLines = oldContent.Split('\n');
        var newLines = newContent.Split('\n');
        var parts = new List<string> { $"--- {filePath}\n+++ {filePath}\n" };
        var maxLines = Math.Max(oldLines.Length, newLines.Length);

        for (var index = 0; index < maxLines; index += 1)
        {
            var hasOld = index < oldLines.Length;
            var hasNew = index < newLines.Length;
            var oldLine = hasOld ? oldLines[index] : string.Empty;
            var newLine = hasNew ? newLines[index] : string.Empty;
            var lineNumber = index + 1;
            var hash = HashComputation.ComputeLineHash(lineNumber, newLine);

            if (!hasOld)
            {
                parts.Add($"+ {lineNumber}#{hash}|{newLine}\n");
                continue;
            }

            if (!hasNew)
            {
                parts.Add($"- {lineNumber}#  |{oldLine}\n");
                continue;
            }

            if (oldLine != newLine)
            {
                parts.Add($"- {lineNumber}#  |{oldLine}\n");
                parts.Add($"+ {lineNumber}#{hash}|{newLine}\n");
            }
        }

        return string.Concat(parts);
    }
}
