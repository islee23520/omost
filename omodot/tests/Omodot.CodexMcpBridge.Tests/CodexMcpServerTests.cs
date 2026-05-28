using Omodot.CodexAdapter;
using Omodot.CodexMcpBridge;
using Xunit;

namespace Omodot.CodexMcpBridge.Tests;

public sealed class CodexMcpServerTests
{
    private static CodexResolvedConfig CreateTestConfig() => new(
        BinaryPath: "/usr/bin/true",
        WorkingDirectory: Path.GetTempPath(),
        TimeoutMs: 5000,
        EnvironmentOverrides: new Dictionary<string, string>(),
        SessionOptions: new CodexSessionOptions());

    [Fact]
    public async Task DispatchAsync_MapsFieldsToUlwPromptRequest_InCorrectOrder()
    {
        // REGRESSION TEST: This verifies that UlwPromptRequest is constructed with (sessionId, prompt)
        // instead of (prompt, sessionId). 
        // UlwPromptRequest definition: (string SessionId, string Message, ...)
        // If swapped, the returned SessionId in the result would be the prompt text.
        
        using var server = new CodexMcpToolServer(CreateTestConfig());
        const string prompt = "test-prompt-content";
        const string sessionId = "test-session-123";

        var result = await server.DispatchAsync(prompt, sessionId);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.NotEqual(prompt, result.SessionId);
    }
}
