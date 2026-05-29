using Google.Protobuf;
using MeshTransit.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetMQ;
using NetMQ.Sockets;

namespace MeshTransit;

/// <summary>
/// Hosts a <see cref="ResponseSocket"/> bound at <see cref="MeshTransitServerOptions.ControlEndpoint"/>,
/// decodes incoming <see cref="Envelope"/>s, dispatches them to the registered
/// <see cref="IMessageHandler{TCommand,TReply}"/>, and writes the reply (or an
/// error envelope on handler exceptions) back on the same socket.
/// </summary>
public sealed class ControlServer<TCommand, TReply> : IDisposable
    where TCommand : IMessage<TCommand>, new()
    where TReply : IMessage<TReply>, new()
{
    private readonly ResponseSocket _socket = new();
    private readonly NetMQPoller _poller = new();
    private readonly IMessageHandler<TCommand, TReply> _handler;
    private readonly string _serviceName;
    private readonly ILogger _logger;
    private readonly MessageParser<TCommand> _parser = new(() => new TCommand());

    private bool _started;
    private bool _disposed;

    public ControlServer(
        IMessageHandler<TCommand, TReply> handler,
        MeshTransitServerOptions options,
        ILogger<ControlServer<TCommand, TReply>>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ControlEndpoint))
            throw new ArgumentException("ControlEndpoint must be set.", nameof(options));

        _handler = handler;
        _serviceName = options.ServiceName ?? string.Empty;
        _logger = (ILogger?)logger ?? NullLogger.Instance;

        _socket.ReceiveReady += OnReceiveReady;
        _poller.Add(_socket);

        _socket.Bind(options.ControlEndpoint);
        BoundEndpoint = options.ControlEndpoint;
    }

    public string BoundEndpoint { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started) return Task.CompletedTask;
        _poller.RunAsync($"meshtransit-control-{BoundEndpoint}");
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started) return Task.CompletedTask;
        try { _poller.StopAsync(); } catch { }
        _started = false;
        return Task.CompletedTask;
    }

    private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        // REQ/REP is strict request-then-reply; we must always send exactly one reply.
        byte[] frame;
        try
        {
            frame = e.Socket.ReceiveFrameBytes();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MeshTransit control socket receive failed.");
            return;
        }

        Envelope reply;
        string correlationId = string.Empty;
        try
        {
            var inbound = EnvelopeCodec.FromBytes(frame);
            correlationId = inbound.Header?.CorrelationId ?? string.Empty;
            var command = _parser.ParseFrom(inbound.Payload);

            // Handler is invoked synchronously on the poller thread; we block on
            // the returned task. Consumer handlers should be cheap or offload
            // their own work — REP is single-flight by design.
            var replyMessage = _handler.HandleAsync(command, CancellationToken.None)
                .GetAwaiter().GetResult();

            reply = EnvelopeCodec.Encode(replyMessage, _serviceName, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MeshTransit handler threw; returning error envelope.");
            reply = EnvelopeCodec.EncodeError(
                _serviceName,
                correlationId,
                code: ex.GetType().Name,
                message: ex.Message,
                details: ex.ToString());
        }

        try
        {
            e.Socket.SendFrame(EnvelopeCodec.ToBytes(reply));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MeshTransit control socket send failed.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (_poller.IsRunning) _poller.StopAsync(); } catch { }
        _poller.Dispose();
        _socket.Dispose();
    }
}
