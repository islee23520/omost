namespace Omodot.Utils.Tests;

public sealed class FormatDurationTests
{
    [Fact]
    public void Human_formats_seconds_minutes_and_hours()
    {
        Assert.Equal("0s", FormatDuration.Human(999));
        Assert.Equal("1m 0s", FormatDuration.Human(60000));
        Assert.Equal("1h 0m 0s", FormatDuration.Human(3600000));
    }
}
