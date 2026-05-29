using Lfe.UlwHostContract;

namespace Lfe.CodexAdapter;

public sealed record CodexAdapterRuntime(
    IUlwHost Host,
    CodexResolvedConfig ResolvedConfig) : IDisposable
{
    public void Dispose()
    {
        if (Host is IDisposable disposable)
            disposable.Dispose();
    }
}

public static class CodexAdapterFactory
{
    public static CodexAdapterRuntime Create(CodexAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolver = new CodexBinaryResolver();
        var config = resolver.ResolveConfig(options);
        var host = new CodexUlwHost(config);

        return new CodexAdapterRuntime(host, config);
    }

    public static CodexAdapterRuntime CreateWithOverride(
        CodexAdapterOptions options,
        Func<CodexResolvedConfig, CodexResolvedConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var resolver = new CodexBinaryResolver();
        var config = configure(resolver.ResolveConfig(options));
        var host = new CodexUlwHost(config);

        return new CodexAdapterRuntime(host, config);
    }
}
