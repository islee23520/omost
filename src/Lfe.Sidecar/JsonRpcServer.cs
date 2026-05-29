using System.Text.Json;

using Lfe.Protocol.Execution;
using Lfe.Protocol.JsonRpc;
using Lfe.Protocol.Methods;
using Lfe.Protocol.Notifications;
using Lfe.Protocol.Types;

namespace Lfe.Sidecar;

public sealed class JsonRpcServer : INotificationEmitter, IAsyncDisposable
{
    private readonly TextWriter _errorWriter;
    private bool _disposed;
    private bool _listening;
    private readonly List<Action> _closeHandlers = new();
    private readonly List<Action<Exception>> _errorHandlers = new();
    private readonly Dictionary<string, Action<JsonElement>> _notificationHandlers = new(StringComparer.Ordinal);
    private byte[] _inputBuffer = Array.Empty<byte>();
    private readonly Dictionary<string, IMethodHandler> _methodHandlers = new(StringComparer.Ordinal);
    private readonly Stream _reader;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly Stream _writer;

    public JsonRpcServer(Stream reader, Stream writer, TextWriter errorWriter)
    {
        _reader = reader;
        _writer = writer;
        _errorWriter = errorWriter;
        _serializerOptions = JsonRpcProtocol.SerializerOptions;

        var serverState = new LfeServerState();
        var progressEmitter = new ProgressEmitter(this);
        var resultEmitter = new ResultEmitter(this);
        var errorEmitter = new ErrorEmitter(this);
        var runExecutor = new ProtocolConformanceExecutor(serverState, progressEmitter, resultEmitter, errorEmitter);

        var initializeHandler = new InitializeHandler(
            OmoProtocolInfo.ProtocolVersion,
            OmoProtocolInfo.ImplementationName,
            OmoCapabilityNames.All);
        _methodHandlers[initializeHandler.MethodName] = initializeHandler;

        var sessionStartHandler = new SessionStartHandler(serverState);
        _methodHandlers[sessionStartHandler.MethodName] = sessionStartHandler;

        var runDispatchHandler = new RunDispatchHandler(runExecutor);
        _methodHandlers[runDispatchHandler.MethodName] = runDispatchHandler;

        var runCancelHandler = new RunCancelHandler(runExecutor);
        _methodHandlers[runCancelHandler.MethodName] = runCancelHandler;
    }

    public async Task AcceptChunkAsync(ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        if (chunk.Length == 0)
        {
            return;
        }

        var nextBuffer = new byte[_inputBuffer.Length + chunk.Length];
        Buffer.BlockCopy(_inputBuffer, 0, nextBuffer, 0, _inputBuffer.Length);
        chunk.CopyTo(nextBuffer.AsMemory(_inputBuffer.Length));
        _inputBuffer = nextBuffer;

        await DrainInputBufferAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task EmitAsync<TParams>(string methodName, TParams parameters, CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.CompletedTask;
        return WriteMessageAsync(new JsonRpcNotificationMessage<TParams>
        {
            Method = methodName,
            Params = parameters,
        }, cancellationToken);
    }

    public void OnClose(Action handler) => _closeHandlers.Add(handler);

    public void OnError(Action<Exception> handler) => _errorHandlers.Add(handler);

    public void OnNotification(string method, Action<JsonElement> handler) => _notificationHandlers[method] = handler;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        if (_listening) throw new InvalidOperationException("Server is already listening.");
        _listening = true;

        var chunkBuffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var bytesRead = await _reader.ReadAsync(chunkBuffer.AsMemory(0, chunkBuffer.Length), cancellationToken)
                        .ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        foreach (var handler in _closeHandlers) { try { handler(); } catch { } }
                        return;
                    }

                    await AcceptChunkAsync(chunkBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException exception)
                {
                    await WriteErrorAsync(null, OmoProtocolErrors.InvalidRequest(exception.Message).ToJsonRpcError(), cancellationToken)
                        .ConfigureAwait(false);
                    await LogErrorAsync(exception.Message).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            foreach (var handler in _errorHandlers) { try { handler(exception); } catch { } }
            foreach (var handler in _closeHandlers) { try { handler(); } catch { } }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var handler in _closeHandlers) { try { handler(); } catch { } }
        _closeHandlers.Clear();
        _errorHandlers.Clear();
        _methodHandlers.Clear();
        _notificationHandlers.Clear();
        _inputBuffer = Array.Empty<byte>();
    }

    private async Task DrainInputBufferAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var headerEnd = IndexOfHeaderSeparator(_inputBuffer);
            if (headerEnd < 0)
            {
                return;
            }

            var headers = System.Text.Encoding.ASCII.GetString(_inputBuffer, 0, headerEnd);
            var contentLength = ContentLengthFraming.ParseContentLength(headers);
            if (!contentLength.HasValue)
            {
                _inputBuffer = Array.Empty<byte>();
                throw new InvalidDataException("Missing or invalid Content-Length header.");
            }

            var bodyStart = headerEnd + 4;
            var bodyEnd = bodyStart + contentLength.Value;
            if (_inputBuffer.Length < bodyEnd)
            {
                return;
            }

            var body = System.Text.Encoding.UTF8.GetString(_inputBuffer, bodyStart, contentLength.Value);
            var remaining = _inputBuffer.Length - bodyEnd;
            if (remaining == 0)
            {
                _inputBuffer = Array.Empty<byte>();
            }
            else
            {
                var nextBuffer = new byte[remaining];
                Buffer.BlockCopy(_inputBuffer, bodyEnd, nextBuffer, 0, remaining);
                _inputBuffer = nextBuffer;
            }

            await DispatchBodyAsync(body, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchBodyAsync(string body, CancellationToken cancellationToken)
    {
        JsonRpcInboundRequestMessage? request;

        try
        {
            request = JsonSerializer.Deserialize<JsonRpcInboundRequestMessage>(body, _serializerOptions);
        }
        catch (JsonException exception)
        {
            await WriteErrorAsync(null, OmoProtocolErrors.ParseError(exception.Message).ToJsonRpcError(), cancellationToken)
                .ConfigureAwait(false);
            await LogErrorAsync(exception.Message).ConfigureAwait(false);
            return;
        }

        if (request is null ||
            !string.Equals(request.Jsonrpc, JsonRpcProtocol.Version, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.Method))
        {
            await WriteErrorAsync(null, OmoProtocolErrors.InvalidRequest("Invalid JSON-RPC 2.0 request envelope.").ToJsonRpcError(), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (request.Id.ValueKind == JsonValueKind.Undefined)
        {
            HandleNotification(request.Method, request.Params);
            return;
        }

        if (!JsonRpcProtocol.TryConvertId(request.Id, out var id))
        {
            await WriteErrorAsync(null, OmoProtocolErrors.InvalidRequest("Invalid JSON-RPC request id.").ToJsonRpcError(), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!_methodHandlers.TryGetValue(request.Method, out var handler))
        {
            await WriteErrorAsync(id, OmoProtocolErrors.MethodNotFound(request.Method).ToJsonRpcError(), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            var result = await handler.HandleAsync(request.Params, cancellationToken).ConfigureAwait(false);
            await WriteMessageAsync(new JsonRpcSuccessResponseMessage<object>
            {
                Id = id,
                Result = result,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OmoProtocolException exception)
        {
            await WriteErrorAsync(id, exception.ToJsonRpcError(), cancellationToken).ConfigureAwait(false);
            await LogErrorAsync(exception.Message).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(id, OmoProtocolErrors.InternalError(exception.Message).ToJsonRpcError(), cancellationToken)
                .ConfigureAwait(false);
            await LogErrorAsync(exception.ToString()).ConfigureAwait(false);
        }
    }

    private void HandleNotification(string method, JsonElement paramsElement)
    {
        if (!_notificationHandlers.TryGetValue(method, out var handler)) return;
        try
        {
            handler(paramsElement);
        }
        catch (Exception exception)
        {
            foreach (var errorHandler in _errorHandlers) { try { errorHandler(exception); } catch { } }
            _ = LogErrorAsync($"Notification handler error for '{method}': {exception.Message}");
        }
    }

    private async Task LogErrorAsync(string message)
    {
        await _errorWriter.WriteLineAsync(message).ConfigureAwait(false);
        await _errorWriter.FlushAsync().ConfigureAwait(false);
    }

    private Task WriteErrorAsync(object? id, JsonRpcError error, CancellationToken cancellationToken)
    {
        return WriteMessageAsync(new JsonRpcErrorResponseMessage
        {
            Error = error,
            Id = id,
        }, cancellationToken);
    }

    private Task WriteMessageAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
    {
        return ContentLengthFraming.WriteMessageAsync(_writer, message, _serializerOptions, cancellationToken).AsTask();
    }

    private static int IndexOfHeaderSeparator(byte[] buffer)
    {
        for (var index = 0; index <= buffer.Length - 4; index += 1)
        {
            if (buffer[index] == (byte)'\r' &&
                buffer[index + 1] == (byte)'\n' &&
                buffer[index + 2] == (byte)'\r' &&
                buffer[index + 3] == (byte)'\n')
            {
                return index;
            }
        }

        return -1;
    }
}
