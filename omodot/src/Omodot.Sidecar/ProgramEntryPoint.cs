namespace Omodot.Sidecar;

internal static class ProgramEntryPoint
{
    public static async Task<int> RunAsync()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += HandleCancelKeyPress;

        try
        {
            await using var input = Console.OpenStandardInput();
            await using var output = Console.OpenStandardOutput();

            var server = new JsonRpcServer(input, output, Console.Error);
            await server.RunAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"omodot sidecar terminated: {exception}").ConfigureAwait(false);
            await Console.Error.FlushAsync().ConfigureAwait(false);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= HandleCancelKeyPress;
        }

        void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            cancellationTokenSource.Cancel();
        }
    }
}
