using System.Text;
using System.Text.Json;

namespace Omodot.Utils;

public static class JsoncParse
{
    public static T Parse<T>(string content)
    {
        var normalizedContent = StripBom(content);
        var strippedContent = StripJsonComments(normalizedContent);

        try
        {
            return JsonSerializer.Deserialize<T>(strippedContent, JsonDefaults.Options)
                ?? throw new SyntaxErrorException("JSONC parse error: null result");
        }
        catch (JsonException exception)
        {
            throw new SyntaxErrorException($"JSONC parse error: {exception.Message}", exception);
        }
    }

    public static JsoncParseResult<T> ParseSafe<T>(string content)
    {
        try
        {
            return new JsoncParseResult<T>(Parse<T>(content), []);
        }
        catch (SyntaxErrorException exception)
        {
            return new JsoncParseResult<T>(default, [new JsoncParseError(exception.Message, 0, 0)]);
        }
    }

    public static T? ReadFile<T>(string filePath)
    {
        try
        {
            return Parse<T>(File.ReadAllText(filePath));
        }
        catch
        {
            return default;
        }
    }

    internal static string StripJsonComments(string content)
    {
        var builder = new StringBuilder(content.Length);
        var inString = false;
        var escaped = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inString)
            {
                builder.Append(character);
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                builder.Append(character);
                continue;
            }

            if (character == '/' && index + 1 < content.Length)
            {
                var next = content[index + 1];
                if (next == '/')
                {
                    index += 2;
                    while (index < content.Length && content[index] is not '\r' and not '\n')
                    {
                        index++;
                    }

                    index--;
                    continue;
                }

                if (next == '*')
                {
                    index += 2;
                    while (index + 1 < content.Length && !(content[index] == '*' && content[index + 1] == '/'))
                    {
                        index++;
                    }

                    index++;
                    continue;
                }
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string StripBom(string content) => content.Length > 0 && content[0] == '\uFEFF' ? content[1..] : content;
}

public sealed class SyntaxErrorException : Exception
{
    public SyntaxErrorException(string message) : base(message)
    {
    }

    public SyntaxErrorException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
