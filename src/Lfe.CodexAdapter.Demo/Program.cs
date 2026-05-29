using Lfe.CodexAdapter;
using Lfe.UlwHostContract;

Console.WriteLine("=== Lfe CodexAdapter Demo ===");
Console.WriteLine();

var options = new CodexAdapterOptions
{
    CodexBinaryPath = FindCodexBinary(),
    TimeoutMs = 60_000,
};

if (options.CodexBinaryPath is null)
{
    Console.WriteLine("[ERROR] codex binary not found in PATH");
    return 1;
}

Console.WriteLine($"Codex binary: {options.CodexBinaryPath}");
Console.WriteLine($"Timeout:      {options.TimeoutMs}ms");
Console.WriteLine();

using var host = new CodexUlwHost(new CodexBinaryResolver().ResolveConfig(options));

var events = new List<UlwSessionEvent>();
host.OnEvent(evt =>
{
    var color = evt.Type switch
    {
        UlwSessionEventType.Idle => "🟢",
        UlwSessionEventType.Completed => "✅",
        UlwSessionEventType.Error => "🔴",
        _ => "⚪",
    };
    Console.WriteLine($"  {color} Event: {evt.Type} (session={evt.SessionId})");
});

var sessionId = $"demo-{Guid.NewGuid():N}".Substring(0, 13);
var prompt = args.Length > 0 ? string.Join(" ", args) : "Say hello in Korean, Japanese, and English. Keep it to 3 lines.";

Console.WriteLine($"Session ID: {sessionId}");
Console.WriteLine($"Prompt:     {prompt}");
Console.WriteLine();
Console.WriteLine("--- Dispatching to codex CLI... ---");
Console.WriteLine();

var receipt = await host.DispatchPromptAsync(new UlwPromptRequest(sessionId, prompt));

Console.WriteLine();
Console.WriteLine("--- Receipt ---");
Console.WriteLine($"  Accepted:   {receipt.Accepted}");
Console.WriteLine($"  SessionId:  {receipt.SessionId}");
Console.WriteLine($"  DispatchId: {receipt.DispatchId}");
Console.WriteLine();

var status = await host.ReadStatusAsync(sessionId);
Console.WriteLine($"--- Status: {status} ---");
Console.WriteLine();

var messages = await host.ReadMessagesAsync(sessionId);
Console.WriteLine("--- Messages ---");
foreach (var msg in messages)
{
    var label = msg.Role switch
    {
        "assistant" => "🤖",
        "user" => "👤",
        "system" => "⚙️",
        _ => "📝",
    };
    Console.WriteLine($"  {label} [{msg.Role}]: {msg.Text}");
}

Console.WriteLine();

var todos = await host.ReadTodosAsync(sessionId);
Console.WriteLine($"--- Todos: {todos.Count} items ---");

Console.WriteLine();
Console.WriteLine("=== Demo Complete ===");
return 0;

static string? FindCodexBinary()
{
    var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
    foreach (var dir in pathEnv.Split(Path.PathSeparator))
    {
        var candidate = Path.Combine(dir, "codex");
        if (File.Exists(candidate))
            return candidate;
    }
    return null;
}
