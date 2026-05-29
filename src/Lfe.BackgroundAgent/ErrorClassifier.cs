using System.Text.Json;

namespace Lfe.BackgroundAgent;

public static class ErrorClassifier
{
    public static bool IsRecord(object? value)
    {
        return ObjectRecord.IsRecord(value);
    }

    public static bool IsAbortedSessionError(object? error)
    {
        return GetErrorText(error).Contains("aborted", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetErrorText(object? error)
    {
        return error switch
        {
            null => string.Empty,
            string text => text,
            Exception exception => $"{exception.GetType().Name}: {exception.Message}",
            _ => GetRecordErrorText(error),
        };
    }

    public static string? ExtractErrorName(object? error)
    {
        return ObjectRecord.GetString(ObjectRecord.AsRecord(error), "name") ??
               (error as Exception)?.GetType().Name;
    }

    public static string? ExtractErrorMessage(object? error)
    {
        if (error is null)
        {
            return null;
        }

        if (error is string text)
        {
            return text;
        }

        var record = ObjectRecord.AsRecord(error);
        if (record is not null)
        {
            var dataRaw = record.TryGetValue("data", out var dataValue) ? dataValue : null;
            var dataRecord = ObjectRecord.AsRecord(dataRaw);
            var candidates = new[]
            {
                dataRaw,
                dataRecord is not null && dataRecord.TryGetValue("error", out var nestedError) ? nestedError : null,
                record.TryGetValue("error", out var directError) ? directError : null,
                record.TryGetValue("cause", out var cause) ? cause : null,
                error,
            };

            foreach (var candidate in candidates)
            {
                if (candidate is string candidateText && candidateText.Length > 0)
                {
                    return candidateText;
                }

                var candidateRecord = ObjectRecord.AsRecord(candidate);
                var message = ObjectRecord.GetString(candidateRecord, "message");
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }
        }

        if (error is Exception exception)
        {
            return exception.Message;
        }

        try
        {
            return JsonSerializer.Serialize(error);
        }
        catch
        {
            return error.ToString();
        }
    }

    public static int? ExtractErrorStatusCode(object? error)
    {
        var record = ObjectRecord.AsRecord(error);
        if (record is null)
        {
            return null;
        }

        foreach (var key in new[] { "statusCode", "status", "code" })
        {
            var statusCode = ObjectRecord.GetInt32(record, key);
            if (statusCode is >= 100 and < 600)
            {
                return statusCode;
            }
        }

        var response = ObjectRecord.GetRecord(record, "response");
        var responseStatus = ObjectRecord.GetInt32(response, "status");
        return responseStatus is >= 100 and < 600 ? responseStatus : null;
    }

    public static string? GetSessionErrorMessage(IReadOnlyDictionary<string, object?> properties)
    {
        var error = ObjectRecord.GetRecord(properties, "error");
        if (error is null)
        {
            return null;
        }

        var data = ObjectRecord.GetRecord(error, "data");
        return ObjectRecord.GetString(data, "message") ?? ObjectRecord.GetString(error, "message");
    }

    private static string GetRecordErrorText(object? error)
    {
        var record = ObjectRecord.AsRecord(error);
        if (record is null)
        {
            return string.Empty;
        }

        return ObjectRecord.GetString(record, "message") ??
               ObjectRecord.GetString(record, "name") ??
               string.Empty;
    }
}
