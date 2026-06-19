using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MeshTransit;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a MeshTransit server: a shared <see cref="EventBus"/>, a
    /// <see cref="ControlServer{TCommand,TReply}"/> dispatching to
    /// <typeparamref name="THandler"/>, a typed
    /// <see cref="EventPublisher{TEvent}"/> for the consumer's event type, and
    /// an automatic <see cref="HeartbeatPublisher"/>. All three are brought up
    /// by a single hosted service.
    /// </summary>
    public static IServiceCollection AddMeshTransitServer<TCommand, TReply, TEvent, THandler>(
        this IServiceCollection services,
        Action<MeshTransitServerOptions> configure)
        where TCommand : IMessage<TCommand>, new()
        where TReply : IMessage<TReply>, new()
        where TEvent : IMessage<TEvent>
        where THandler : class, IMessageHandler<TCommand, TReply>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<IMessageHandler<TCommand, TReply>, THandler>();
        services.AddSingleton<EventBus>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MeshTransitServerOptions>>().Value;
            var bus = sp.GetRequiredService<EventBus>();
            return new EventPublisher<TEvent>(bus, opts.ServiceName);
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MeshTransitServerOptions>>().Value;
            var bus = sp.GetRequiredService<EventBus>();
            return new HeartbeatPublisher(bus, opts);
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MeshTransitServerOptions>>().Value;
            var handler = sp.GetRequiredService<IMessageHandler<TCommand, TReply>>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<ControlServer<TCommand, TReply>>>();
            return new ControlServer<TCommand, TReply>(handler, opts, logger);
        });

        services.AddSingleton<IHostedService, MeshTransitHostedService<TCommand, TReply>>();
        return services;
    }

    /// <summary>
    /// Registers a standalone heartbeat publisher: a dedicated <see cref="EventBus"/>, a
    /// <see cref="HeartbeatPublisher"/>, and a hosted service that binds the event endpoint
    /// and runs the publisher. Use this when a process only needs to advertise liveness on
    /// the mesh and does not host a command server. Do not combine with
    /// <see cref="AddMeshTransitServer{TCommand,TReply,TEvent,THandler}"/> in the same
    /// container — both bind the event endpoint.
    /// </summary>
    public static IServiceCollection AddMeshTransitHeartbeat(
        this IServiceCollection services,
        Action<HeartbeatOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<EventBus>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<HeartbeatOptions>>().Value;
            var bus = sp.GetRequiredService<EventBus>();
            return new HeartbeatPublisher(bus, opts);
        });

        services.AddSingleton<IHostedService, HeartbeatHostedService>();
        return services;
    }
}
