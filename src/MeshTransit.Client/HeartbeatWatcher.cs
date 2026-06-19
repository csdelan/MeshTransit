using System.Collections.Concurrent;
using MeshTransit.Contracts;
using NetMQ;
using NetMQ.Sockets;

namespace MeshTransit.Client;

/// <summary>
/// Reason an observed service transitioned to Down.
/// </summary>
public enum ServiceDownReason
{
    MissedHeartbeats,
    Graceful,
}

/// <summary>
/// Snapshot of a peer's observed liveness state.
/// </summary>
public sealed class ServiceLiveness
{
    public string Name { get; internal set; } = string.Empty;
    public string InstanceId { get; internal set; } = string.Empty;
    public ServiceStatus Status { get; internal set; } = ServiceStatus.Unspecified;
    public DateTime LastHeartbeat { get; internal set; } = DateTime.MinValue;

    /// <summary>When the peer's current instance started (UTC), as reported on the wire.</summary>
    public DateTime StartedAt { get; internal set; } = DateTime.MinValue;

    public uint IntervalMs { get; internal set; }
    public bool IsUp { get; internal set; }
    public ServiceDownReason LastDownReason { get; internal set; } = ServiceDownReason.MissedHeartbeats;

    /// <summary>
    /// How long the current instance has been up, measured from <see cref="StartedAt"/> to
    /// the last observed heartbeat. <see cref="TimeSpan.Zero"/> until a heartbeat carrying a
    /// start time has been seen.
    /// </summary>
    public TimeSpan Uptime =>
        StartedAt == DateTime.MinValue || LastHeartbeat == DateTime.MinValue
            ? TimeSpan.Zero
            : LastHeartbeat - StartedAt;
}

/// <summary>
/// Subscribes to the framework heartbeat topic across a static list of peers
/// and raises lifecycle events as their liveness changes. Independent of the
/// domain-event <see cref="EventSubscriber{TEvent}"/>.
/// </summary>
public sealed class HeartbeatWatcher : IDisposable
{
    private readonly IReadOnlyList<ServiceEndpoint> _peers;
    private readonly TimeSpan _checkInterval;
    private readonly int _missTolerance;
    private readonly int _minIntervalMs;
    private readonly ConcurrentDictionary<string, ServiceLiveness> _services = new();

    private SubscriberSocket? _socket;
    private NetMQPoller? _poller;
    private NetMQTimer? _checkTimer;
    private bool _disposed;

    public HeartbeatWatcher(
        IEnumerable<ServiceEndpoint> peers,
        int missTolerance = 3,
        int minIntervalMs = 250,
        TimeSpan? checkInterval = null)
    {
        ArgumentNullException.ThrowIfNull(peers);
        _peers = peers.ToList();
        _missTolerance = Math.Max(1, missTolerance);
        _minIntervalMs = Math.Max(50, minIntervalMs);
        _checkInterval = checkInterval ?? TimeSpan.FromMilliseconds(250);
    }

    public event Action<ServiceLiveness>? ServiceUp;
    public event Action<ServiceLiveness>? ServiceDown;
    public event Action<ServiceLiveness>? ServiceRestarted;
    public event Action<ServiceLiveness>? StatusChanged;

    /// <summary>Snapshot of every peer the watcher has heard from at least once.</summary>
    public IReadOnlyCollection<ServiceLiveness> Services => _services.Values.ToList();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_poller is not null) return Task.CompletedTask;

        _socket = new SubscriberSocket();
        foreach (var peer in _peers)
        {
            if (!string.IsNullOrEmpty(peer.EventEndpoint))
                _socket.Connect(peer.EventEndpoint);
        }
        _socket.Subscribe(MeshTransitProtocol.HeartbeatTopicPrefix);
        _socket.ReceiveReady += OnReceiveReady;

        _checkTimer = new NetMQTimer(_checkInterval);
        _checkTimer.Elapsed += (_, _) => CheckLiveness();

        _poller = new NetMQPoller { _socket, _checkTimer };
        _poller.RunAsync("meshtransit-heartbeat-watcher");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_poller is null) return Task.CompletedTask;
        try { _poller.StopAsync(); } catch { }
        return Task.CompletedTask;
    }

    private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        if (!e.Socket.TryReceiveFrameString(out _)) return;
        if (!e.Socket.TryReceiveFrameBytes(out var frame) || frame is null) return;

        Heartbeat hb;
        try
        {
            var envelope = EnvelopeCodec.FromBytes(frame);
            hb = Heartbeat.Parser.ParseFrom(envelope.Payload);
        }
        catch { return; }

        if (string.IsNullOrEmpty(hb.ServiceName)) return;
        var now = DateTime.UtcNow;
        var startedAt = hb.StartedAt?.ToDateTime() ?? DateTime.MinValue;

        var liveness = _services.AddOrUpdate(
            hb.ServiceName,
            _ => new ServiceLiveness
            {
                Name = hb.ServiceName,
                InstanceId = hb.InstanceId,
                Status = hb.Status,
                LastHeartbeat = now,
                StartedAt = startedAt,
                IntervalMs = hb.IntervalMs,
                IsUp = false,
            },
            (_, existing) => existing);

        bool wasUp;
        bool restarted = false;
        bool statusChanged = false;
        lock (liveness)
        {
            wasUp = liveness.IsUp;
            if (!string.IsNullOrEmpty(liveness.InstanceId)
                && !string.Equals(liveness.InstanceId, hb.InstanceId, StringComparison.Ordinal))
            {
                restarted = true;
            }
            if (liveness.Status != hb.Status) statusChanged = true;

            liveness.InstanceId = hb.InstanceId;
            liveness.Status = hb.Status;
            liveness.LastHeartbeat = now;
            if (startedAt != DateTime.MinValue) liveness.StartedAt = startedAt;
            liveness.IntervalMs = hb.IntervalMs == 0 ? liveness.IntervalMs : hb.IntervalMs;

            if (hb.Status == ServiceStatus.Stopping)
            {
                liveness.IsUp = false;
                liveness.LastDownReason = ServiceDownReason.Graceful;
            }
            else
            {
                liveness.IsUp = true;
            }
        }

        if (!wasUp && liveness.IsUp) ServiceUp?.Invoke(liveness);
        if (restarted) ServiceRestarted?.Invoke(liveness);
        if (statusChanged) StatusChanged?.Invoke(liveness);
        if (hb.Status == ServiceStatus.Stopping && wasUp) ServiceDown?.Invoke(liveness);
    }

    private void CheckLiveness()
    {
        var now = DateTime.UtcNow;
        foreach (var s in _services.Values)
        {
            bool shouldDown = false;
            lock (s)
            {
                if (!s.IsUp) continue;
                var interval = Math.Max((int)s.IntervalMs, _minIntervalMs);
                var threshold = TimeSpan.FromMilliseconds(interval * _missTolerance);
                if (now - s.LastHeartbeat > threshold)
                {
                    s.IsUp = false;
                    s.LastDownReason = ServiceDownReason.MissedHeartbeats;
                    shouldDown = true;
                }
            }
            if (shouldDown) ServiceDown?.Invoke(s);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _poller?.StopAsync(); } catch { }
        _poller?.Dispose();
        _socket?.Dispose();
    }
}
