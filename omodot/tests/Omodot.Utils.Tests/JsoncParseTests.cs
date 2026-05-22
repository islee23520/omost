namespace Omodot.Utils.Tests;

public sealed class JsoncParseTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"omodot-utils-jsonc-{Guid.NewGuid():N}");

    public JsoncParseTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Parse_supports_comments_and_trailing_commas()
    {
        var result = JsoncParse.Parse<JsoncDocument>("{\n // comment\n \"key\": \"value\",\n}");
        Assert.Equal("value", result.Key);
    }

    [Fact]
    public void ParseSafe_returns_errors_on_invalid_jsonc()
    {
        var result = JsoncParse.ParseSafe<JsoncDocument>("{ invalid }");
        Assert.Null(result.Data);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ReadFile_reads_jsonc_from_disk()
    {
        var filePath = Path.Combine(_tempDirectory, "config.jsonc");
        File.WriteAllText(filePath, "{\n // comment\n \"key\": \"value\"\n}");
        var result = JsoncParse.ReadFile<JsoncDocument>(filePath);
        Assert.Equal("value", result!.Key);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    private sealed record JsoncDocument(string Key);
}
