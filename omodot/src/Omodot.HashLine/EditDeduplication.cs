namespace Omodot.HashLine;

public static class EditDeduplication
{
    public static (IReadOnlyList<HashlineEdit> Edits, int DeduplicatedEdits) DedupeEdits(IReadOnlyList<HashlineEdit> edits)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<HashlineEdit>();
        var deduplicatedEdits = 0;

        foreach (var edit in edits)
        {
            var key = BuildDedupeKey(edit);
            if (!seen.Add(key))
            {
                deduplicatedEdits += 1;
                continue;
            }

            deduped.Add(edit);
        }

        return (deduped, deduplicatedEdits);
    }

    private static string NormalizeEditPayload(object payload)
    {
        return string.Join("\n", LinePayloadUtilities.ToNewLines(payload));
    }

    private static string CanonicalAnchor(string? anchor)
    {
        return anchor is null ? string.Empty : Validation.NormalizeLineRef(anchor);
    }

    private static string BuildDedupeKey(HashlineEdit edit)
    {
        return edit switch
        {
            ReplaceEdit replace => $"replace|{CanonicalAnchor(replace.Pos)}|{CanonicalAnchor(replace.End)}|{NormalizeEditPayload(replace.Lines)}",
            AppendEdit append => $"append|{CanonicalAnchor(append.Pos)}|{NormalizeEditPayload(append.Lines)}",
            PrependEdit prepend => $"prepend|{CanonicalAnchor(prepend.Pos)}|{NormalizeEditPayload(prepend.Lines)}",
            _ => edit.ToString() ?? edit.GetType().FullName ?? nameof(HashlineEdit),
        };
    }
}
