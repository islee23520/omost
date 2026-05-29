using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lfe.Utils;

public sealed record LoggerTestOverrides(string? FilePath = null, long? MaxSizeBytes = null, int? MaxBackups = null);

public static class Logger
{
    private const string DefaultLogFilename = "omo.log";
    private const long DefaultMaxLogFileSizeBytes = 50L * 1024L * 1024L;
    private const int DefaultMaxLogFileBackups = 2;
    private const int FlushIntervalMilliseconds = 500;
    private const int BufferSizeLimit = 50;

    private static readonly object SyncLock = new();
    private static string _logFile = Path.Combine(Path.GetTempPath(), DefaultLogFilename);
    private static long _maxLogFileSizeBytes = DefaultMaxLogFileSizeBytes;
    private static int _maxLogFileBackups = DefaultMaxLogFileBackups;
    private static readonly List<string> Buffer = [];
    private static Timer? _flushTimer;

    public static void Log(string message) => Write(message, null);

    public static void Log<T>(string message, T data) => Write(message, JsonSerializer.SerializeToNode(data, JsonDefaults.Options));

    public static string GetLogFilePath() => _logFile;

    public static void SetLoggerForTesting(LoggerTestOverrides overrides)
    {
        lock (SyncLock)
        {
            Buffer.Clear();
            ResetTimer();
            if (overrides.FilePath is not null)
            {
                _logFile = overrides.FilePath;
            }

            if (overrides.MaxSizeBytes is not null)
            {
                _maxLogFileSizeBytes = overrides.MaxSizeBytes.Value;
            }

            if (overrides.MaxBackups is not null)
            {
                _maxLogFileBackups = overrides.MaxBackups.Value;
            }
        }
    }

    public static void ResetLoggerForTesting()
    {
        lock (SyncLock)
        {
            _logFile = Path.Combine(Path.GetTempPath(), DefaultLogFilename);
            _maxLogFileSizeBytes = DefaultMaxLogFileSizeBytes;
            _maxLogFileBackups = DefaultMaxLogFileBackups;
            Buffer.Clear();
            ResetTimer();
        }
    }

    public static void FlushForTesting()
    {
        lock (SyncLock)
        {
            ResetTimer();
            FlushCore();
        }
    }

    private static void Write(string message, JsonNode? data)
    {
        try
        {
            lock (SyncLock)
            {
                var timestamp = DateTimeOffset.UtcNow.ToString("O");
                var serializedData = data is null ? string.Empty : JsonSerializer.Serialize(data, JsonDefaults.Options);
                Buffer.Add($"[{timestamp}] {message} {serializedData}\n");

                if (Buffer.Count >= BufferSizeLimit)
                {
                    FlushCore();
                    return;
                }

                _flushTimer ??= new Timer(static state =>
                {
                    if (state is not null)
                    {
                        ((Action)state)();
                    }
                }, () =>
                {
                    lock (SyncLock)
                    {
                        ResetTimer();
                        FlushCore();
                    }
                }, FlushIntervalMilliseconds, Timeout.Infinite);
            }
        }
        catch
        {
        }
    }

    private static void FlushCore()
    {
        if (Buffer.Count == 0)
        {
            return;
        }

        var data = string.Concat(Buffer);
        Buffer.Clear();

        try
        {
            File.AppendAllText(_logFile, data);
            RotateLogFileIfNeeded();
        }
        catch
        {
        }
    }

    private static void RotateLogFileIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFile))
            {
                return;
            }

            var fileInfo = new FileInfo(_logFile);
            if (fileInfo.Length <= _maxLogFileSizeBytes)
            {
                return;
            }

            var oldest = $"{_logFile}.{_maxLogFileBackups}";
            if (File.Exists(oldest))
            {
                File.Delete(oldest);
            }

            for (var index = _maxLogFileBackups - 1; index >= 1; index--)
            {
                var source = $"{_logFile}.{index}";
                var destination = $"{_logFile}.{index + 1}";
                if (File.Exists(source))
                {
                    File.Move(source, destination, true);
                }
            }

            File.Move(_logFile, $"{_logFile}.1", true);
        }
        catch
        {
        }
    }

    private static void ResetTimer()
    {
        _flushTimer?.Dispose();
        _flushTimer = null;
    }
}
