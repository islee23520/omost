namespace Lfe.Utils.Tests;

public sealed class IsAbortErrorTests
{
    [Fact]
    public void Check_detects_abort_from_string_exception_and_info()
    {
        Assert.True(IsAbortError.Check("operation canceled"));
        Assert.True(IsAbortError.Check(new InvalidOperationException("Request aborted by user")));
        Assert.True(IsAbortError.Check(new AbortErrorInfo("DOMException", "The operation was aborted")));
    }
}
