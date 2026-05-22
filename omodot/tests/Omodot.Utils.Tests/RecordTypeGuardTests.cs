using System.Text.Json.Nodes;

namespace Omodot.Utils.Tests;

public sealed class RecordTypeGuardTests
{
    [Fact]
    public void IsRecord_returns_true_for_json_nodes()
    {
        Assert.True(RecordTypeGuard.IsRecord(new JsonObject()));
        Assert.True(RecordTypeGuard.IsRecord(new JsonArray()));
        Assert.False(RecordTypeGuard.IsRecord(null));
    }
}
