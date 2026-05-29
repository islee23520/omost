using Lfe.Protocol.JsonRpc;
using Lfe.Protocol.Types;

namespace Lfe.Protocol.Methods;

public sealed class InitializeHandler : MethodHandlerBase<InitializeRequestParams, InitializeResult>
{
    private readonly string[] _supportedCapabilities;
    private readonly HashSet<string> _supportedCapabilitySet;

    public InitializeHandler(
        string protocolVersion,
        string implementationName,
        IEnumerable<string> supportedCapabilities,
        string serverMode = LfeServerModes.Standalone)
        : base(LfeMethodNames.Initialize)
    {
        ProtocolVersion = protocolVersion;
        ImplementationName = implementationName;
        ServerMode = serverMode;
        _supportedCapabilities = supportedCapabilities.ToArray();
        _supportedCapabilitySet = new HashSet<string>(_supportedCapabilities, StringComparer.Ordinal);
    }

    public string ImplementationName { get; }

    public string ProtocolVersion { get; }

    public string ServerMode { get; }

    protected override void Validate(InitializeRequestParams parameters)
    {
        RequestValidator.RequireNonEmptyString(parameters.ProtocolVersion, "protocolVersion");
        RequestValidator.RequireNonEmptyString(parameters.HostName, "hostName");
        RequestValidator.RequireNonEmptyString(parameters.HostVersion, "hostVersion");
        RequestValidator.RequireNonEmptyString(parameters.ClientKind, "clientKind");
        RequestValidator.RequireStringArray(parameters.RequestedCapabilities, "requestedCapabilities");

        if (!LfeClientKinds.IsValid(parameters.ClientKind))
        {
            throw LfeProtocolErrors.InvalidRequest(
                $"Method '{MethodName}' received an unsupported clientKind '{parameters.ClientKind}'.");
        }
    }

    protected override ValueTask<InitializeResult> HandleTypedAsync(
        InitializeRequestParams parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(parameters.ProtocolVersion, ProtocolVersion, StringComparison.Ordinal))
        {
            throw LfeProtocolErrors.VersionMismatch(parameters.ProtocolVersion, ProtocolVersion);
        }

        var acceptedCapabilities = parameters.RequestedCapabilities
            .Where(capability => _supportedCapabilitySet.Contains(capability))
            .ToArray();

        return ValueTask.FromResult(new InitializeResult
        {
            AcceptedCapabilities = acceptedCapabilities,
            ImplementationName = ImplementationName,
            ProtocolVersion = ProtocolVersion,
            ServerMode = ServerMode,
        });
    }
}
