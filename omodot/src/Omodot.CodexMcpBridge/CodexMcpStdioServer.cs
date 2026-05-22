namespace Omodot.CodexMcpBridge;

public static class CodexMcpStdioServer
{
    public static async Task RunLineDelimitedJsonAsync(
        TextReader input,
        TextWriter output,
        CodexMcpToolServer server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(server);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) return;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var response = await CodexMcpJsonRpcServer.HandleRequestAsync(line, server).ConfigureAwait(false);
            if (response is null) continue;

            await output.WriteLineAsync(response).ConfigureAwait(false);
            await output.FlushAsync().ConfigureAwait(false);
        }
    }
}
