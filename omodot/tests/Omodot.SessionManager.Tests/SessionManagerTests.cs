using Omodot.SessionManager;
using Xunit;

namespace Omodot.SessionManager.Tests;

public class SessionManagerTests
{
    [Fact]
    public void FormatSessionList_Empty_ReturnsNoSessions()
        => Assert.Equal("No sessions found.", SessionFormatter.FormatSessionList([]));

    [Fact]
    public void FormatSessionList_WithRecords_ReturnsTable()
    {
        var info = new SessionInfo("abc", 5, DateTimeOffset.Parse("2024-01-01"), DateTimeOffset.Parse("2024-01-02"), ["build"]);
        var records = new[] { new SessionRecord(info, []) };
        var result = SessionFormatter.FormatSessionList(records);
        Assert.Contains("abc", result);
        Assert.Contains("build", result);
    }

    [Fact]
    public void FormatSessionMessages_Empty_ReturnsNoMessages()
        => Assert.Equal("No messages found in this session.", SessionFormatter.FormatSessionMessages([]));

    [Fact]
    public void BuildSessionInfo_Empty_ReturnsNull()
        => Assert.Null(SessionFormatter.BuildSessionInfo("x", []));

    [Fact]
    public void BuildSessionInfo_WithMessages_ReturnsInfo()
    {
        var msgs = new[] { new SessionMessage("1", "user", "hello", CreatedAt: 1700000000000) };
        var info = SessionFormatter.BuildSessionInfo("s1", msgs);
        Assert.NotNull(info);
        Assert.Equal("s1", info.Id);
        Assert.Equal(1, info.MessageCount);
    }

    [Fact]
    public void SearchInSession_FindsMatch()
    {
        var msgs = new[] { new SessionMessage("1", "user", "hello world foo bar") };
        var results = SessionFormatter.SearchInSession("s1", msgs, "world");
        Assert.Single(results);
        Assert.Equal(1, results[0].MatchCount);
    }

    [Fact]
    public void SearchInSession_NoMatch_ReturnsEmpty()
    {
        var msgs = new[] { new SessionMessage("1", "user", "hello") };
        Assert.Empty(SessionFormatter.SearchInSession("s1", msgs, "xyz"));
    }

    [Fact]
    public void FilterSessionsByDate_FiltersCorrectly()
    {
        var info = new SessionInfo("1", 1, LastMessage: DateTimeOffset.Parse("2024-06-15"));
        var records = new[] { new SessionRecord(info, []) };
        var filtered = SessionFormatter.FilterSessionsByDate(records, "2024-01-01", "2024-03-01");
        Assert.Empty(filtered);
        var kept = SessionFormatter.FilterSessionsByDate(records, "2024-01-01", "2024-12-31");
        Assert.Single(kept);
    }
}
