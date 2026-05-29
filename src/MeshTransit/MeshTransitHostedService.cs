using Google.Protobuf;
using MeshTransit.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MeshTransit;

/// <summary>
/// Generic-host adapter that brings up the event bus, control server, and
/// heartbeat publisher in a single <see cref="IHostedService"/>. Registered
/// by <c>AddMeshTransitServer</c>.
/// </summary>
public sealed class MeshTransitHostedService<TCommand, TReply> : IHostedService, IAsyncDisposable
    where TCommand : IMessage<TCommand>, new()
    where TReply : IMessage<TReply>, new()
{
    private readonly MeshTransitServerOptions _options;
    private readonly EventBus _bus;
    private readonly ControlServer<TCommand, TReply> _control;
    private readonly HeartbeatPublisher _heartbeat;

    public MeshTransitHostedService(
        IOptions<MeshTransitServerOptions> options,
        EventBus bus,
        ControlServer<TCommand, TReply> control,
        HeartbeatPublisher heartbeat)
    {
        _options = options.Value;
        _bus = bus;
        _control = control;
        _heartbeat = heartbeat;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _bus.Bind(_options.EventEndpoint);
        await _control.StartAsync(cancellationToken).ConfigureAwait(false);
        await _heartbeat.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _heartbeat.StopAsync(cancellationToken).ConfigureAwait(false);
        await _control.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _heartbeat.DisposeAsync().ConfigureAwait(false);
        _control.Dispose();
        _bus.Dispose();
    }
}
