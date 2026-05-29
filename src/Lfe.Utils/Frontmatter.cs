using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Lfe.Utils;

public sealed record FrontmatterResult<T>(T? Data, string Body, bool HadFrontmatter, bool ParseError);

public static partial class Frontmatter
{
    public static FrontmatterResult<JsonObject> Parse(string content)
    {
        var typed = Parse<JsonObject>(content);
        return new FrontmatterResult<JsonObject>(typed.Data ?? [], typed.Body, typed.HadFrontmatter, typed.ParseError);
    }

    public static FrontmatterResult<T> Parse<T>(string content)
    {
        var match = FrontmatterPattern().Match(content);
        if (!match.Success)
        {
            return new FrontmatterResult<T>(default, content, false, false);
        }

        var yamlContent = match.Groups[1].Value;
        var body = match.Groups[2].Value;

        try
        {
            var dataNode = ParseYamlObject(yamlContent);
            var data = dataNode.Deserialize<T>(JsonDefaults.Options);
            return new FrontmatterResult<T>(data, body, true, false);
        }
        catch
        {
            return new FrontmatterResult<T>(default, body, true, true);
        }
    }

    private static JsonObject ParseYamlObject(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return [];
        }

        var stream = new YamlStream();
        using var reader = new StringReader(yamlContent);
        stream.Load(reader);

        if (stream.Documents.Count == 0)
        {
            return [];
        }

        return ConvertYamlNode(stream.Documents[0].RootNode) as JsonObject ?? [];
    }

    private static JsonNode? ConvertYamlNode(YamlNode node)
    {
        return node switch
        {
            YamlMappingNode mapping => ConvertMapping(mapping),
            YamlSequenceNode sequence => ConvertSequence(sequence),
            YamlScalarNode scalar => ConvertScalar(scalar),
            _ => null,
        };
    }

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var result = new JsonObject();

        foreach (var pair in mapping.Children)
        {
            var key = (pair.Key as YamlScalarNode)?.Value ?? string.Empty;
            result[key] = ConvertYamlNode(pair.Value);
        }

        return result;
    }

    private static JsonArray ConvertSequence(YamlSequenceNode sequence)
    {
        var result = new JsonArray();

        foreach (var child in sequence.Children)
        {
            result.Add(ConvertYamlNode(child));
        }

        return result;
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var booleanValue))
        {
            return JsonValue.Create(booleanValue);
        }

        if (long.TryParse(value, out var longValue))
        {
            return JsonValue.Create(longValue);
        }

        if (decimal.TryParse(value, out var decimalValue))
        {
            return JsonValue.Create(decimalValue);
        }

        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~")
        {
            return null;
        }

        return JsonValue.Create(value);
    }

    [GeneratedRegex("^---\\r?\\n([\\s\\S]*?)\\r?\\n?---\\r?\\n([\\s\\S]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex FrontmatterPattern();
}
