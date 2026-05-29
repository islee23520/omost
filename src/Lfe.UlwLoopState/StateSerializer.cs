using System.Globalization;
using System.Text.Json;

namespace Lfe.UlwLoopState;

public static class StateSerializer
{
    public static string Serialize(UlwLoopState state)
    {
        var lines = new List<string>
        {
            "---",
            $"active: {state.Active.ToString().ToLowerInvariant()}",
            $"iteration: {state.Iteration}",
            $"max_iterations: {state.MaxIterations}",
            $"completion_promise: {Quote(state.CompletionPromise)}",
            $"initial_completion_promise: {Quote(state.InitialCompletionPromise)}",
            $"started_at: {Quote(state.StartedAt)}",
            $"session_id: {Quote(state.SessionID)}",
            $"strategy: {Quote(state.Strategy.ToString().ToLowerInvariant())}",
        };
        if (state.MessageCountAtStart.HasValue) lines.Add($"message_count_at_start: {state.MessageCountAtStart.Value}");
        if (state.Ultrawork == true) lines.Add("ultrawork: true");
        if (state.VerificationPending == true) lines.Add("verification_pending: true");
        if (state.VerificationAttemptID is not null) lines.Add($"verification_attempt_id: {Quote(state.VerificationAttemptID)}");
        if (state.VerificationSessionID is not null) lines.Add($"verification_session_id: {Quote(state.VerificationSessionID)}");
        return $"{string.Join("\n", lines)}\n---\n{state.Prompt}\n";
    }

    public static UlwLoopState? Deserialize(string content)
    {
        var opening = content.StartsWith("---\n") ? 4 : content.StartsWith("---\r\n") ? 5 : 0;
        if (opening == 0) return null;
        var closing = content.IndexOf("\n---", opening, StringComparison.Ordinal);
        if (closing == -1) return null;
        var yaml = content[opening..closing];
        var body = content[(closing + 4)..].Trim('\r', '\n');

        var data = ParseSimpleYaml(yaml);
        if (!data.TryGetValue("active", out var activeVal)) return null;
        if (!data.TryGetValue("iteration", out var iterVal)) return null;

        var active = activeVal is "true" or "True";
        var iteration = int.TryParse(iterVal, out var i) ? i : 0;
        var maxIter = data.TryGetValue("max_iterations", out var mi) && int.TryParse(mi, out var m) ? m : UlwLoopConstants.DefaultMaxIterations;
        var completionPromise = Strip(data.GetValueOrDefault("completion_promise", UlwLoopConstants.DefaultCompletionPromise));
        var initialPromise = Strip(data.GetValueOrDefault("initial_completion_promise", completionPromise));
        var startedAt = Strip(data.GetValueOrDefault("started_at", DateTime.Now.ToString("O")));
        var sessionId = Strip(data.GetValueOrDefault("session_id", ""));
        var strategyStr = Strip(data.GetValueOrDefault("strategy", "continue"));
        var strategy = strategyStr == "reset" ? UlwLoopStrategy.Reset : UlwLoopStrategy.Continue;
        var ultrawork = data.GetValueOrDefault("ultrawork") is "true" or "True" ? true : (bool?)null;
        var msgCount = data.TryGetValue("message_count_at_start", out var mc) && int.TryParse(mc, out var mcv) ? (int?)mcv : null;
        var verifPending = data.GetValueOrDefault("verification_pending") is "true" or "True" ? true : (bool?)null;
        var verifAttempt = data.TryGetValue("verification_attempt_id", out var va) ? Strip(va) : null;
        var verifSession = data.TryGetValue("verification_session_id", out var vs) ? Strip(vs) : null;

        return new UlwLoopState(active, iteration, maxIter, completionPromise, initialPromise, startedAt, body, sessionId, msgCount, verifPending, verifAttempt, verifSession, strategy, ultrawork);
    }

    private static Dictionary<string, string> ParseSimpleYaml(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var colon = trimmed.IndexOf(':');
            if (colon == -1) continue;
            result[trimmed[..colon].Trim()] = trimmed[(colon + 1)..].Trim();
        }
        return result;
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value);
    private static string Strip(string value) => value.Trim('"', '\'');
}
