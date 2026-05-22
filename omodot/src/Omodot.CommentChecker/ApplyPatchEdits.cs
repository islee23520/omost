using System.Text.Json;

namespace Omodot.CommentChecker;

public static class ApplyPatchEdits
{
    public static List<CheckerEdit> ExtractApplyPatchEdits(object? details, Dictionary<string, object>? args = null)
    {
        var metadataEdits = GetApplyPatchMetadataFiles(details)
            .Where(f => f.Type?.ToLowerInvariant() != "delete")
            .Select(f => new CheckerEdit(f.MovePath ?? f.FilePath, f.Before, f.After))
            .ToList();
        if (metadataEdits.Count > 0) return metadataEdits;

        if (args is not null)
        {
            var patch = GetStringFromArgs(args, ["patchText", "input", "patch", "command"]);
            if (patch is not null) return ParseApplyPatchRequests(patch);
        }
        return [];
    }

    public static List<ApplyPatchFileMetadata> GetApplyPatchMetadataFiles(object? details)
    {
        if (details is not Dictionary<string, object> d) return [];
        var direct = ReadApplyPatchMetadataFiles(d.GetValueOrDefault("files"));
        if (direct.Count > 0) return direct;
        if (d.GetValueOrDefault("result") is Dictionary<string, object> rd)
        {
            var result = ReadApplyPatchMetadataFiles(rd.GetValueOrDefault("files"));
            if (result.Count > 0) return result;
        }
        if (d.GetValueOrDefault("metadata") is Dictionary<string, object> md)
            return ReadApplyPatchMetadataFiles(md.GetValueOrDefault("files"));
        return [];
    }

    public static List<ApplyPatchFileMetadata> ReadApplyPatchMetadataFiles(object? value)
    {
        if (value is not List<object> items) return [];
        var files = new List<ApplyPatchFileMetadata>();
        foreach (var item in items)
        {
            if (item is not Dictionary<string, object> obj) continue;
            var filePath = GetStringFromDict(obj, ["filePath", "file_path", "path"]);
            var movePath = GetStringFromDict(obj, ["movePath", "move_path"]);
            var before = GetStringFromDict(obj, ["before", "old", "oldString", "old_string"]);
            var after = GetStringFromDict(obj, ["after", "new", "newString", "new_string"]);
            var type = GetStringFromDict(obj, ["type", "operation"]);
            if (filePath is null || before is null || after is null) continue;
            files.Add(new ApplyPatchFileMetadata(filePath, movePath, before, after, type));
        }
        return files;
    }

    public static List<CheckerEdit> ParseApplyPatchRequests(string patch)
    {
        var edits = new List<CheckerEdit>();
        ApplyPatchAccumulator? current = null;

        void Flush()
        {
            if (current is null) return;
            if (current.Operation == "add")
            {
                var after = JoinPatchLines(current.NewLines);
                if (after.Length > 0) edits.Add(new CheckerEdit(current.FilePath, "", after));
            }
            if (current.Operation == "update")
            {
                var after = JoinPatchLines(current.NewLines);
                if (after.Length > 0) edits.Add(new CheckerEdit(current.MovePath ?? current.FilePath, JoinPatchLines(current.OldLines), after));
            }
            current = null;
        }

        foreach (var line in patch.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line == "*** Begin Patch" || line == "*** End Patch") continue;
            if (line.StartsWith("*** Add File: ")) { Flush(); current = MakeAccumulator("add", line["*** Add File: ".Length..].Trim()); continue; }
            if (line.StartsWith("*** Update File: ")) { Flush(); current = MakeAccumulator("update", line["*** Update File: ".Length..].Trim()); continue; }
            if (line.StartsWith("*** Delete File: ")) { Flush(); current = MakeAccumulator("delete", line["*** Delete File: ".Length..].Trim()); continue; }
            if (line.StartsWith("*** Move to: ")) { if (current?.Operation == "update") current = current with { MovePath = line["*** Move to: ".Length..].Trim() }; continue; }
            if (current is null || line.StartsWith("@@")) continue;
            if (current.Operation == "add") { if (line.StartsWith('+')) current.NewLines.Add(line[1..]); continue; }
            if (current.Operation == "update") { if (line.StartsWith('-')) current.OldLines.Add(line[1..]); if (line.StartsWith('+')) current.NewLines.Add(line[1..]); }
        }
        Flush();
        return edits;
    }

    public static ApplyPatchAccumulator MakeAccumulator(string operation, string filePath) => new(operation, filePath, null, [], []);
    public static string JoinPatchLines(List<string> lines) => lines.Count == 0 ? "" : $"{string.Join("\n", lines)}\n";

    public static bool IsRecord(object? value) => value is Dictionary<string, object>;

    internal static string? GetStringFromDict(Dictionary<string, object> input, string[] keys)
    {
        foreach (var key in keys)
        {
            if (input.GetValueOrDefault(key) is string s && s.Trim().Length > 0) return s.Trim();
        }
        return null;
    }

    internal static string? GetStringFromArgs(Dictionary<string, object> input, string[] keys) => GetStringFromDict(input, keys);
}
