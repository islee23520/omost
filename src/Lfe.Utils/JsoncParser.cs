namespace Lfe.Utils;

public static class JsoncParser
{
    public static T ParseJsonc<T>(string content) => JsoncParse.Parse<T>(content);

    public static JsoncParseResult<T> ParseJsoncSafe<T>(string content) => JsoncParse.ParseSafe<T>(content);

    public static T? ReadJsoncFile<T>(string filePath) => JsoncParse.ReadFile<T>(filePath);

    public static DetectPluginConfigResult DetectConfigFile(string basePath) => PluginConfigDetection.DetectConfigFile(basePath);

    public static void ClearPluginConfigFileDetectionCache() => PluginConfigDetection.ClearCache();

    public static DetectPluginConfigResult DetectPluginConfigFile(string directory, DetectPluginConfigFileOptions options) => PluginConfigDetection.DetectPluginConfigFile(directory, options);
}
