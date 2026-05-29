using MeshTransit.Contracts;
using NetMQ;
using NetMQ.Sockets;

namespace MeshTransit;

/// <summary>
/// Owns the PUB socket. Both <see cref="EventPublisher{TEvt}"/> and
/// <see cref="HeartbeatPublisher"/> publish through this single shared socket
/// — one bound port carries both domain events and framework heartbeats.
/// </summary>
public sealed class EventBus : IDisposable
{
    private readonly PublisherSocket _socket = new();
    private readonly NetMQQueue<(string Topic, byte[] Frame)> _queue = new();
    private readonly NetMQPoller _poller = new();
    private readonly object _startLock = new();
    private bool _started;
    private bool _disposed;

    public EventBus()
    {
        _queue.ReceiveReady += OnDequeue;
        _poller.Add(_queue);
        _poller.Add(_socket);
    }

    /// <summary>Bind address, e.g. <c>tcp://*:9001</c>.</summary>
    public string? BoundEndpoint { get; private set; }

    public void Bind(string endpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        lock (_startLock)
        {
            if (_started) throw new InvalidOperationException("EventBus is already started.");
            _socket.Bind(endpoint);
            BoundEndpoint = endpoint;
            _poller.RunAsync($"meshtransit-eventbus-{endpoint}");
            _started = true;
        }
    }

    /// <summary>
    /// Enqueues a pre-encoded envelope frame for publication under
    /// <paramref name="topic"/>. Safe to call from any thread.
    /// </summary>
    public void Publish(string topic, byte[] frame)
    {
        if (_disposed) return;
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(frame);
        _queue.Enqueue((topic, frame));
    }

    private void OnDequeue(object? sender, NetMQQueueEventArgs<(string Topic, byte[] Frame)> e)
    {
        while (e.Queue.TryDequeue(out var item, TimeSpan.Zero))
        {
            _socket.SendMoreFrame(item.Topic).SendFrame(item.Frame);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_poller.IsRunning) _poller.StopAsync();
        }
        catch { /* tolerated on shutdown */ }
        _poller.Dispose();
        _queue.Dispose();
        _socket.Dispose();
    }
}
