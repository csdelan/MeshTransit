using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MeshTransit;

/// <summary>
/// Generic-host adapter that brings up just an <see cref="EventBus"/> and a
/// <see cref="HeartbeatPublisher"/> — liveness advertisement with no command server.
/// Registered by <c>AddMeshTransitHeartbeat</c>. For the full command-server case the
/// equivalent wiring lives in <see cref="MeshTransitHostedService{TCommand,TReply}"/>;
/// the two are mutually exclusive (both bind the event endpoint).
/// </summary>
public sealed class HeartbeatHostedService : IHostedService, IAsyncDisposable
{
    private readonly HeartbeatOptions _options;
    private readonly EventBus _bus;
    private readonly HeartbeatPublisher _heartbeat;

    public HeartbeatHostedService(
        IOptions<HeartbeatOptions> options,
        EventBus bus,
        HeartbeatPublisher heartbeat)
    {
        _options = options.Value;
        _bus = bus;
        _heartbeat = heartbeat;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.EventEndpoint))
            throw new InvalidOperationException("HeartbeatOptions.EventEndpoint must be set.");

        _bus.Bind(_options.EventEndpoint);
        await _heartbeat.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _heartbeat.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _heartbeat.DisposeAsync().ConfigureAwait(false);
        _bus.Dispose();
    }
}
