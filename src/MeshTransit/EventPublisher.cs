using Google.Protobuf;
using MeshTransit.Contracts;

namespace MeshTransit;



/// <summary>
/// Typed wrapper that publishes <typeparamref name="TEvent"/> messages over a
/// shared <see cref="EventBus"/>. Topics are consumer-defined; the framework
/// reserves only the <see cref="MeshTransitProtocol.ReservedTopicPrefix"/>
/// namespace.
/// </summary>
public sealed class EventPublisher<TEvent> where TEvent : IMessage<TEvent>
{
    private readonly EventBus _bus;
    private readonly string _sourceService;

    public EventPublisher(EventBus bus, string sourceService)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        _sourceService = sourceService ?? string.Empty;
    }

    /// <summary>
    /// Publishes <paramref name="event"/> on <paramref name="topic"/>. The
    /// topic must not start with the framework-reserved prefix.
    /// </summary>
    public void Publish(string topic, TEvent @event)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        if (topic.StartsWith(MeshTransitProtocol.ReservedTopicPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Topic '{topic}' uses the reserved '{MeshTransitProtocol.ReservedTopicPrefix}' prefix.",
                nameof(topic));
        }
        var envelope = EnvelopeCodec.Encode(@event, _sourceService);
        _bus.Publish(topic, EnvelopeCodec.ToBytes(envelope));
    }
}
