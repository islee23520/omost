using Lfe.CommentChecker;
using Xunit;

namespace Lfe.CommentChecker.Tests;

public class ApplyPatchEditsTests
{
    [Fact]
    public void IsRecord_Works() => Assert.True(ApplyPatchEdits.IsRecord(new Dictionary<string, object>()));
    [Fact]
    public void IsRecord_Null() => Assert.False(ApplyPatchEdits.IsRecord(null));
    [Fact]
    public void IsRecord_String() => Assert.False(ApplyPatchEdits.IsRecord("nope"));

    [Fact]
    public void JoinPatchLines_Empty() => Assert.Equal("", ApplyPatchEdits.JoinPatchLines([]));
    [Fact]
    public void JoinPatchLines_Joins() => Assert.Equal("one\ntwo\n", ApplyPatchEdits.JoinPatchLines(["one", "two"]));

    [Fact]
    public void MakeAccumulator_CreatesCorrectly()
    {
        var acc = ApplyPatchEdits.MakeAccumulator("add", "file.txt");
        Assert.Equal("add", acc.Operation);
        Assert.Equal("file.txt", acc.FilePath);
        Assert.Empty(acc.OldLines);
        Assert.Empty(acc.NewLines);
    }

    [Fact]
    public void ReadApplyPatchMetadataFiles_NormalizesKeys()
    {
        var result = ApplyPatchEdits.ReadApplyPatchMetadataFiles(new List<object>
        {
            new Dictionary<string, object> { ["file_path"] = "src/a.ts", ["move_path"] = "src/b.ts", ["old_string"] = "before", ["new_string"] = "after", ["operation"] = "update" },
        });
        Assert.Single(result);
        Assert.Equal("src/a.ts", result[0].FilePath);
        Assert.Equal("src/b.ts", result[0].MovePath);
        Assert.Equal("update", result[0].Type);
    }

    [Fact]
    public void ReadApplyPatchMetadataFiles_SkipsInvalid()
    {
        var result = ApplyPatchEdits.ReadApplyPatchMetadataFiles(new List<object> { null!, new Dictionary<string, object> { ["filePath"] = "missing-fields" } });
        Assert.Empty(result);
    }

    [Fact]
    public void ParseApplyPatchRequests_AddUpdateMoveDelete()
    {
        var patch = string.Join("\n", [
            "*** Begin Patch", "*** Add File: src/new.ts", "+export const added = true", "+",
            "*** Update File: src/old.ts", "@@", "-old line", "+new line",
            "*** Move to: src/moved.ts", "*** Delete File: src/remove.ts", "*** End Patch"
        ]);
        var edits = ApplyPatchEdits.ParseApplyPatchRequests(patch);
        Assert.Equal(2, edits.Count);
        Assert.Equal("src/new.ts", edits[0].FilePath);
        Assert.Equal("", edits[0].Before);
        Assert.Contains("export const added = true", edits[0].After);
        Assert.Equal("src/moved.ts", edits[1].FilePath);
    }

    [Fact]
    public void ExtractApplyPatchEdits_FiltersDelete()
    {
        var details = new Dictionary<string, object>
        {
            ["files"] = new List<object>
            {
                new Dictionary<string, object> { ["filePath"] = "src/keep.ts", ["before"] = "a", ["after"] = "b" },
                new Dictionary<string, object> { ["filePath"] = "src/drop.ts", ["before"] = "x", ["after"] = "y", ["type"] = "delete" },
            }
        };
        var edits = ApplyPatchEdits.ExtractApplyPatchEdits(details);
        Assert.Single(edits);
        Assert.Equal("src/keep.ts", edits[0].FilePath);
    }

    [Fact]
    public void ExtractApplyPatchEdits_FallsBackToPatch()
    {
        var args = new Dictionary<string, object>
        {
            ["input"] = string.Join("\n", ["*** Begin Patch", "*** Add File: src/fallback.ts", "+fallback", "*** End Patch"])
        };
        var edits = ApplyPatchEdits.ExtractApplyPatchEdits(null, args);
        Assert.Single(edits);
        Assert.Equal("src/fallback.ts", edits[0].FilePath);
    }

    [Fact]
    public void ExtractApplyPatchEdits_NoInput() => Assert.Empty(ApplyPatchEdits.ExtractApplyPatchEdits(null));
}

public class RunnerTests
{
    [Fact]
    public void ResolveCommentCheckerBinary_CachedExists() => Assert.Equal("/cache/bin", Runner.ResolveCommentCheckerBinary("cc", "/cache/bin", p => p == "/cache/bin"));

    [Fact]
    public void ResolveCommentCheckerBinary_CachedMissing() => Assert.Null(Runner.ResolveCommentCheckerBinary("cc", "/cache/bin", _ => false));

    [Fact]
    public void ResolveCommentCheckerBinary_NoCache() => Assert.Null(Runner.ResolveCommentCheckerBinary("cc", null, _ => false));
}
