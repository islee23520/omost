namespace Omodot.Utils.Tests;

public sealed class CompactionMarkerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"omodot-utils-compaction-{Guid.NewGuid():N}");

    public CompactionMarkerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void IsCompactionAgent_recognizes_compaction_text()
    {
        Assert.True(CompactionMarker.IsCompactionAgent(" COMPACTION "));
    }

    [Fact]
    public void HasCompactionPart_checks_parts()
    {
        Assert.True(CompactionMarker.HasCompactionPart([new CompactionPart("compaction")]));
    }

    [Fact]
    public void IsCompactionMessage_checks_agent_and_parts()
    {
        var message = new CompactionMessage("assistant", new CompactionMessageInfo("compaction"), [new CompactionPart("text")]);
        Assert.True(CompactionMarker.IsCompactionMessage(message));
    }

    [Fact]
    public void GetCompactionPartStorageDir_combines_paths()
    {
        Assert.Equal(Path.Combine(_tempDirectory, "message-1"), CompactionMarker.GetCompactionPartStorageDir("message-1", _tempDirectory));
    }

    [Fact]
    public void HasCompactionPartInStorage_detects_compaction_json()
    {
        var directory = CompactionMarker.GetCompactionPartStorageDir("message-1", _tempDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "part.json"), "{\"type\":\"compaction\"}");
        Assert.True(CompactionMarker.HasCompactionPartInStorage("message-1", _tempDirectory));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
