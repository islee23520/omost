namespace Omodot.Utils;

public static class PluginConfigDetection
{
    private static readonly Dictionary<string, DetectPluginConfigResult> Cache = new(StringComparer.Ordinal);

    public static DetectPluginConfigResult DetectConfigFile(string basePath)
    {
        var jsoncPath = $"{basePath}.jsonc";
        var jsonPath = $"{basePath}.json";

        if (File.Exists(jsoncPath))
        {
            return new DetectPluginConfigResult("jsonc", jsoncPath);
        }

        if (File.Exists(jsonPath))
        {
            return new DetectPluginConfigResult("json", jsonPath);
        }

        return new DetectPluginConfigResult("none", jsonPath);
    }

    public static void ClearCache() => Cache.Clear();

    public static DetectPluginConfigResult DetectPluginConfigFile(string directory, DetectPluginConfigFileOptions options)
    {
        var cacheKey = GetCacheKey(directory, options);
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var canonicalBasename = options.Basenames[0];
        var canonicalResult = DetectConfigFile(Path.Combine(directory, canonicalBasename));
        var legacyResults = (options.LegacyBasenames ?? []).Select(legacyBasename => DetectConfigFile(Path.Combine(directory, legacyBasename))).ToArray();
        var firstExistingLegacyResult = legacyResults.FirstOrDefault(result => result.Format != "none");

        DetectPluginConfigResult detectionResult;
        if (canonicalResult.Format != "none")
        {
            detectionResult = canonicalResult with { LegacyPath = firstExistingLegacyResult?.Path };
        }
        else if (firstExistingLegacyResult is not null)
        {
            detectionResult = firstExistingLegacyResult;
        }
        else
        {
            detectionResult = new DetectPluginConfigResult("none", Path.Combine(directory, $"{canonicalBasename}.json"));
        }

        Cache[cacheKey] = detectionResult;
        return detectionResult;
    }

    private static string GetCacheKey(string directory, DetectPluginConfigFileOptions options)
    {
        var basenames = string.Join(',', options.Basenames);
        var legacyBasenames = options.LegacyBasenames is null ? string.Empty : string.Join(',', options.LegacyBasenames);
        return $"{directory}::{basenames}::{legacyBasenames}";
    }
}
