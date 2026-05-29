using Google.Protobuf.WellKnownTypes;
using MeshTransit.Contracts;

namespace MeshTransit;

/// <summary>
/// Periodically broadcasts <see cref="Heartbeat"/> messages on the reserved
/// <c>_mt.heartbeat.&lt;service-name&gt;</c> topic through the shared
/// <see cref="EventBus"/>. Status transitions can be signaled via
/// <see cref="SetStatus"/>; a final <see cref="ServiceStatus.Stopping"/>
/// heartbeat is drained on <see cref="StopAsync"/>.
/// </summary>
public sealed class HeartbeatPublisher : IAsyncDisposable
{
    private readonly EventBus _bus;
    private readonly string _serviceName;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly Timestamp _startedAt = Timestamp.FromDateTime(DateTime.UtcNow);
    private readonly int _intervalMs;
    private readonly Dictionary<string, string> _metadata;
    private readonly string _topic;

    private long _sequence;
    private int _status = (int)ServiceStatus.Starting;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public HeartbeatPublisher(EventBus bus, MeshTransitServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ServiceName))
            throw new ArgumentException("ServiceName must be set.", nameof(options));

        _bus = bus;
        _serviceName = options.ServiceName;
        _intervalMs = Math.Max(50, options.HeartbeatIntervalMs);
        _metadata = new Dictionary<string, string>(options.HeartbeatMetadata);
        _topic = MeshTransitProtocol.HeartbeatTopic(_serviceName);
    }

    public string InstanceId => _instanceId;
    public ServiceStatus Status => (ServiceStatus)Volatile.Read(ref _status);

    public void SetStatus(ServiceStatus status) =>
        Volatile.Write(ref _status, (int)status);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_loop is not null) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        SetStatus(ServiceStatus.Healthy);
        _loop = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loop is null) return;
        SetStatus(ServiceStatus.Stopping);
        try { PublishOnce(); } catch { /* best-effort drain */ }
        try { _cts?.Cancel(); } catch { }
        try { await _loop.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _loop = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(_intervalMs);
        while (!ct.IsCancellationRequested)
        {
            PublishOnce();
            try { await Task.Delay(delay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void PublishOnce()
    {
        var hb = new Heartbeat
        {
            ServiceName = _serviceName,
            InstanceId = _instanceId,
            StartedAt = _startedAt,
            SentAt = Timestamp.FromDateTime(DateTime.UtcNow),
            Sequence = (ulong)Interlocked.Increment(ref _sequence),
            Status = Status,
            IntervalMs = (uint)_intervalMs,
        };
        foreach (var kv in _metadata) hb.Metadata[kv.Key] = kv.Value;

        var envelope = EnvelopeCodec.Encode(hb, _serviceName);
        _bus.Publish(_topic, EnvelopeCodec.ToBytes(envelope));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _cts?.Dispose();
    }
}
