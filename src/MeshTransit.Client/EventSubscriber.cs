using System.Threading.Channels;
using Google.Protobuf;
using MeshTransit.Contracts;
using NetMQ;
using NetMQ.Sockets;

namespace MeshTransit.Client;

/// <summary>
/// SUB-side consumer for a single peer's event stream. Subscribes to a topic
/// prefix and surfaces decoded <typeparamref name="TEvent"/> instances either
/// via the <see cref="EventReceived"/> event or
/// <see cref="ReadAllAsync"/> for <c>await foreach</c> consumption.
/// </summary>
public sealed class EventSubscriber<TEvent> : IDisposable
    where TEvent : IMessage<TEvent>, new()
{
    private readonly string _endpoint;
    private readonly string _topicPrefix;
    private readonly MessageParser<TEvent> _parser = new(() => new TEvent());
    private readonly Channel<TEvent> _channel = Channel.CreateUnbounded<TEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

    private SubscriberSocket? _socket;
    private NetMQPoller? _poller;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public EventSubscriber(string endpoint, string topicPrefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNull(topicPrefix);
        _endpoint = endpoint;
        _topicPrefix = topicPrefix;
    }

    /// <summary>Raised on the poller thread for each decoded event.</summary>
    public event Action<string, TEvent>? EventReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_poller is not null) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _socket = new SubscriberSocket();
        _socket.Connect(_endpoint);
        _socket.Subscribe(_topicPrefix);
        _socket.ReceiveReady += OnReceiveReady;

        _poller = new NetMQPoller { _socket };
        _poller.RunAsync($"meshtransit-sub-{_endpoint}");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_poller is null) return Task.CompletedTask;
        try { _poller.StopAsync(); } catch { }
        try { _cts?.Cancel(); } catch { }
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    /// <summary>Reads decoded events until <see cref="StopAsync"/>.</summary>
    public IAsyncEnumerable<TEvent> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        // Frame 0: topic. Frame 1: serialized Envelope.
        if (!e.Socket.TryReceiveFrameString(out var topic)) return;
        if (!e.Socket.TryReceiveFrameBytes(out var frame) || frame is null) return;

        try
        {
            var envelope = EnvelopeCodec.FromBytes(frame);
            var payload = _parser.ParseFrom(envelope.Payload);
            EventReceived?.Invoke(topic, payload);
            _channel.Writer.TryWrite(payload);
        }
        catch
        {
            // Malformed frames are dropped — SUB is best-effort by design.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _poller?.StopAsync(); } catch { }
        _poller?.Dispose();
        _socket?.Dispose();
        _cts?.Dispose();
        _channel.Writer.TryComplete();
    }
}
